#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <span>
#include <string>
#include <vector>

#define UNITY_SUPPORT_TEST_CASES

#include "unity.h"
#include "volume_display_model.hpp"

template<typename T>
const T& get_delta(const std::vector<display_delta_t>& deltas, std::size_t index)
{
    TEST_ASSERT_LESS_THAN(index, deltas.size());

    auto delta = std::get_if<T>(&deltas[index]);
    TEST_ASSERT_NOT_NULL(delta);

    return *delta;
}

bridge_audio_stream_id_t make_stream_id(std::string id = "stream-1", std::string agent_id = "agent-1")
{
    return bridge_audio_stream_id_t{
        .id = std::move(id),
        .agent_id = std::move(agent_id)
    };
}

bridge_audio_stream_t make_stream(
    std::string id = "stream-1",
    std::string agent_id = "agent-1",
    std::string source = "app.exe",
    std::string name = "App",
    float volume = 0.42f,
    bool mute = false,
    int32_t icon_hash = 100)
{
    return bridge_audio_stream_t{
        .id = make_stream_id(std::move(id), std::move(agent_id)),
        .source = std::move(source),
        .name = std::move(name),
        .mute = mute,
        .volume = volume,
        .icon_hash = icon_hash
    };
}

TEST_CASE("add_complete_stream_emits_item_and_missing_icon", "[volume_display]")
{
    volume_display_model_t model;

    auto deltas = model.refresh(std::vector{make_stream()}, std::vector<bridge_audio_stream_id_t>{});

    TEST_ASSERT_EQUAL(std::size_t{2}, deltas.size());
    TEST_ASSERT_EQUAL(std::size_t{1}, model.size());

    const auto& add = get_delta<display_add_item_t>(deltas, 0);
    TEST_ASSERT_EQUAL_STRING("stream-1", add.id.id.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-1", add.id.agent_id.c_str());
    TEST_ASSERT_EQUAL_STRING("app.exe", add.source.c_str());
    TEST_ASSERT_EQUAL_STRING("App", add.title.c_str());
    TEST_ASSERT_EQUAL(42, add.volume);
    TEST_ASSERT_EQUAL(false, add.mute);
    TEST_ASSERT_EQUAL(100, add.icon_hash);

    const auto& missing_icon = get_delta<display_missing_icon_t>(deltas, 1);
    TEST_ASSERT_EQUAL_STRING("app.exe", missing_icon.source.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-1", missing_icon.agent_id.c_str());
    TEST_ASSERT_EQUAL(100, missing_icon.icon_hash);
}

TEST_CASE("incomplete_new_stream_is_ignored", "[volume_display]")
{
    volume_display_model_t model;
    bridge_audio_stream_t incomplete{
        .id = make_stream_id(),
        .source = "app.exe",
        .name = "App",
        .mute = true,
        .volume = std::nullopt,
        .icon_hash = std::nullopt
    };

    auto deltas = model.refresh(std::vector{incomplete}, std::vector<bridge_audio_stream_id_t>{});

    TEST_ASSERT_TRUE(deltas.empty());
    TEST_ASSERT_EQUAL(0, model.size());
}

TEST_CASE("update_existing_stream_emits_only_present_fields", "[volume_display]")
{
    volume_display_model_t model;
    model.refresh(std::vector{make_stream()}, std::vector<bridge_audio_stream_id_t>{});

    bridge_audio_stream_t update{
        .id = make_stream_id(),
        .source = "ignored-new-source",
        .name = std::nullopt,
        .mute = true,
        .volume = 0.7f,
        .icon_hash = std::nullopt
    };

    auto deltas = model.refresh(std::vector{update}, std::vector<bridge_audio_stream_id_t>{});

    TEST_ASSERT_EQUAL(1, deltas.size());

    const auto& updated = get_delta<display_update_item_t>(deltas, 0);
    TEST_ASSERT_EQUAL_STRING("stream-1", updated.id.id.c_str());
    TEST_ASSERT_FALSE(updated.title.has_value());
    TEST_ASSERT_TRUE(updated.mute.has_value());
    TEST_ASSERT_TRUE(*updated.mute);
    TEST_ASSERT_TRUE(updated.volume.has_value());
    TEST_ASSERT_EQUAL(70, *updated.volume);
}

TEST_CASE("deleted_stream_emits_remove_once", "[volume_display]")
{
    volume_display_model_t model;
    model.refresh(std::vector{make_stream()}, std::vector<bridge_audio_stream_id_t>{});

    auto deltas = model.refresh(
        std::vector<bridge_audio_stream_t>{},
        std::vector{make_stream_id(), make_stream_id("missing", "agent-1")});

    TEST_ASSERT_EQUAL(1, deltas.size());
    TEST_ASSERT_EQUAL(0, model.size());

    const auto& remove = get_delta<display_remove_item_t>(deltas, 0);
    TEST_ASSERT_EQUAL_STRING("stream-1", remove.id.id.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-1", remove.id.agent_id.c_str());
}

TEST_CASE("missing_icon_requests_are_deduplicated_per_refresh", "[volume_display]")
{
    volume_display_model_t model;
    auto first = make_stream("stream-1", "agent-1", "app.exe", "App 1", 0.1f, false, 100);
    auto second = make_stream("stream-2", "agent-1", "app.exe", "App 2", 0.2f, false, 100);

    auto deltas = model.refresh(std::vector{first, second}, std::vector<bridge_audio_stream_id_t>{});

    TEST_ASSERT_EQUAL(3, deltas.size());
    get_delta<display_add_item_t>(deltas, 0);
    get_delta<display_add_item_t>(deltas, 1);

    const auto& missing_icon = get_delta<display_missing_icon_t>(deltas, 2);
    TEST_ASSERT_EQUAL_STRING("app.exe", missing_icon.source.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-1", missing_icon.agent_id.c_str());
    TEST_ASSERT_EQUAL(100, missing_icon.icon_hash);
}

TEST_CASE("update_icon_applies_to_matching_items_and_reuses_cache", "[volume_display]")
{
    volume_display_model_t model;
    model.refresh(std::vector{make_stream()}, std::vector<bridge_audio_stream_id_t>{});

    const std::vector<uint8_t> icon{1, 2, 3, 4};
    auto icon_deltas = model.update_icon("app.exe", "agent-1", 100, 2, 2, icon);

    TEST_ASSERT_EQUAL(1, icon_deltas.size());

    const auto& set_icon = get_delta<display_set_item_icon_t>(icon_deltas, 0);
    TEST_ASSERT_EQUAL_STRING("stream-1", set_icon.id.id.c_str());
    TEST_ASSERT_EQUAL(2, set_icon.icon.w);
    TEST_ASSERT_EQUAL(2, set_icon.icon.h);
    TEST_ASSERT_EQUAL(icon.size(), set_icon.icon.data.size());
    TEST_ASSERT_EQUAL_INT_ARRAY(icon.data(), set_icon.icon.data.data(), icon.size());

    auto cached_deltas = model.refresh(
        std::vector{make_stream("stream-2", "agent-1", "app.exe", "App 2", 0.2f, false, 100)},
        std::vector<bridge_audio_stream_id_t>{});

    TEST_ASSERT_EQUAL(2, cached_deltas.size());
    get_delta<display_add_item_t>(cached_deltas, 0);

    const auto& cached_icon = get_delta<display_set_item_icon_t>(cached_deltas, 1);
    TEST_ASSERT_EQUAL_STRING("stream-2", cached_icon.id.id.c_str());
    TEST_ASSERT_EQUAL_INT_ARRAY(icon.data(), cached_icon.icon.data.data(), icon.size());
}

TEST_CASE("icon_hash_change_requests_or_sets_icon_for_existing_item", "[volume_display]")
{
    volume_display_model_t model;
    model.refresh(std::vector{make_stream()}, std::vector<bridge_audio_stream_id_t>{});

    bridge_audio_stream_t hash_update{
        .id = make_stream_id(),
        .source = "ignored-new-source",
        .name = std::nullopt,
        .mute = std::nullopt,
        .volume = std::nullopt,
        .icon_hash = 200
    };

    auto missing_deltas = model.refresh(std::vector{hash_update}, std::vector<bridge_audio_stream_id_t>{});

    TEST_ASSERT_EQUAL(1, missing_deltas.size());

    const auto& missing_icon = get_delta<display_missing_icon_t>(missing_deltas, 0);
    TEST_ASSERT_EQUAL_STRING("app.exe", missing_icon.source.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-1", missing_icon.agent_id.c_str());
    TEST_ASSERT_EQUAL(200, missing_icon.icon_hash);

    const std::vector<uint8_t> icon{1, 2, 3, 4};
    auto set_icon_deltas = model.update_icon("app.exe", "agent-1", 200, 1, 1, icon);

    TEST_ASSERT_EQUAL(1, set_icon_deltas.size());

    const auto& set_icon = get_delta<display_set_item_icon_t>(set_icon_deltas, 0);
    TEST_ASSERT_EQUAL_STRING("stream-1", set_icon.id.id.c_str());
    TEST_ASSERT_EQUAL_INT_ARRAY(icon.data(), set_icon.icon.data.data(), icon.size());
}

TEST_CASE("icon_cache_evicts_least_recently_used_icon", "[volume_display]")
{
    display_icon_cache_t cache;
    const std::vector<uint8_t> icon{1, 2, 3, 4};

    for (auto i = 0; i < 16; ++i)
        cache.put("source-" + std::to_string(i), "agent", i, 1, 1, icon);

    TEST_ASSERT_TRUE(cache.get("source-0", "agent", 0).has_value());

    cache.put("source-16", "agent", 16, 1, 1, icon);

    TEST_ASSERT_EQUAL(16, cache.size());
    TEST_ASSERT_TRUE(cache.get("source-0", "agent", 0).has_value());
    TEST_ASSERT_FALSE(cache.get("source-1", "agent", 1).has_value());
}

TEST_CASE("volume_conversion_round_trips_between_protocol_and_display_values", "[volume_display]")
{
    TEST_ASSERT_EQUAL_FLOAT(volume_display_model_t::to_volume_fraction(0), 0.0f);
    TEST_ASSERT_EQUAL_FLOAT(volume_display_model_t::to_volume_fraction(42), 0.42f);
    TEST_ASSERT_EQUAL_FLOAT(volume_display_model_t::to_volume_fraction(100), 1.0f);
}
