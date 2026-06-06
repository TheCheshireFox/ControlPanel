#pragma once

#include <cstdint>
#include <optional>
#include <span>
#include <string>
#include <tuple>
#include <variant>
#include <vector>

template<typename T, typename M>
struct model_field_t
{
    const char* name;
    M T::* member;
};

template<typename T>
struct model_traits;

#define MODEL_TRAITS_PARENS ()
#define MODEL_TRAITS_EXPAND(...) MODEL_TRAITS_EXPAND_(MODEL_TRAITS_EXPAND_(MODEL_TRAITS_EXPAND_(MODEL_TRAITS_EXPAND_(__VA_ARGS__))))
#define MODEL_TRAITS_EXPAND_(...) __VA_ARGS__
#define MODEL_TRAITS_FIELD(type, field) model_field_t<type, decltype(type::field)>{#field, &type::field}
#define MODEL_TRAITS_FIELDS(type, field, ...) MODEL_TRAITS_FIELD(type, field) __VA_OPT__(, MODEL_TRAITS_FIELDS_AGAIN MODEL_TRAITS_PARENS (type, __VA_ARGS__))
#define MODEL_TRAITS_FIELDS_AGAIN() MODEL_TRAITS_FIELDS
#define MODEL_TRAITS(type, ...) \
    template<> \
    struct model_traits<type> \
    { \
        static constexpr auto fields() \
        { \
            return std::tuple{__VA_OPT__(MODEL_TRAITS_EXPAND(MODEL_TRAITS_FIELDS(type, __VA_ARGS__)))}; \
        } \
    }

enum class bridge_message_type_t : int8_t
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
MODEL_TRAITS(bridge_audio_stream_id_t, id, agent_id);

struct bridge_audio_stream_t
{
    bridge_audio_stream_id_t id;
    std::string source;
    std::optional<std::string> name;
    std::optional<bool> mute;
    std::optional<float> volume;
    std::optional<int32_t> icon_hash;
};
MODEL_TRAITS(bridge_audio_stream_t, id, source, name, mute, volume, icon_hash);

struct streams_message_t : bridge_message_base_t<bridge_message_type_t::streams>
{
    std::vector<bridge_audio_stream_t> updated;
    std::vector<bridge_audio_stream_id_t> deleted;
};
MODEL_TRAITS(streams_message_t, updated, deleted);

using icon_bytes_view_t = std::span<const uint8_t>;

struct icon_message_t : bridge_message_base_t<bridge_message_type_t::icon>
{
    std::string source;
    std::string agent_id;
    int32_t icon_hash;
    int size;
    icon_bytes_view_t icon;
};
MODEL_TRAITS(icon_message_t, source, agent_id, icon_hash, size, icon);

struct set_mute_message_t : bridge_message_base_t<bridge_message_type_t::set_mute>
{
    bridge_audio_stream_id_t id;
    bool mute;
};
MODEL_TRAITS(set_mute_message_t, id, mute);

struct set_volume_message_t : bridge_message_base_t<bridge_message_type_t::set_volume>
{
    bridge_audio_stream_id_t id;
    float volume;
};
MODEL_TRAITS(set_volume_message_t, id, volume);

struct get_icon_message_t : bridge_message_base_t<bridge_message_type_t::get_icon>
{
    std::string source;
    std::string agent_id;
    int32_t icon_hash;
};
MODEL_TRAITS(get_icon_message_t, source, agent_id, icon_hash);

struct request_refresh_message_t : bridge_message_base_t<bridge_message_type_t::request_refresh>
{

};
MODEL_TRAITS(request_refresh_message_t);

using bridge_message_t = std::variant<std::monostate, streams_message_t, icon_message_t>;
