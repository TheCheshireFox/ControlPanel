idf_component_register(SRCS "main.cpp" INCLUDE_DIRS ".")

target_include_directories(${COMPONENT_LIB} PRIVATE "${CMAKE_CURRENT_LIST_DIR}" "${CMAKE_CURRENT_LIST_DIR}/lib/etl/include")

target_compile_options(${COMPONENT_LIB} PRIVATE -Wno-missing-field-initializers)