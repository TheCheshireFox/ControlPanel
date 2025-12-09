#pragma once

#include <string>
#include <vector>
#include <variant>

#include "esp_log.h"
#include "sdkconfig.h"

#include "nlohmann/json.hpp"

#include "base64.hpp"

enum bridge_message_type_t : int8_t
{
    none = -1,
    streams,
    set_volume,
    set_mute,
    icon,
    get_icon
};

template<bridge_message_type_t Type>
struct bridge_message_base_t
{
    bridge_message_type_t type = Type;
};

struct bridge_audio_stream_t
{
    std::string agent_id;
    std::string id;
    std::string name;
    bool mute;
    float volume;
};

struct streams_message_t : public bridge_message_base_t<bridge_message_type_t::streams>
{
    std::vector<bridge_audio_stream_t> streams;
};

struct icon_message_t : public bridge_message_base_t<bridge_message_type_t::get_icon>
{
    std::string id;
    std::string agent_id;
    std::vector<uint8_t> rgb565a8;
};

struct set_mute_message_t : public bridge_message_base_t<bridge_message_type_t::set_mute>
{
    std::string id;
    std::string agent_id;
    bool mute;
};

struct set_volume_message_t : public bridge_message_base_t<bridge_message_type_t::set_volume>
{
    std::string id;
    std::string agent_id;
    float volume;
};

struct get_icon_message_t : public bridge_message_base_t<bridge_message_type_t::get_icon>
{
    std::string id;
    std::string agent_id;
};

using bridge_message_t = std::variant<std::monostate, streams_message_t, icon_message_t>;

inline void from_json(const nlohmann::json& j, bridge_audio_stream_t& p) {
    p.agent_id = j.value("agent_id", "");
    p.id = j.value("id", "");
    p.name = j.value("name", "");
    p.mute = j.value("mute", false);
    p.volume = j.value("volume", 0.0f);
}

inline void from_json(const nlohmann::json& j, icon_message_t& p) {
    p.agent_id = j.value("agent_id", "");
    p.id = j.value("id", "");
    p.rgb565a8 = base64_decode(j.value("rgb565_a8_icon", "")); // hello from System.Text.Json snake case handling
}

inline void from_json(const nlohmann::json& j, streams_message_t& p) {
    p.streams = j.value<std::vector<bridge_audio_stream_t>>("streams", {});
}

inline void to_json(nlohmann::json& j, const set_mute_message_t& p) {
    j = nlohmann::json{{"type", p.type}, {"id", p.id}, {"agent_id", p.agent_id}, {"mute", p.mute}};
}

inline void to_json(nlohmann::json& j, const set_volume_message_t& p) {
    j = nlohmann::json{{"type", p.type}, {"id", p.id}, {"agent_id", p.agent_id}, {"volume", p.volume}};
}

inline void to_json(nlohmann::json& j, const get_icon_message_t& p) {
    j = nlohmann::json{{"id", p.id}, {"agent_id", p.agent_id}};
}

inline bridge_message_t parse_bridge_message(const std::string& str)
{
    auto data = nlohmann::json::parse(str);
    auto type = data.value("type", bridge_message_type_t::none);
    switch (type)
    {
        case bridge_message_type_t::none:
            return {};
        case bridge_message_type_t::streams:
            return data.get<streams_message_t>();
        case bridge_message_type_t::icon:
            return data.get<icon_message_t>();
        default:
            ESP_LOGE("JSON", "Unexpected message type %d", (uint8_t)type);
            return {};
    }
}

template<typename T>
inline std::string serialize_bridge_message(const T& message)
{
    nlohmann::json j = message; return j.dump();
}