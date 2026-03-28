idf_component_register(
    SRCS "profinet_test.c"
    INCLUDE_DIRS "."
    REQUIRES 
        ksz8863
        esp_timer
        esp_driver_gpio
        esp_driver_spi
        freertos
)