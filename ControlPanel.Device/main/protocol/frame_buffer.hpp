#pragma once

#include <array>
#include <span>

namespace transport
{
    template<std::size_t Size>
    struct frame_buffer_t
    {
        bool try_insert(std::span<const uint8_t> data)
        {
            if (data.size() > _buffer.size() - _w_pos)
                return false;
            
            std::memcpy(&_buffer[_w_pos], data.data(), data.size());
            _w_pos += data.size();

            return true;
        }

        void shift_left_from(std::span<const uint8_t> mark)
        {
            auto pos = find_sequence(span(), mark);
            if (pos == -1)
            {
                clear();
                return;
            }

            const auto mark_pos = pos + _r_pos;
            const auto remaining = _w_pos - mark_pos;
            std::memmove(_buffer.data(), &_buffer[mark_pos], remaining);
            _r_pos = 0;
            _w_pos = remaining;
        }

        void seek(std::size_t offset)
        {
            configASSERT(_r_pos + offset <= _w_pos);
            _r_pos += offset;
        }

        void clear()
        {
            _r_pos = 0;
            _w_pos = 0;
        }

        std::size_t size() const
        {
            return _w_pos - _r_pos;
        }

        constexpr std::size_t capacity() const
        {
            return Size;
        }

        std::span<uint8_t> span()
        {
            return std::span<uint8_t>{_buffer.data() + _r_pos, size()};
        }

    private:
        std::size_t _r_pos = 0;
        std::size_t _w_pos = 0;
        std::array<uint8_t, Size> _buffer;
    };
}