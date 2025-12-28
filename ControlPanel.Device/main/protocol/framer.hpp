#pragma once

#include <stdint.h>
#include <ranges>
#include <algorithm>
#include <etl/byte_stream.h>
#include <etl/crc16_ccitt.h>

#include "esp_log.h"

#include "utils.hpp"
#include "frame_buffer.hpp"

namespace transport
{
    enum class frame_type_t : uint8_t
    {
        data = 0,
        ack = 1
    };

    enum class frame_field_t
    {
        magic,
        len,
        seq,
        type,
        data,
        crc16
    };

    struct frame_t
    {
        uint16_t seq;
        frame_type_t type;
        std::span<uint8_t> data;
    };

    template<std::size_t MagicSize, std::size_t BufferSize>
    class framer_t {
        static constexpr char TAG[] = "FRAMER";

    public:

        using seq_t = uint16_t;
        using type_t = std::underlying_type_t<frame_type_t>;
        using len_t = uint16_t;

        framer_t(std::span<const uint8_t, MagicSize> magic)
            : _magic(magic)
        {
        }

        std::size_t to_bytes(std::span<uint8_t> buffer, const frame_t& frame)
        {
            configASSERT(calc_frame_size(frame.data.size()) < buffer.size());

            etl::byte_stream_writer writer(buffer, etl::endian::big);

            writer.write<const uint8_t>(_magic);
            writer.write<len_t>(frame.data.size());
            writer.write<seq_t>(frame.seq);
            writer.write<type_t>((uint8_t)frame.type);
            writer.write<uint8_t>(frame.data);
            writer.write<uint16_t>(crc16_ccitt(writer.used_data()));

            ESP_LOGD(TAG, "frame to bytes seq=%d type=%d size=%d", frame.seq, (uint8_t)frame.type, frame.data.size());

            return writer.size_bytes();
        }

        template<frame_field_t... excludes>
        static constexpr uint16_t calc_frame_size(uint16_t data_size)
        {
            uint16_t ret = data_size;
            
            constexpr std::array<frame_field_t, sizeof...(excludes)> ex{ excludes... };

            for (auto [t, s]: _field_sizes)
            {
                if (std::ranges::find(ex, t) != ex.end())
                    continue;
                ret += s;
            }

            return ret;
        }

        template <class F>
        void feed(std::span<const uint8_t> data, F&& on_frame)
        {
            if (data.size() == 0)
                return;

            if (!_buffer.try_insert(data))
            {
                _buffer.shift_left_from(_magic);
                _last_frame_start = -1;
                if (!_buffer.try_insert(data))
                {
                    ESP_LOGE(TAG, "buffer to small (%d), dropping buffers", _buffer.capacity() - _buffer.size());
                    _buffer.clear();
                    return;
                }
            }

            auto span = _buffer.span();

            while (true)
            {
                if (_last_frame_start == -1)
                {
                    auto start = find_sequence(span, _magic);
                    if (start == -1)
                        return;

                    _last_frame_start = start;
                    ESP_LOGI(TAG, "frame found at %d", _last_frame_start);
                }

                auto reader_span = span.subspan(_last_frame_start);

                etl::byte_stream_reader reader((void*)reader_span.data(), reader_span.size(), etl::endian::big);
                reader.skip<uint8_t>(_magic.size());

                auto len = reader.read<len_t>();
                if (!len || reader.available_bytes() < calc_frame_size<frame_field_t::magic, frame_field_t::len>(*len))
                    return;
                
                auto seq = reader.read<seq_t>();
                if (!seq)
                    return;
                
                auto type = reader.read<type_t>();
                if (!type)
                    return;
                
                auto data = reader.free_data().subspan(0, *len);
                if (!reader.skip<uint8_t>(data.size()))
                    return;

                auto crc16 = reader.read<ushort>();
                if (!crc16)
                    return;

                auto frame_crc16 = crc16_ccitt(reader_span.subspan(0, calc_frame_size<frame_field_t::crc16>(*len)));

                if (frame_crc16 == crc16)
                {
                    on_frame(frame_t{*seq, (frame_type_t)*type, {(uint8_t*)data.data(), data.size()}});
                }
                else
                {
                    ESP_LOGE(TAG, "bad crc16 %d != %d", frame_crc16, crc16);
                }

                _buffer.seek(_last_frame_start + reader.used_data().size());
                _last_frame_start = -1;
                span = _buffer.span();
            }
        }

    private:
        uint16_t crc16_ccitt(auto span)
        {
            etl::crc16_ccitt_t<4> crc(span.begin(), span.end());
            return crc.value();
        }

    private:
        const std::span<const uint8_t, MagicSize> _magic;
        frame_buffer_t<BufferSize> _buffer;
        int32_t _last_frame_start = -1;

        static constexpr std::array<std::tuple<frame_field_t, uint16_t>, 5> _field_sizes {{
            { frame_field_t::magic, MagicSize },
            { frame_field_t::len, sizeof(len_t) },
            { frame_field_t::seq, sizeof(seq_t) },
            { frame_field_t::type, sizeof(type_t) },
            { frame_field_t::crc16, sizeof(ushort) },
        }};
    };
}