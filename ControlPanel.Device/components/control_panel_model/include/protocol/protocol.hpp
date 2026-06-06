#pragma once

#include "protocol/messages.hpp"

#include <type_traits>

#include "esp_log.h"
#include "sdkconfig.h"

#define ARDUINOJSON_AUTO_SHRINK 0

#include "ArduinoJson.h"
#include "utils/arduino_json_utils.hpp"

inline bool convertToJson(const bridge_message_type_t& type, JsonVariant dst) { return dst.set(static_cast<int8_t>(type)); }
inline void convertFromJson(JsonVariantConst src, bridge_message_type_t& type) { type = static_cast<bridge_message_type_t>(src.as<int8_t>()); }

template<typename T>
concept has_model_traits = requires { model_traits<T>::fields(); };

template<has_model_traits T>
void convertToJson(const T& src, JsonVariant dst)
{
    if constexpr (requires { src.type; })
        dst["type"] = src.type;

    std::apply([&](auto... field)
    {
        ((dst[field.name] = src.*field.member), ...);
    }, model_traits<T>::fields());
}

template<has_model_traits T>
void convertFromJson(JsonVariantConst src, T& dst)
{
    if constexpr (requires { dst.type; })
        dst.type = src["type"].as<decltype(dst.type)>();

    std::apply([&](auto... field)
    {
        ((dst.*(field.member) = src[field.name].template as<std::remove_cvref_t<decltype(dst.*field.member)>>()), ...);
    }, model_traits<T>::fields());
}

namespace protocol
{
    inline static constexpr char TAG[] = "MsgPack";
}

inline bridge_message_t parse_bridge_message(std::span<const uint8_t> msg_data)
{
    static JsonDocument doc;

    if (auto err = deserializeMsgPack(doc, msg_data.data(), msg_data.size()); err != DeserializationError::Ok)
    {
        ESP_LOGE(protocol::TAG, "Deserialization error: %d", err);
        return {};
    }

    switch (auto type = doc["type"].as<bridge_message_type_t>())
    {
        case bridge_message_type_t::streams:
            return doc.as<streams_message_t>();
        case bridge_message_type_t::icon:
            return doc.as<icon_message_t>();
        default:
            ESP_LOGE(protocol::TAG, "Unsupported deserilize type: %d", type);
            return {};
    }
}

template<typename T>
std::span<uint8_t> serialize_bridge_message(const T& message)
{
    static dynamic_writer_t writer = {};

    writer.clear();

    JsonDocument doc;
    doc.set(message);

    auto sz = serializeMsgPack(doc, writer);
    
    if (!sz)
    {
        ESP_LOGE(protocol::TAG, "%s", "serialization failed");
        return {writer.data(), 0};
    }

    ESP_LOGD(protocol::TAG, "serialized to sz=%d", sz);

    return {writer.data(), sz};
}