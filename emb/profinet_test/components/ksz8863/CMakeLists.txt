idf_component_register(
    SRCS 
        "esp_eth_ksz8863.c"
        "esp_eth_phy_ksz8863.c"
        "esp_eth_pmac_ksz8863.c"
        "esp_eth_netif_glue_ksz8863.c"
        "ksz8863_ctrl.c"

    INCLUDE_DIRS "."

    REQUIRES 
        esp_eth
        esp_netif
        driver
        esp_timer
        freertos
)