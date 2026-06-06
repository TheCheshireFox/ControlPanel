#include "../main/volume_display_model.hpp"

#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <span>
#include <sstream>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    struct test_failure : std::runtime_error
    {
        test_failure(const std::string& message) : runtime_error(message) {}
    };

    [[noreturn]] void fail(const std::string& message)
    {
        throw test_failure{message};
    }

    void expect(bool value, std::string_view message)
    {
        if (!value)
            fail(std::string{message});
    }

    template<typename T, typename U>
    void expect_equal(const T& actual, const U& expected, std::string_view message)
    {
        if (actual == expected)
            return;

        std::ostringstream output;
        output << message << ": expected " << expected << ", got " << actual;
        fail(output.str());
    }

    template<typename T>
    const T& expect_delta(const std::vector<display_delta_t>& deltas, std::size_t index)
    {
        expect(index < deltas.size(), "delta index out of range");

        auto delta = std::get_if<T>(&deltas[index]);
        if (delta == nullptr)
        {
            std::ostringstream output;
            output << "unexpected delta type at index " << index;
            fail(output.str());
        }

        return *delta;
    }

    bridge_audio_stream_id_t stream_id(std::string id = "stream-1", std::string agent_id = "agent-1")
    {
        return bridge_audio_stream_id_t{
            .id = std::move(id),
            .agent_id = std::move(agent_id)
        };
    }

    bridge_audio_stream_t stream(
        std::string id = "stream-1",
        std::string agent_id = "agent-1",
        std::string source = "app.exe",
        std::string name = "App",
        float volume = 0.42f,
        bool mute = false,
        int32_t icon_hash = 100)
    {
        return bridge_audio_stream_t{
            .id = stream_id(std::move(id), std::move(agent_id)),
            .source = std::move(source),
            .name = std::move(name),
            .mute = mute,
            .volume = volume,
            .icon_hash = icon_hash
        };
    }

    void add_complete_stream_emits_item_and_missing_icon()
    {
        volume_display_model_t model;

        auto deltas = model.refresh(std::vector{stream()}, std::vector<bridge_audio_stream_id_t>{});

        expect_equal(deltas.size(), std::size_t{2}, "delta count");
        expect_equal(model.size(), std::size_t{1}, "model size");

        const auto& add = expect_delta<display_add_item_t>(deltas, 0);
        expect_equal(add.id.id, "stream-1", "added id");
        expect_equal(add.id.agent_id, "agent-1", "added agent id");
        expect_equal(add.source, "app.exe", "added source");
        expect_equal(add.title, "App", "added title");
        expect_equal(add.volume, static_cast<int8_t>(42), "added volume");
        expect_equal(add.mute, false, "added mute");
        expect_equal(add.icon_hash, 100, "added icon hash");

        const auto& missing_icon = expect_delta<display_missing_icon_t>(deltas, 1);
        expect_equal(missing_icon.source, "app.exe", "missing icon source");
        expect_equal(missing_icon.agent_id, "agent-1", "missing icon agent id");
        expect_equal(missing_icon.icon_hash, 100, "missing icon hash");
    }

    void incomplete_new_stream_is_ignored()
    {
        volume_display_model_t model;
        bridge_audio_stream_t incomplete{
            .id = stream_id(),
            .source = "app.exe",
            .name = "App",
            .mute = true
        };

        auto deltas = model.refresh(std::vector{incomplete}, std::vector<bridge_audio_stream_id_t>{});

        expect(deltas.empty(), "incomplete stream should not emit deltas");
        expect_equal(model.size(), std::size_t{0}, "model size");
    }

    void update_existing_stream_emits_only_present_fields()
    {
        volume_display_model_t model;
        model.refresh(std::vector{stream()}, std::vector<bridge_audio_stream_id_t>{});

        bridge_audio_stream_t update{
            .id = stream_id(),
            .source = "ignored-new-source",
            .mute = true,
            .volume = 0.7f
        };

        auto deltas = model.refresh(std::vector{update}, std::vector<bridge_audio_stream_id_t>{});

        expect_equal(deltas.size(), std::size_t{1}, "delta count");

        const auto& updated = expect_delta<display_update_item_t>(deltas, 0);
        expect_equal(updated.id.id, "stream-1", "updated id");
        expect(!updated.title.has_value(), "title should be absent");
        expect(updated.mute.has_value(), "mute should be present");
        expect_equal(*updated.mute, true, "updated mute");
        expect(updated.volume.has_value(), "volume should be present");
        expect_equal(*updated.volume, static_cast<int8_t>(70), "updated volume");
    }

    void deleted_stream_emits_remove_once()
    {
        volume_display_model_t model;
        model.refresh(std::vector{stream()}, std::vector<bridge_audio_stream_id_t>{});

        auto deltas = model.refresh(
            std::vector<bridge_audio_stream_t>{},
            std::vector{stream_id(), stream_id("missing", "agent-1")});

        expect_equal(deltas.size(), std::size_t{1}, "delta count");
        expect_equal(model.size(), std::size_t{0}, "model size");

        const auto& remove = expect_delta<display_remove_item_t>(deltas, 0);
        expect_equal(remove.id.id, "stream-1", "removed id");
        expect_equal(remove.id.agent_id, "agent-1", "removed agent id");
    }

    void missing_icon_requests_are_deduplicated_per_refresh()
    {
        volume_display_model_t model;
        auto first = stream("stream-1", "agent-1", "app.exe", "App 1", 0.1f, false, 100);
        auto second = stream("stream-2", "agent-1", "app.exe", "App 2", 0.2f, false, 100);

        auto deltas = model.refresh(std::vector{first, second}, std::vector<bridge_audio_stream_id_t>{});

        expect_equal(deltas.size(), std::size_t{3}, "delta count");
        expect_delta<display_add_item_t>(deltas, 0);
        expect_delta<display_add_item_t>(deltas, 1);

        const auto& missing_icon = expect_delta<display_missing_icon_t>(deltas, 2);
        expect_equal(missing_icon.source, "app.exe", "missing icon source");
        expect_equal(missing_icon.agent_id, "agent-1", "missing icon agent id");
        expect_equal(missing_icon.icon_hash, 100, "missing icon hash");
    }

    void update_icon_applies_to_matching_items_and_reuses_cache()
    {
        volume_display_model_t model;
        model.refresh(std::vector{stream()}, std::vector<bridge_audio_stream_id_t>{});

        const std::vector<uint8_t> icon{1, 2, 3, 4};
        auto icon_deltas = model.update_icon("app.exe", "agent-1", 100, 2, 2, icon);

        expect_equal(icon_deltas.size(), std::size_t{1}, "icon delta count");
        const auto& set_icon = expect_delta<display_set_item_icon_t>(icon_deltas, 0);
        expect_equal(set_icon.id.id, "stream-1", "set icon id");
        expect_equal(set_icon.icon.w, uint32_t{2}, "icon width");
        expect_equal(set_icon.icon.h, uint32_t{2}, "icon height");
        expect_equal(set_icon.icon.data.size(), icon.size(), "icon byte count");
        expect_equal(set_icon.icon.data[2], uint8_t{3}, "icon byte");

        auto cached_deltas = model.refresh(
            std::vector{stream("stream-2", "agent-1", "app.exe", "App 2", 0.2f, false, 100)},
            std::vector<bridge_audio_stream_id_t>{});

        expect_equal(cached_deltas.size(), std::size_t{2}, "cached delta count");
        expect_delta<display_add_item_t>(cached_deltas, 0);
        const auto& cached_icon = expect_delta<display_set_item_icon_t>(cached_deltas, 1);
        expect_equal(cached_icon.id.id, "stream-2", "cached icon id");
        expect_equal(cached_icon.icon.data[0], uint8_t{1}, "cached icon byte");
    }

    void icon_hash_change_requests_or_sets_icon_for_existing_item()
    {
        volume_display_model_t model;
        model.refresh(std::vector{stream()}, std::vector<bridge_audio_stream_id_t>{});

        bridge_audio_stream_t hash_update{
            .id = stream_id(),
            .source = "ignored-new-source",
            .icon_hash = 200
        };

        auto missing_deltas = model.refresh(std::vector{hash_update}, std::vector<bridge_audio_stream_id_t>{});

        expect_equal(missing_deltas.size(), std::size_t{1}, "missing delta count");
        const auto& missing_icon = expect_delta<display_missing_icon_t>(missing_deltas, 0);
        expect_equal(missing_icon.source, "app.exe", "missing icon source uses stored item source");
        expect_equal(missing_icon.agent_id, "agent-1", "missing icon agent id");
        expect_equal(missing_icon.icon_hash, 200, "missing icon hash");

        const std::vector<uint8_t> icon{9};
        auto set_icon_deltas = model.update_icon("app.exe", "agent-1", 200, 1, 1, icon);

        expect_equal(set_icon_deltas.size(), std::size_t{1}, "set icon delta count");
        const auto& set_icon = expect_delta<display_set_item_icon_t>(set_icon_deltas, 0);
        expect_equal(set_icon.id.id, "stream-1", "set icon id");
        expect_equal(set_icon.icon.data[0], uint8_t{9}, "set icon byte");
    }

    void icon_cache_evicts_least_recently_used_icon()
    {
        display_icon_cache_t cache;
        const std::vector<uint8_t> icon{1};

        for (int i = 0; i < 16; ++i)
            cache.put("source-" + std::to_string(i), "agent", i, 1, 1, icon);

        expect(cache.get("source-0", "agent", 0).has_value(), "source-0 should be cached");

        cache.put("source-16", "agent", 16, 1, 1, icon);

        expect_equal(cache.size(), std::size_t{16}, "cache size");
        expect(cache.get("source-0", "agent", 0).has_value(), "recently used icon should remain cached");
        expect(!cache.get("source-1", "agent", 1).has_value(), "least recently used icon should be evicted");
    }

    void volume_conversion_round_trips_between_protocol_and_display_values()
    {
        expect_equal(volume_display_model_t::to_volume_fraction(0), 0.0f, "zero volume fraction");
        expect_equal(volume_display_model_t::to_volume_fraction(42), 0.42f, "normal volume fraction");
        expect_equal(volume_display_model_t::to_volume_fraction(100), 1.0f, "max volume fraction");
    }

    template<typename Test>
    bool run_test(std::string_view name, Test test)
    {
        try
        {
            test();
            std::cout << "[PASS] " << name << '\n';
            return true;
        }
        catch (const test_failure& failure)
        {
            std::cerr << "[FAIL] " << name << ": " << failure.what() << '\n';
            return false;
        }
        catch (const std::exception& exception)
        {
            std::cerr << "[FAIL] " << name << ": unexpected exception: " << exception.what() << '\n';
            return false;
        }
    }
}

int main()
{
    auto success = true;
    success &= run_test("add_complete_stream_emits_item_and_missing_icon", add_complete_stream_emits_item_and_missing_icon);
    success &= run_test("incomplete_new_stream_is_ignored", incomplete_new_stream_is_ignored);
    success &= run_test("update_existing_stream_emits_only_present_fields", update_existing_stream_emits_only_present_fields);
    success &= run_test("deleted_stream_emits_remove_once", deleted_stream_emits_remove_once);
    success &= run_test("missing_icon_requests_are_deduplicated_per_refresh", missing_icon_requests_are_deduplicated_per_refresh);
    success &= run_test("update_icon_applies_to_matching_items_and_reuses_cache", update_icon_applies_to_matching_items_and_reuses_cache);
    success &= run_test("icon_hash_change_requests_or_sets_icon_for_existing_item", icon_hash_change_requests_or_sets_icon_for_existing_item);
    success &= run_test("icon_cache_evicts_least_recently_used_icon", icon_cache_evicts_least_recently_used_icon);
    success &= run_test("volume_conversion_round_trips_between_protocol_and_display_values", volume_conversion_round_trips_between_protocol_and_display_values);

    return success ? EXIT_SUCCESS : EXIT_FAILURE;
}
