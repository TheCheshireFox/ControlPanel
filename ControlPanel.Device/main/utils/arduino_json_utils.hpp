#pragma once

#include <vector>
#include <span>
#include <optional>

#include "ArduinoJson.h"

#define PARENS ()
#define EXPAND(...) EXPAND_(EXPAND_(EXPAND_(EXPAND_(EXPAND_(EXPAND_(EXPAND_(EXPAND_(EXPAND_(__VA_ARGS__)))))))))
#define EXPAND_(...) __VA_ARGS__
#define FOR_EACH(macro, ...) __VA_OPT__(EXPAND(FOR_EACH_HELPER(macro, __VA_ARGS__)))
#define FOR_EACH_HELPER(macro, a1, ...) macro(a1) __VA_OPT__(FOR_EACH_AGAIN PARENS (macro, __VA_ARGS__))
#define FOR_EACH_AGAIN() FOR_EACH_HELPER

#define _SCTJ_ASSIGN(m) dst[#m] = src.m;
#define SIMPLE_CONVERT_TO_JSON(type, ...) void convertToJson(const type& src, JsonVariant dst)\
{\
    FOR_EACH(_SCTJ_ASSIGN, __VA_ARGS__)\
}

#define _SCFJ_ASSIGN(m) dst.m = src[#m].as<decltype(dst.m)>();
#define SIMPLE_CONVERT_FROM_JSON(type, ...) void convertFromJson(JsonVariantConst src, type& dst) \
{\
    FOR_EACH(_SCFJ_ASSIGN, __VA_ARGS__)\
}\

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

    template<>
    struct Converter<std::span<const uint8_t>>
    {
        static std::span<const uint8_t> fromJson(JsonVariantConst src)
        {
            auto bin = src.as<MsgPackBinary>();
            return std::span<const uint8_t>{(uint8_t*)bin.data(), bin.size()};
        }
    };
}

template<typename T>
void convertFromJson(JsonVariantConst src, std::vector<T>& dst)
{
    auto array = src.as<JsonArrayConst>();
    dst.reserve(array.size());

    for (const auto& v: array)
    {
        dst.emplace_back(v.as<T>());
    }
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