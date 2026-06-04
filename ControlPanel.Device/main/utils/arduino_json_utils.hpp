#pragma once

#include <vector>
#include <span>
#include <optional>

#include "ArduinoJson.h"

namespace ArduinoJson
{
    template<typename T>
    struct Converter<std::optional<T>>
    {
        static std::optional<T> fromJson(JsonVariantConst src)
        {
            return src.isNull() ? std::optional<T>{} : src.as<T>();
        }
    };

    template<typename T>
    struct Converter<std::vector<T>>
    {
        static std::vector<T> fromJson(JsonVariantConst src)
        {
            auto array = src.as<JsonArrayConst>();
            std::vector<T> result;
            result.reserve(array.size());

            for (const auto& value: array)
            {
                result.emplace_back(value.as<T>());
            }

            return result;
        }
    };

    template<>
    struct Converter<std::span<const uint8_t>>
    {
        static std::span<const uint8_t> fromJson(JsonVariantConst src)
        {
            assert(src.is<MsgPackBinary>());

            auto bin = src.as<MsgPackBinary>();
            return std::span{static_cast<const uint8_t*>(bin.data()), bin.size()};
        }
    };
}

class dynamic_writer_t
{
public:
    size_t write(uint8_t c)
    {
        _buffer.emplace_back(c);
        return 1;
    }

    size_t write(const uint8_t* s, size_t n)
    {
        _buffer.insert(_buffer.end(), s, s + n);
        return n;
    }

    uint8_t* data()
    {
        return _buffer.data();
    }

    void clear()
    {
        _buffer.clear();
    }

private:
    std::vector<uint8_t> _buffer = {};
};
