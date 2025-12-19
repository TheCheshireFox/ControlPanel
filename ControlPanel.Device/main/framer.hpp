#pragma once

#include <byteswap.h>
#include <string.h>
#include <vector>
#include <span>
#include <ranges>
#include <algorithm>

#include "esp_log.h"

namespace framer::details
{
    enum class state_t
    {
        magic,
        seq,
        type,
        len,
        data,
        crc16
    };

    inline uint16_t crc16_ccitt(const auto& data, uint16_t initial = 0xFFFF)
    {
        const uint16_t polynomial = 0x1021; 

        auto crc = initial;

        for (int i = 0; i < data.size(); i++)
        {
            auto b = data[i];
            crc ^= (uint16_t)(b << 8);

            for (auto j = 0; j < 8; j++)
            {
                crc = (crc & 0x8000) != 0
                    ? (uint16_t)((crc << 1) ^ polynomial)
                    : (uint16_t)(crc << 1);
            }
        }

        return crc;
    }

    template<std::size_t Size>
    struct fixed_buffer_t
    {
        fixed_buffer_t() {}
        
        fixed_buffer_t(const uint8_t (&buf)[Size])
        {
            std::copy_n(buf, Size, _buf.begin());
            _pos = Size;
        }

        void push(uint8_t v)
        {
            if (_pos < Size) _buf[_pos++] = v;
        }

        void clear()
        {
            _pos = 0;
        }

        void erase_before_last(uint8_t v)
        {
            if (_pos == 0)
                return;

            for (auto i = _pos; i-- > 0; )
            {
                if (_buf[i] == v)
                {
                    const std::size_t keep = _pos - i;
                    std::memmove(_buf.data(), _buf.data() + i, keep);
                    _pos = keep;
                    return;
                }
            }

            clear();
        }

        uint8_t operator[](std::size_t n) const { return _buf[n]; }

        uint8_t* data() { return _buf.data(); }
        const uint8_t* data() const { return _buf.data(); }
        
        std::size_t size() const { return _pos; }
        
        bool empty() const { return _pos == 0; }
        
        consteval std::size_t capacity() const { return Size; }
    
    private:
        std::size_t _pos = 0;
        std::array<uint8_t, Size> _buf;
    };
}

enum frame_type_t : uint8_t
{
    Data = 0,
    ACK = 1
};

struct frame_t
{
    uint16_t seq;
    frame_type_t type;
    std::span<uint8_t> data;
};

template<std::size_t MagicSize>
class uart_framer_t {
    static constexpr char TAG[] = "CST328";

    using state_t = framer::details::state_t;
    template<std::size_t Size> using fixed_buffer_t = framer::details::fixed_buffer_t<Size>;

public:

    using seq_t = uint16_t;
    using type_t = frame_type_t;
    using len_t = uint16_t;

    uart_framer_t(const uint8_t(&magic)[MagicSize], std::size_t max_frame_size)
        : _capacity(MagicSize + sizeof(seq_t) + sizeof(type_t) + sizeof(len_t) + sizeof(uint16_t) + max_frame_size)
        , _max_frame_size(max_frame_size)
        , _magic(magic)
    {
        _data_buf.reserve(max_frame_size);
    }

    void to_bytes(std::vector<uint8_t>& buffer, const frame_t& frame)
    {
        auto insert_num = [&](auto val)
        {
            static_assert(sizeof(val) == 1 || sizeof(val) == 2);

            if (sizeof(val) == 1)
            {
                buffer.emplace_back((uint8_t)val);
            }
            else if (sizeof(val) == 2)
            {
                uint16_t sw = __bswap_16(val);
                buffer.insert(buffer.end(), (uint8_t*)&sw, (uint8_t*)&sw + sizeof(sw));
            }
        };

        ESP_LOGD(TAG, "frame to bytes seq=%d type=%d size=%d", frame.seq, (uint8_t)frame.type, frame.data.size());

        buffer.clear();
        buffer.insert(buffer.end(), _magic.data(), _magic.data() + _magic.size());
        insert_num((uint16_t)frame.seq);
        insert_num((uint8_t)frame.type);
        insert_num((uint16_t)frame.data.size());
        buffer.insert(buffer.end(), frame.data.begin(), frame.data.end());
        insert_num(framer::details::crc16_ccitt(buffer));
    }

    uint16_t calc_frame_size(uint16_t data_size) const
    {
        return _magic.size() + sizeof(seq_t) + sizeof(type_t) + sizeof(len_t) + sizeof(uint16_t) + data_size;
    }

