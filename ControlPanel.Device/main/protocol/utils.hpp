#pragma once

#include <stdint.h>
#include <cstring>
#include <span>

namespace transport
{
    inline std::ptrdiff_t find_sequence(std::span<const uint8_t> span, std::span<const uint8_t> seq)
    {
        if (span.size() < seq.size())
            return -1;

        for (std::size_t i = 0; i <= span.size() - seq.size(); i++)
        {
            if (span[i] != seq[0])
                continue;
            
            if (std::memcmp(&span[i], seq.data(), seq.size()) == 0)
                return i;
        }

        return -1;
    }
}