#include <cstdint>
#include <span>
#include <variant>

#define UNITY_SUPPORT_TEST_CASES

#include "unity.h"
#include "protocol/protocol.hpp"

extern const uint8_t streams_msgpack_start[] asm("_binary_streams_msgpack_start");
extern const uint8_t streams_msgpack_end[] asm("_binary_streams_msgpack_end");
extern const uint8_t icon_msgpack_start[] asm("_binary_icon_msgpack_start");
extern const uint8_t icon_msgpack_end[] asm("_binary_icon_msgpack_end");
extern const uint8_t get_icon_msgpack_start[] asm("_binary_get_icon_msgpack_start");
extern const uint8_t get_icon_msgpack_end[] asm("_binary_get_icon_msgpack_end");
extern const uint8_t request_refresh_msgpack_start[] asm("_binary_request_refresh_msgpack_start");
extern const uint8_t request_refresh_msgpack_end[] asm("_binary_request_refresh_msgpack_end");
extern const uint8_t set_mute_msgpack_start[] asm("_binary_set_mute_msgpack_start");
extern const uint8_t set_mute_msgpack_end[] asm("_binary_set_mute_msgpack_end");
extern const uint8_t set_volume_msgpack_start[] asm("_binary_set_volume_msgpack_start");
extern const uint8_t set_volume_msgpack_end[] asm("_binary_set_volume_msgpack_end");

std::span<const uint8_t> fixture_span(const uint8_t* start, const uint8_t* end)
{
    return {start, static_cast<std::size_t>(end - start)};
}

void assert_equal_bytes(std::span<const uint8_t> expected, std::span<const uint8_t> actual)
{
    TEST_ASSERT_EQUAL(expected.size(), actual.size());
    TEST_ASSERT_EQUAL_MEMORY(expected.data(), actual.data(), expected.size());
}

void assert_serializes_to(std::span<const uint8_t> expected, const auto& message)
{
    auto actual = serialize_bridge_message(message);
    assert_equal_bytes(expected, actual);
}

bridge_audio_stream_id_t fixture_stream_id()
{
    return {
        .id = "stream-1",
        .agent_id = "agent-a"
    };
}

TEST_CASE("parse_streams_message_matches_bridge_fixture", "[protocol]")
{
    auto parsed = parse_bridge_message(fixture_span(streams_msgpack_start, streams_msgpack_end));

    auto streams = std::get_if<streams_message_t>(&parsed);
    TEST_ASSERT_NOT_NULL(streams);
    TEST_ASSERT_EQUAL(static_cast<int>(bridge_message_type_t::streams), static_cast<int>(streams->type));

    TEST_ASSERT_EQUAL(1, streams->updated.size());
    const auto& updated = streams->updated[0];
    TEST_ASSERT_EQUAL_STRING("stream-1", updated.id.id.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-a", updated.id.agent_id.c_str());
    TEST_ASSERT_EQUAL_STRING("firefox", updated.source.c_str());
    TEST_ASSERT_TRUE(updated.name.has_value());
    TEST_ASSERT_EQUAL_STRING("Firefox", updated.name->c_str());
    TEST_ASSERT_TRUE(updated.mute.has_value());
    TEST_ASSERT_FALSE(*updated.mute);
    TEST_ASSERT_TRUE(updated.volume.has_value());
    TEST_ASSERT_EQUAL_FLOAT(0.42f, *updated.volume);
    TEST_ASSERT_TRUE(updated.icon_hash.has_value());
    TEST_ASSERT_EQUAL(123, *updated.icon_hash);

    TEST_ASSERT_EQUAL(1, streams->deleted.size());
    TEST_ASSERT_EQUAL_STRING("stream-2", streams->deleted[0].id.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-a", streams->deleted[0].agent_id.c_str());
}

TEST_CASE("parse_icon_message_matches_bridge_fixture", "[protocol]")
{
    auto parsed = parse_bridge_message(fixture_span(icon_msgpack_start, icon_msgpack_end));

    auto icon = std::get_if<icon_message_t>(&parsed);
    TEST_ASSERT_NOT_NULL(icon);
    TEST_ASSERT_EQUAL(static_cast<int>(bridge_message_type_t::icon), static_cast<int>(icon->type));
    TEST_ASSERT_EQUAL_STRING("firefox", icon->source.c_str());
    TEST_ASSERT_EQUAL_STRING("agent-a", icon->agent_id.c_str());
    TEST_ASSERT_EQUAL(123, icon->icon_hash);
    TEST_ASSERT_EQUAL(2, icon->size);

    const uint8_t expected_icon[] = {1, 2, 3, 4};
    TEST_ASSERT_EQUAL(sizeof(expected_icon), icon->icon.size());
    TEST_ASSERT_EQUAL_MEMORY(expected_icon, icon->icon.data(), sizeof(expected_icon));
}

TEST_CASE("serialize_set_volume_message_matches_device_fixture", "[protocol]")
{
    set_volume_message_t message{};
    message.id = fixture_stream_id();
    message.volume = 0.42f;

    assert_serializes_to(fixture_span(set_volume_msgpack_start, set_volume_msgpack_end), message);
}

TEST_CASE("serialize_set_mute_message_matches_device_fixture", "[protocol]")
{
    set_mute_message_t message{};
    message.id = fixture_stream_id();
    message.mute = true;

    assert_serializes_to(fixture_span(set_mute_msgpack_start, set_mute_msgpack_end), message);
}

TEST_CASE("serialize_get_icon_message_matches_device_fixture", "[protocol]")
{
    get_icon_message_t message{};
    message.source = "firefox";
    message.agent_id = "agent-a";
    message.icon_hash = 123;

    assert_serializes_to(fixture_span(get_icon_msgpack_start, get_icon_msgpack_end), message);
}

TEST_CASE("serialize_request_refresh_message_matches_device_fixture", "[protocol]")
{
    request_refresh_message_t message{};

    assert_serializes_to(fixture_span(request_refresh_msgpack_start, request_refresh_msgpack_end), message);
}
