idf_component_register(
        SRCS "test_volume_display_model.cpp" "test_protocol_fixtures.cpp"
        REQUIRES unity control_panel_model
        EMBED_FILES
        fixtures/bridge/streams.msgpack
        fixtures/bridge/icon.msgpack
        fixtures/device/get-icon.msgpack
        fixtures/device/request-refresh.msgpack
        fixtures/device/set-mute.msgpack
        fixtures/device/set-volume.msgpack)
