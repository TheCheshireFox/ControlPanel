#pragma once

#include <string>
#include <vector>
#include <span>
#include <variant>
#include <optional>

#include "esp_log.h"
#include "sdkconfig.h"

#define ARDUINOJSON_AUTO_SHRINK 0

#include "ArduinoJson.h"
#include "arduino_json_utils.hpp"

enum bridge_message_type_t : int8_t
{
    none = -1,
    streams,
    set_volume,
    set_mute,
    icon,
    get_icon,
    request_refresh
};

template<bridge_message_type_t Type>
struct bridge_message_base_t
{
    bridge_message_type_t type = Type;
};

struct bridge_audio_stream_id_t
{
    std::string id;
    std::string agent_id;
};

struct name_sprite_t
{
    std::string name;
    std::span<const uint8_t> sprite; // huge
    int width;
    int height;
};

struct bridge_audio_stream_t
{
    bridge_audio_stream_id_t id;
    std::string source;
    std::optional<name_sprite_t> name;
    std::optional<bool> mute;
    std::optional<float> volume;
};

struct streams_message_t : public bridge_message_base_t<bridge_message_type_t::streams>
{
    std::vector<bridge_audio_stream_t> updated;
    std::vector<bridge_audio_stream_id_t> deleted;
};
struct icon_message_t : public bridge_message_base_t<bridge_message_type_t::icon>
{
    std::string source;
    std::string agent_id;
    std::span<const uint8_t> icon;
};

struct set_mute_message_t : public bridge_message_base_t<bridge_message_type_t::set_mute>
{
    bridge_audio_stream_id_t id;
    bool mute;
};

struct set_volume_message_t : public bridge_message_base_t<bridge_message_type_t::set_volume>
{
    bridge_audio_stream_id_t id;
    float volume;
};

struct get_icon_message_t : public bridge_message_base_t<bridge_message_type_t::get_icon>
{
    std::string source;
    std::string agent_id;
};

struct request_refresh_message_t : public bridge_message_base_t<bridge_message_type_t::request_refresh>
{

};

SIMPLE_CONVERT_TO_JSON(bridge_audio_stream_id_t, id, agent_id);
SIMPLE_CONVERT_TO_JSON(request_refresh_message_t, type);
SIMPLE_CONVERT_TO_JSON(get_icon_message_t, type, source, agent_id);
SIMPLE_CONVERT_TO_JSON(set_volume_message_t, id, type, volume);
SIMPLE_CONVERT_TO_JSON(set_mute_message_t, id, type, mute);

SIMPLE_CONVERT_FROM_JSON(bridge_audio_stream_id_t, id, agent_id);
SIMPLE_CONVERT_FROM_JSON(name_sprite_t, name, sprite, width, height);
SIMPLE_CONVERT_FROM_JSON(bridge_audio_stream_t, id, source, name, mute, volume);
SIMPLE_CONVERT_FROM_JSON(streams_message_t, type, updated, deleted);
SIMPLE_CONVERT_FROM_JSON(icon_message_t, type, icon, source, agent_id);

namespace protocol::details
{
    inline static constexpr char TAG[] = "MSGPACK";
}

using bridge_message_t = std::variant<std::monostate, streams_message_t, icon_message_t>;

inline bridge_message_t parse_bridge_message(std::span<const uint8_t> msg_data)
{
    static JsonDocument doc;

    deserializeMsgPack(doc, msg_data.data(), msg_data.size());
    
    auto type = doc["type"].as<bridge_message_type_t>();
    switch (type)
    {
        case bridge_message_type_t::streams:
            return doc.as<streams_message_t>();
        case bridge_message_type_t::icon:
            return doc.as<icon_message_t>();
        default:
            ESP_LOGE(protocol::details::TAG, "Unsupported deserilize type: %d", type);
            return {};
    }
}

template<typename T>
inline std::span<uint8_t> serialize_bridge_message(const T& message)
{
    static dynamic_writer_t writer = {};

    writer.clear();

    JsonDocument doc;
    doc.set(message);

    auto sz = serializeMsgPack(doc, writer);
    
    if (!sz)
    {
        ESP_LOGE(protocol::details::TAG, "%s", "serialization failed");
        return {writer.data(), 0};
    }

    ESP_LOGD(protocol::details::TAG, "serialized to sz=%d", sz);

    return {writer.data(), sz};
}