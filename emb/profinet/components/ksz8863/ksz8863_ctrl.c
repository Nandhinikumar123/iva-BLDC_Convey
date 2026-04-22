#include <stdio.h>
#include "string.h"

#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"

#include "esp_log.h"
#include "esp_check.h"

#include "ksz8863_ctrl_internal.h"
#include "ksz8863.h"

#define KSZ8863_SPI_LOCK_TIMEOUT_MS 500

typedef struct {
    ksz8863_intf_mode_t mode;
    SemaphoreHandle_t bus_lock;
    esp_err_t (*ksz8863_reg_read)(uint8_t reg_addr, uint8_t *data, size_t len);
    esp_err_t (*ksz8863_reg_write)(uint8_t reg_addr, uint8_t *data, size_t len);
    spi_device_handle_t spi_handle;
} ksz8863_ctrl_intf_t;

static ksz8863_ctrl_intf_t *s_ksz8863_ctrl_intf = NULL;

static const char *TAG = "ksz8863_ctrl";

/* =========================
   BUS LOCK
========================= */
static inline bool bus_lock(uint32_t timeout)
{
    return xSemaphoreTake(s_ksz8863_ctrl_intf->bus_lock, pdMS_TO_TICKS(timeout)) == pdTRUE;
}

static inline void bus_unlock(void)
{
    xSemaphoreGive(s_ksz8863_ctrl_intf->bus_lock);
}

/* =========================
   SPI WRITE
========================= */
static esp_err_t ksz8863_spi_write(uint8_t reg_addr, uint8_t *data, size_t len)
{
    esp_err_t ret = ESP_OK;

    spi_transaction_t trans = {
        .cmd = KSZ8863_SPI_WRITE_CMD,
        .addr = reg_addr,
        .length = 8 * len,
        .tx_buffer = data
    };

    ESP_GOTO_ON_FALSE(bus_lock(KSZ8863_SPI_LOCK_TIMEOUT_MS), ESP_ERR_TIMEOUT, err, TAG, "SPI lock fail");

    ESP_GOTO_ON_ERROR(spi_device_polling_transmit(s_ksz8863_ctrl_intf->spi_handle, &trans),
                      err_release, TAG, "SPI write fail");

err_release:
    bus_unlock();
err:
    return ret;
}

/* =========================
   SPI READ
========================= */
static esp_err_t ksz8863_spi_read(uint8_t reg_addr, uint8_t *data, size_t len)
{
    esp_err_t ret = ESP_OK;

    spi_transaction_t trans = {
        .flags = len <= 4 ? SPI_TRANS_USE_RXDATA : 0,
        .cmd = KSZ8863_SPI_READ_CMD,
        .addr = reg_addr,
        .length = 8 * len,
        .rx_buffer = data
    };

    ESP_GOTO_ON_FALSE(bus_lock(KSZ8863_SPI_LOCK_TIMEOUT_MS), ESP_ERR_TIMEOUT, err, TAG, "SPI lock fail");

    ESP_GOTO_ON_ERROR(spi_device_polling_transmit(s_ksz8863_ctrl_intf->spi_handle, &trans),
                      err_release, TAG, "SPI read fail");

    bus_unlock();

    if ((trans.flags & SPI_TRANS_USE_RXDATA) && len <= 4) {
        memcpy(data, trans.rx_data, len);
    }

    return ESP_OK;

err_release:
    bus_unlock();
err:
    return ret;
}

/* =========================
   PHY ACCESS
========================= */
esp_err_t ksz8863_phy_reg_write(esp_eth_handle_t eth_handle, uint32_t phy_addr, uint32_t phy_reg, uint32_t reg_value)
{
    return s_ksz8863_ctrl_intf->ksz8863_reg_write(phy_reg, (uint8_t *)&reg_value, 1);
}

esp_err_t ksz8863_phy_reg_read(esp_eth_handle_t eth_handle, uint32_t phy_addr, uint32_t phy_reg, uint32_t *reg_value)
{
    return s_ksz8863_ctrl_intf->ksz8863_reg_read(phy_reg, (uint8_t *)reg_value, 1);
}

/* =========================
   INIT
========================= */
esp_err_t ksz8863_ctrl_intf_init(ksz8863_ctrl_intf_config_t *config)
{
    if (s_ksz8863_ctrl_intf != NULL) {
        ESP_LOGW(TAG, "Already initialized");
        return ESP_ERR_INVALID_STATE;
    }

    s_ksz8863_ctrl_intf = calloc(1, sizeof(ksz8863_ctrl_intf_t));
    ESP_RETURN_ON_FALSE(s_ksz8863_ctrl_intf, ESP_ERR_NO_MEM, TAG, "no mem");

    s_ksz8863_ctrl_intf->mode = config->host_mode;

    ESP_RETURN_ON_FALSE(config->host_mode == KSZ8863_SPI_MODE,
                        ESP_ERR_INVALID_ARG, TAG, "ONLY SPI MODE SUPPORTED");

    s_ksz8863_ctrl_intf->bus_lock = xSemaphoreCreateMutex();
    ESP_RETURN_ON_FALSE(s_ksz8863_ctrl_intf->bus_lock, ESP_ERR_NO_MEM, TAG, "mutex fail");

    spi_device_interface_config_t devcfg = {
        .command_bits = 8,
        .address_bits = 8,
        .mode = 0,
        .clock_speed_hz = config->spi_dev_config->clock_speed_hz,
        .spics_io_num = config->spi_dev_config->spics_io_num,
        .queue_size = 20
    };

    ESP_ERROR_CHECK(spi_bus_add_device(config->spi_dev_config->host_id,
                                       &devcfg,
                                       &s_ksz8863_ctrl_intf->spi_handle));

    s_ksz8863_ctrl_intf->ksz8863_reg_read = ksz8863_spi_read;
    s_ksz8863_ctrl_intf->ksz8863_reg_write = ksz8863_spi_write;

    return ESP_OK;
}

/* =========================
   DEINIT
========================= */
esp_err_t ksz8863_ctrl_intf_deinit(void)
{
    if (s_ksz8863_ctrl_intf) {
        vSemaphoreDelete(s_ksz8863_ctrl_intf->bus_lock);
        spi_bus_remove_device(s_ksz8863_ctrl_intf->spi_handle);

        free(s_ksz8863_ctrl_intf);
        s_ksz8863_ctrl_intf = NULL;
    }
    return ESP_OK;
}