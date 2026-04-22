# Collect p-net core C sources from device/ and common/ subdirs
file(GLOB PNET_DEVICE_SRCS "../src/device/*.c")
file(GLOB PNET_COMMON_SRCS "../src/common/*.c")

# Explicit list of wrapper/library sources directly under src/
# - Excludes src/ports/linux/* (wrong platform)
# - Excludes src/pugixml/pugixml.cpp (duplicate; use src/pugixml.cpp instead)
# - Excludes src/easylogging.cc (header-only usage; do NOT compile as TU)
set(LIB_SRCS
    ../src/ConsoleLogger.cpp
    ../src/dapModule.cpp
    ../src/Device.cpp
    ../src/DeviceInstance.cpp
    ../src/FileLogger.cpp
    ../src/gsdmltools.cpp
    ../src/Input.cpp
    ../src/InputInstance.cpp
    ../src/Module.cpp
    ../src/ModuleInstance.cpp
    ../src/networktools.cpp      # fixed ESP32 version replaces original
    ../src/osal.c
    ../src/osal_log.c
    ../src/Output.cpp
    ../src/OutputInstance.cpp
    ../src/Parameter.cpp
    ../src/ParameterInstance.cpp
    ../src/Profinet.cpp
    ../src/ProfinetInternal.cpp
    ../src/pugixml.cpp           # single copy only
    ../src/Submodule.cpp
    ../src/SubmoduleInstance.cpp
    ../src/pnal_esp32.c          # ESP32 platform abstraction layer
)

idf_component_register(
    SRCS
        "profinet.cpp"
        ${LIB_SRCS}
        ${PNET_DEVICE_SRCS}
        ${PNET_COMMON_SRCS}
    INCLUDE_DIRS
        "../include"
        "../src"          # pf_includes.h, pf_types.h, osal.h, osal_log.h, etc.
    REQUIRES
        esp_eth
        esp_netif
        esp_wifi
        esp_timer
        driver
        nvs_flash
        lwip
        vfs
        spiffs
        ksz8863
)

# pugixml: disable C++ exceptions (ESP-IDF builds with -fno-exceptions)
# This matches the #define PUGIXML_NO_EXCEPTIONS in pugiconfig.hpp
target_compile_definitions(${COMPONENT_LIB} PRIVATE
    PUGIXML_NO_EXCEPTIONS
)

# Suppress harmless third-party library warnings
target_compile_options(${COMPONENT_LIB} PRIVATE
    "-Wno-reorder"
    "-Wno-error=reorder"
    "-Wno-unused-variable"
    "-Wno-error=unused-variable"
    "-Wno-unused-function"
    "-Wno-error=unused-function"
    "-Wno-missing-field-initializers"
    "-Wno-error=missing-field-initializers"
)