    template <class F>
    void feed(const uint8_t* data, size_t n, F&& on_frame)
    {
        if (n == 0 || n > _capacity)
            return;

        for (int i = 0; i < n; i++)
        {
            switch (_state)
            {
                case state_t::magic:
                    read_magic(data[i]);
                    break;

                case state_t::seq:
                    read_value(data[i], _seq_buf, _seq, state_t::type);
                    break;

                case state_t::type:
                    read_value(data[i], _type_buf, _frame_type, state_t::len);
                    break;
                
                case state_t::len:
                    read_value(data[i], _len_buf, _len, state_t::data,
                        [&](auto v){ return _frame_type == frame_type_t::ACK || (v > 0 && v < _max_frame_size); });
                    break;
                
                case state_t::data:
                    i += read_data(data, n, i) - 1;
                    break;

                case state_t::crc16:
                    if (read_value(data[i], _crc16_buf, _crc16, state_t::magic))
                    {
                        auto crc = framer::details::crc16_ccitt(_magic_buf);
                        crc = framer::details::crc16_ccitt(_seq_buf, crc);
                        crc = framer::details::crc16_ccitt(_type_buf, crc);
                        crc = framer::details::crc16_ccitt(_len_buf, crc);
                        crc = framer::details::crc16_ccitt(_data_buf, crc);

                        if (crc != _crc16)
                        {
                            ESP_LOGE(TAG, "bad crc16 %d != %d", _crc16, crc);
                            break;
                        }

                        on_frame(frame_t{_seq, _frame_type, std::span<uint8_t>{_data_buf.data(), _data_buf.data() + _len}});
                        reset();
                    }
                    break;
            }
        }
    }

    void read_magic(uint8_t b)
    {
        if (b == _magic[0])
        {
            reset();
            _magic_buf.push(b);
        }
        else if (!_magic_buf.empty())
        {
            _magic_buf.push(b);
            if (_magic_buf.size() == _magic.size())
            {
                if (std::memcmp(_magic_buf.data(), _magic.data(), _magic.size()) == 0)
                {
                    ESP_LOGD(TAG, "%s", "frame start detected");
                    _state = state_t::seq;
                }
                else
                {
                    ESP_LOGW(TAG, "%s", "bad magic");
                    _state = state_t::magic;
                    clear_magic();
                }
            }
        }
    }

    template<typename T, typename Pred = std::nullptr_t>
    bool read_value(uint8_t b, auto& buf, T& result, state_t next_state, Pred&& validate = nullptr)
    {
        buf.push(b);
        
        if (buf.size() < buf.capacity())
            return false;
        
        result = from_be<T>(buf);

        bool valid = true;
        if constexpr (!std::is_same_v<std::decay_t<Pred>, std::nullptr_t>) {
            valid = validate(result);
        }

        _state = valid ? next_state : state_t::magic;

        return valid;
    }

    int read_data(const uint8_t* data, size_t n, int i)
    {
        auto need = (std::size_t)_len - _data_buf.size();
        auto to_copy = std::min(n - i, need);
        _data_buf.insert(_data_buf.end(), &data[i], &data[i + to_copy]);

        if (_data_buf.size() == _len)
            _state = state_t::crc16;
        
        return to_copy;
    }

    void clear_magic()
    {
        _magic_buf.erase_before_last(_magic[0]);
    }

    void reset()
    {
        _magic_buf.clear();
        _seq_buf.clear();
        _type_buf.clear();
        _len_buf.clear();
        _data_buf.clear();
        _crc16_buf.clear();
        
        _state = state_t::magic;
        _seq = 0;
        _frame_type = frame_type_t::Data;
        _len = 0;
        _crc16 = 0;
    }

private:
    template<class T> struct dependent_false : std::false_type {};
    template<typename T> requires (std::is_integral_v<T> || std::is_enum_v<T>)
    inline static constexpr T from_be(const auto& bytes)
    {
        if (bytes.size() != sizeof(T))
        {
            ESP_LOGE(TAG, "len buffer wrong size=%d len_t size=%d", bytes.size(), sizeof(len_t));
            return {};
        }

        if constexpr (sizeof(T) == 1)
        {
            return (T)bytes[0];
        }
        else if constexpr (sizeof(T) == 2)
        {
            return (T)((uint16_t)bytes[0] << 8 | bytes[1]);
        }
        else
        {
            static_assert(dependent_false<T>::value, "T not implemented");
        }
    }

    const uint32_t _capacity;
    const std::size_t _max_frame_size;
    const fixed_buffer_t<MagicSize> _magic;

    fixed_buffer_t<MagicSize> _magic_buf = {};
    fixed_buffer_t<sizeof(seq_t)> _seq_buf = {};
    fixed_buffer_t<sizeof(frame_type_t)> _type_buf = {};
    fixed_buffer_t<sizeof(len_t)> _len_buf = {};
    std::vector<uint8_t> _data_buf = {};
    fixed_buffer_t<sizeof(uint16_t)> _crc16_buf = {};
    
    state_t _state = state_t::magic;
    seq_t _seq = 0;
    frame_type_t _frame_type = frame_type_t::Data;
    len_t _len = 0;
    uint16_t _crc16 = 0;
};