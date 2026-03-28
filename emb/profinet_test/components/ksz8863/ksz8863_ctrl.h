#pragma once

#include "esp_err.h"
#include "driver/spi_master.h"
#include "esp_eth_driver.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef enum {
    KSZ8863_SPI_MODE,
    KSZ8863_SMI_MODE
} ksz8863_intf_mode_t;

/* =========================
   SPI CONFIG
========================= */
typedef struct {
    spi_host_device_t host_id;
    int32_t clock_speed_hz;
    int32_t spics_io_num;
} ksz8863_ctrl_spi_config_t;

/* =========================
   MAIN CONFIG
========================= */
typedef struct {
    ksz8863_intf_mode_t host_mode;
    ksz8863_ctrl_spi_config_t *spi_dev_config;
} ksz8863_ctrl_intf_config_t;

/**
 * @brief Initialize control interface (SPI ONLY)
 */
esp_err_t ksz8863_ctrl_intf_init(ksz8863_ctrl_intf_config_t *config);

/**
 * @brief Read register
 */
esp_err_t ksz8863_phy_reg_read(esp_eth_handle_t eth_handle,
                               uint32_t phy_addr,
                               uint32_t phy_reg,
                               uint32_t *reg_value);

/**
 * @brief Write register
 */
esp_err_t ksz8863_phy_reg_write(esp_eth_handle_t eth_handle,
                                uint32_t phy_addr,
                                uint32_t phy_reg,
                                uint32_t reg_value);

#ifdef __cplusplus
}
#endif