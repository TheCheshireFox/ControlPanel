#pragma once

#include "frame_buffer.hpp"
#include <etl/byte_stream.h>
#include <etl/unaligned_type.h>
#include <algorithm>

namespace transport
{
    template<std::size_t BufferSize>
    struct framer_t
    {
        static constexpr char TAG[] = "Framer";

        using magic_t = std::array<uint8_t, 2>;
        using len_t = uint16_t;

        static constexpr magic_t magic = {0xAB, 0xBC};

        bool try_insert(std::span<const uint8_t> raw_data)
        {
            if (!_frame_buffer.try_insert(raw_data))
            {
                _frame_buffer.shift_left_from(magic);
                if (!_frame_buffer.try_insert(raw_data))
                {
                    ESP_LOGW(TAG, "Frame buffer overflow (data=%d, size=%d, capacity=%d), dropping buffer",
                        raw_data.size(), _frame_buffer.size(), _frame_buffer.capacity());

                    _frame_buffer.clear();
                    if (!_frame_buffer.try_insert(raw_data))
                    {
                        ESP_LOGW(TAG, "Data too large (data=%d, capacity=%d)", raw_data.size(), _frame_buffer.capacity());
                        return false;
                    }
                }
            }

            return true;
        }

        bool try_parse_next(std::span<const uint8_t>& frame_data)
        {
            auto buffer_span = _frame_buffer.span();
            auto frame_span = _frame_buffer.find(magic);
            if (frame_span.size() <= magic.size() + sizeof(len_t))
                return false;

            const auto frame_offset = static_cast<std::size_t>(frame_span.data() - buffer_span.data());
            frame_span = frame_span.subspan(magic.size());
            etl::byte_stream_reader reader(static_cast<const void*>(frame_span.data()), frame_span.size(), etl::endian::big);

            auto size = reader.read<len_t>();
            if (!size)
                return false;

            if (*size > _frame_buffer.capacity())
            {
                _frame_buffer.seek(frame_offset + magic.size());
                return false;
            }

            if (*size > reader.available_bytes())
                return false;

            auto data = reader.read<uint8_t>(*size);
            if (!data)
                return false;

            frame_data = {data->data(), data->size()};
            _frame_buffer.seek(frame_offset + magic.size() + reader.used_data().size());
            return true;
        }

        static std::size_t get_frame_size(std::span<const uint8_t> data)
        {
            return magic.size() + sizeof(len_t) + data.size();
        }

        static constexpr std::size_t max_frame_size()
        {
            return magic.size() + sizeof(len_t) + std::numeric_limits<len_t>::max();
        }

    private:
        frame_buffer_t<BufferSize> _frame_buffer{};
    };

    struct frame_builder_t
    {
        frame_builder_t(std::span<const uint8_t>& data) : _data(data), _field(field_t::magic) {}

        bool next(std::span<const uint8_t>& result)
        {
            switch (_field)
            {
                case field_t::magic:
                    _field = field_t::size;
                    result = std::span{framer_t<0>::magic};
                    return true;
                case field_t::size:
                {
                    _field = field_t::data;
                    etl::unaligned_type<framer_t<0>::len_t, etl::endian::big> len(static_cast<framer_t<0>::len_t>(_data.size()));
                    std::ranges::copy(len, _buffer.begin());
                    result = std::span{_buffer.begin(), len.size()};
                    return true;
                }
                case field_t::data:
                    _field = field_t::end;
                    result = _data;
                    return true;
                default:
                    return false;
            }
        }

    private:
        enum class field_t : uint8_t
        {
            magic,
            size,
            data,
            end
        };

        std::span<const uint8_t>& _data;
        std::array<uint8_t, sizeof(framer_t<0>::len_t)> _buffer{};
        field_t _field;
    };
}
