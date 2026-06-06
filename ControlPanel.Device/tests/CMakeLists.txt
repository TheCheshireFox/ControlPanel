cmake_minimum_required(VERSION 3.20)

project(ControlPanel.Device.Tests LANGUAGES CXX)

enable_testing()

add_executable(volume_display_model_tests
    volume_display_model_tests.cpp)

target_compile_features(volume_display_model_tests PRIVATE cxx_std_23)
target_include_directories(volume_display_model_tests PRIVATE
    "${CMAKE_CURRENT_LIST_DIR}/../main")

add_test(NAME volume_display_model_tests COMMAND volume_display_model_tests)
