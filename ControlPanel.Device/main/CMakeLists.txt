idf_component_register(SRCS "main.cpp"
                       INCLUDE_DIRS ".")

target_compile_options(${COMPONENT_LIB} PRIVATE -Wno-missing-field-initializers)
target_compile_definitions(${COMPONENT_LIB} PRIVATE LV_COLOR_16_SWAP)