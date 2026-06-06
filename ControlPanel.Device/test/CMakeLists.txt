cmake_minimum_required(VERSION 3.22)

set(EXTRA_COMPONENT_DIRS "../components" "../managed_components")
set(TEST_COMPONENTS "control_panel_model" CACHE STRING "List of components to test")

include($ENV{IDF_PATH}/tools/cmake/project.cmake)
idf_build_set_property(MINIMAL_BUILD ON)
project(unit_test)
