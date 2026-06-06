#pragma once

#include <algorithm>
#include <cstdint>
#include <functional>
#include <map>
#include <optional>
#include <set>
#include <span>
#include <string>
#include <tuple>
#include <variant>
#include <vector>

#include "protocol/messages.hpp"

struct event_id
{
    std::string id;
    std::string agent_id;

    bool operator==(const event_id& other) const { return std::tuple{id, agent_id} == std::tuple{other.id, other.agent_id}; }
    bool operator<(const event_id& other) const { return std::tuple{id, agent_id} < std::tuple{other.id, other.agent_id}; }
};

template<typename R, typename T>
concept Iterable = std::ranges::input_range<R> && std::convertible_to<std::ranges::range_reference_t<R>, T>;

struct display_icon_t
{
    uint32_t w;
    uint32_t h;
    std::vector<uint8_t> data;
};

struct display_add_item_t
{
    event_id id;
    std::string source;
    std::string title;
    int8_t volume;
    bool mute;
    int32_t icon_hash;
};

struct display_update_item_t
{
    event_id id;
    std::optional<std::string> title;
    std::optional<int8_t> volume;
    std::optional<bool> mute;
};

struct display_remove_item_t
{
    event_id id;
};

struct display_set_item_icon_t
{
    event_id id;
    display_icon_t icon;
};

struct display_missing_icon_t
{
    std::string source;
    std::string agent_id;
    int32_t icon_hash;
};

using display_delta_t = std::variant<
    display_add_item_t,
    display_update_item_t,
    display_remove_item_t,
    display_set_item_icon_t,
    display_missing_icon_t>;

class display_icon_cache_t
{
    struct icon_key_t
    {
        std::string source;
        std::string agent_id;

        bool operator<(const icon_key_t& other) const { return std::tuple{source, agent_id} < std::tuple{other.source, other.agent_id}; }
    };

    struct cached_icon_t
    {
        int32_t icon_hash;
        display_icon_t icon;
        uint32_t last_access;
    };

public:
    std::optional<display_icon_t> get(const std::string& source, const std::string& agent_id, int32_t icon_hash)
    {
        auto it = _icons.find(icon_key_t{source, agent_id});
        if (it == _icons.end() || it->second.icon_hash != icon_hash)
            return std::nullopt;

        it->second.last_access = next_access();
        return it->second.icon;
    }

    display_icon_t put(const std::string& source, const std::string& agent_id, int32_t icon_hash, uint32_t w, uint32_t h, std::span<const uint8_t> data)
    {
        display_icon_t icon{w, h, std::vector<uint8_t>{data.begin(), data.end()}};
        _icons.insert_or_assign(
            icon_key_t{source, agent_id},
            cached_icon_t{icon_hash, icon, next_access()});
        trim();
        return icon;
    }

    std::size_t size() const
    {
        return _icons.size();
    }

private:
    void trim()
    {
        static constexpr std::size_t max_cached_icons = 16;

        if (_icons.size() <= max_cached_icons)
            return;

        auto it = std::ranges::min_element(_icons,
            [](const auto& lhs, const auto& rhs){ return lhs.second.last_access < rhs.second.last_access; });

        if (it != _icons.end())
            _icons.erase(it);
    }

    uint32_t next_access()
    {
        return ++_icon_access;
    }

private:
    std::map<icon_key_t, cached_icon_t> _icons;
    uint32_t _icon_access = 0;
};

class volume_display_model_t
{
    struct item_t
    {
        std::string source;
        int32_t icon_hash;
    };

public:
    std::vector<display_delta_t> refresh(Iterable<bridge_audio_stream_t> auto&& updated, Iterable<bridge_audio_stream_id_t> auto&& deleted)
    {
        std::vector<display_delta_t> deltas;
        std::set<std::tuple<std::string, std::string, int32_t>> missing_icons;

        remove_outdated(deleted, deltas);

        for (const auto& stream: updated)
            update_stream(stream, deltas, missing_icons);

        for (const auto& [source, agent_id, icon_hash]: missing_icons)
            deltas.emplace_back(display_missing_icon_t{source, agent_id, icon_hash});

        return deltas;
    }

