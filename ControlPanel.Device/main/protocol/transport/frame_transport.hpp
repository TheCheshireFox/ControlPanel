#pragma once

#include <stdint.h>
#include <functional>
#include <span>

namespace transport
{
    template<typename T>
    concept frame_transport_t = requires(T t)
    {
        { t.write(std::span<uint8_t>()) } -> std::same_as<void>;
        { t.on_receive(std::function<void(std::span<uint8_t>)>()) } -> std::same_as<void>;
    };
}