    std::vector<display_delta_t> update_icon(const std::string& source, const std::string& agent_id, int32_t icon_hash, uint32_t w, uint32_t h, std::span<const uint8_t> data)
    {
        std::vector<display_delta_t> deltas;
        auto icon = _icon_cache.put(source, agent_id, icon_hash, w, h, data);

        for (const auto& [id, item]: _items)
        {
            if (id.agent_id != agent_id || item.source != source || item.icon_hash != icon_hash)
                continue;

            deltas.emplace_back(display_set_item_icon_t{id, icon});
        }

        return deltas;
    }

    std::size_t size() const
    {
        return _items.size();
    }

    static float to_volume_fraction(int8_t value)
    {
        return value / 100.0f;
    }

private:
    void remove_outdated(Iterable<bridge_audio_stream_id_t> auto&& deleted, std::vector<display_delta_t>& deltas)
    {
        for (const auto& stream_id: deleted)
        {
            event_id id{stream_id.id, stream_id.agent_id};
            auto it = _items.find(id);
            if (it == _items.end())
                continue;

            _items.erase(it);
            deltas.emplace_back(display_remove_item_t{id});
        }
    }

    void update_stream(const bridge_audio_stream_t& stream, std::vector<display_delta_t>& deltas, std::set<std::tuple<std::string, std::string, int32_t>>& missing_icons)
    {
        event_id id{stream.id.id, stream.id.agent_id};

        auto it = _items.find(id);
        if (it == _items.end())
        {
            add_stream(stream, id, deltas, missing_icons);
            return;
        }

        display_update_item_t update{id};
        if (stream.name.has_value()) update.title = stream.name;
        if (stream.mute.has_value()) update.mute = stream.mute;
        if (stream.volume.has_value()) update.volume = to_volume_percent(*stream.volume);

        if (update.title.has_value() || update.mute.has_value() || update.volume.has_value())
            deltas.emplace_back(update);

        if (stream.icon_hash.has_value() && it->second.icon_hash != stream.icon_hash)
        {
            it->second.icon_hash = *stream.icon_hash;
            set_cached_icon_or_request(id, it->second.source, *stream.icon_hash, deltas, missing_icons);
        }
    }

    void add_stream(const bridge_audio_stream_t& stream, const event_id& id, std::vector<display_delta_t>& deltas, std::set<std::tuple<std::string, std::string, int32_t>>& missing_icons)
    {
        if (!stream.name.has_value() || !stream.volume.has_value() || !stream.mute.has_value() || !stream.icon_hash.has_value())
            return;

        if (auto [_, inserted] = _items.emplace(id, item_t{stream.source, *stream.icon_hash}); !inserted)
            return;

        deltas.emplace_back(display_add_item_t{
            id,
            stream.source,
            *stream.name,
            to_volume_percent(*stream.volume),
            *stream.mute,
            *stream.icon_hash});
        set_cached_icon_or_request(id, stream.source, *stream.icon_hash, deltas, missing_icons);
    }

    void set_cached_icon_or_request(const event_id& id, const std::string& source, int32_t icon_hash, std::vector<display_delta_t>& deltas, std::set<std::tuple<std::string, std::string, int32_t>>& missing_icons)
    {
        if (auto icon = _icon_cache.get(source, id.agent_id, icon_hash))
        {
            deltas.emplace_back(display_set_item_icon_t{id, *icon});
            return;
        }

        missing_icons.emplace(source, id.agent_id, icon_hash);
    }

    static int8_t to_volume_percent(float volume)
    {
        return static_cast<int8_t>(volume * 100);
    }

private:
    std::map<event_id, item_t> _items;
    display_icon_cache_t _icon_cache;
};
