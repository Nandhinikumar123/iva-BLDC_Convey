#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_timer.h"
#include "driver/gpio.h"
#include "driver/ledc.h"
#include "esp_log.h"
#include "esp_eth.h"
#include "esp_event.h"
#include "esp_netif.h"
#include "driver/spi_master.h"

/* KSZ8863 Specific Headers */
#include "esp_eth_ksz8863.h"
#include "ksz8863_ctrl.h"

#define TAG "CONVEYOR_CTRL"

/* --- MOTOR CONFIG --- */
#define PIN_MOTOR_PWM      14    
#define PWM_FREQ           25000 
#define PWM_DUTY_50        127   

/* --- SPI PINS --- */
#define MY_SPI_HOST    SPI2_HOST
#define PIN_MOSI       23
#define PIN_MISO       32
#define PIN_SCLK       18
#define PIN_CS         5
#define PIN_RST        13

/* --- GLOBALS --- */
#define WATCHDOG_TIMEOUT_MS 500
volatile int64_t last_heartbeat = 0;
volatile bool plc_connected = false;
static esp_eth_handle_t eth_handle = NULL;
uint8_t my_mac[6] = {0};

/* ===========================================
   DCP RESPONDER (Permanent Flags + GSDML Match + Device Type)
   =========================================== */
void send_dcp_identify_response(uint8_t *req) {
    uint8_t resp[128] = {0}; 
    
    // 1. Ethernet Header (14 bytes)
    memcpy(resp, &req[6], 6);           // Dest: Requester MAC
    memcpy(&resp[6], my_mac, 6);        // Source: ESP32 MAC
    resp[12] = 0x88; resp[13] = 0x92;   // EtherType: PROFINET
    
    // 2. DCP Header (12 bytes)
    resp[14] = 0xFE; resp[15] = 0xFF;   // FrameID: Identify Response
    resp[16] = 0x05;                    // Service ID: Identify (0x05)
    resp[17] = 0x01;                    // Service Type: Response Success
    resp[18] = req[18]; resp[19] = req[19]; // XID (4 bytes)
    resp[20] = req[20]; resp[21] = req[21]; 
    resp[22] = 0x00; resp[23] = 0x00;   // Response Delay
    
    uint16_t pos = 26;

    // --- Block 1: Name of Station ("esp32-pn") ---
    resp[pos++] = 0x02; resp[pos++] = 0x02; // Type: Name
    resp[pos++] = 0x00; resp[pos++] = 0x0A; // Length: 10 (2 Info + 8 String)
    resp[pos++] = 0x00; resp[pos++] = 0x01; // **0x01 = PERMANENT NAME**
    memcpy(&resp[pos], "esp32-pn", 8);
    pos += 8;

    // --- Block 2: IP Address (192.168.1.20) ---
    resp[pos++] = 0x01; resp[pos++] = 0x02; // Type: IP
    resp[pos++] = 0x00; resp[pos++] = 0x0E; // Length: 14
    resp[pos++] = 0x00; resp[pos++] = 0x01; // **0x01 = PERMANENT IP**
    resp[pos++] = 192; resp[pos++] = 168; resp[pos++] = 1; resp[pos++] = 20; 
    resp[pos++] = 255; resp[pos++] = 255; resp[pos++] = 255; resp[pos++] = 0;
    resp[pos++] = 192; resp[pos++] = 168; resp[pos++] = 1; resp[pos++] = 1;

    // --- Block 3: Device ID (GSDML: 0x0025, 0x0101) ---
    resp[pos++] = 0x02; resp[pos++] = 0x03; // Type: ID
    resp[pos++] = 0x00; resp[pos++] = 0x06; // Length: 6
    resp[pos++] = 0x00; resp[pos++] = 0x00; // Block Info
    resp[pos++] = 0x00; resp[pos++] = 0x25; // **Vendor ID: 0x0025**
    resp[pos++] = 0x01; resp[pos++] = 0x01; // **Device ID: 0x0101**
    
    // --- Block 4: Device Type / Vendor Name ("ESP32_CONVEYOR") ---
    resp[pos++] = 0x02; resp[pos++] = 0x01; // Type: Device Type (Vendor Value)
    resp[pos++] = 0x00; resp[pos++] = 0x10; // Length: 16 (2 Info + 14 String)
    resp[pos++] = 0x00; resp[pos++] = 0x00; // Block Info
    memcpy(&resp[pos], "ESP32_CONVEYOR", 14); // Exactly matches GSDML <VendorName>
    pos += 14;

    // --- Block 5: Device Role ---
    resp[pos++] = 0x02; resp[pos++] = 0x04; // Type: Role
    resp[pos++] = 0x00; resp[pos++] = 0x04; // Length: 4
    resp[pos++] = 0x00; resp[pos++] = 0x00; // Block Info
    resp[pos++] = 0x01; // Role: IO-Device
    resp[pos++] = 0x00; // Reserved

    // Calculate DCP Data Length and insert into Header
    uint16_t dcp_data_len = pos - 26;
    resp[24] = (uint8_t)(dcp_data_len >> 8);
    resp[25] = (uint8_t)(dcp_data_len & 0xFF);

    // Transmit
    esp_eth_transmit(eth_handle, resp, (pos < 60) ? 60 : pos);
    ESP_LOGW(TAG, "📡 DCP Response Sent: GSDML Match + Device Type (%d bytes)", pos);
}

/* =========================
   PROFINET INPUT HANDLER
   ========================= */
static esp_err_t pnio_input_handler(esp_eth_handle_t hdl, uint8_t *buf, uint32_t len, void *priv) {
    if (len < 16) return ESP_OK;
    uint16_t et = (buf[12] << 8) | buf[13];
    if (et != 0x8892) return ESP_OK;

    uint16_t fid = (buf[14] << 8) | buf[15];
    
    // Handle Discovery Requests
    if (fid == 0xFEFE || fid == 0xFEFF) {
        send_dcp_identify_response(buf);
        return ESP_OK;
    }

    // Handle Real-Time Control Data
    if (fid >= 0x8000 && fid <= 0xBFFF) {
        last_heartbeat = esp_timer_get_time() / 1000;
        plc_connected = true;
        ledc_set_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0, (buf[16] == 0x01) ? PWM_DUTY_50 : 0);
        ledc_update_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0);
    }
    return ESP_OK;
}

/* =========================
   SAFETY TASK
   ========================= */
void safety_task(void *pv) {
    while(1) {
        if (plc_connected && (esp_timer_get_time()/1000 - last_heartbeat > WATCHDOG_TIMEOUT_MS)) {
            ledc_set_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0, 0); 
            ledc_update_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0);
            plc_connected = false;
            ESP_LOGE(TAG, "🛑 PLC CONNECTION TIMEOUT - MOTOR STOPPED");
        }
        vTaskDelay(pdMS_TO_TICKS(100));
    }
}

/* =========================
   APP MAIN
   ========================= */
void app_main(void) {
    ESP_LOGI(TAG, "Initializing Conflict-Free PROFINET Node...");
    
    // 1. PWM Setup
    ledc_timer_config_t lt = {.speed_mode=LEDC_LOW_SPEED_MODE, .timer_num=LEDC_TIMER_0, .duty_resolution=LEDC_TIMER_8_BIT, .freq_hz=PWM_FREQ, .clk_cfg=LEDC_AUTO_CLK};
    ledc_timer_config(&lt);
    ledc_channel_config_t lc = {.speed_mode=LEDC_LOW_SPEED_MODE, .channel=LEDC_CHANNEL_0, .timer_sel=LEDC_TIMER_0, .gpio_num=PIN_MOTOR_PWM, .duty=0, .hpoint=0};
    ledc_channel_config(&lc);

    // 2. Hardware Reset for KSZ8863
    gpio_set_direction(PIN_RST, GPIO_MODE_OUTPUT);
    gpio_set_level(PIN_RST, 0); 
    vTaskDelay(pdMS_TO_TICKS(100));
    gpio_set_level(PIN_RST, 1); 
    vTaskDelay(pdMS_TO_TICKS(200));

    // 3. SPI & KSZ8863 Initialization
    spi_bus_config_t b={.mosi_io_num=PIN_MOSI, .miso_io_num=PIN_MISO, .sclk_io_num=PIN_SCLK, .quadwp_io_num=-1, .quadhd_io_num=-1};
    spi_bus_initialize(MY_SPI_HOST, &b, SPI_DMA_CH_AUTO);
    ksz8863_ctrl_spi_config_t ks={.host_id=MY_SPI_HOST, .clock_speed_hz=1000000, .spics_io_num=PIN_CS};
    ksz8863_ctrl_intf_config_t kc={.host_mode=KSZ8863_SPI_MODE, .spi_dev_config=&ks};
    ksz8863_ctrl_intf_init(&kc);

    // 4. Ethernet Stack Setup
    esp_netif_init();
    esp_event_loop_create_default();
    eth_mac_config_t mc = ETH_MAC_DEFAULT_CONFIG();
    eth_esp32_emac_config_t ec = ETH_ESP32_EMAC_DEFAULT_CONFIG();
    ec.smi_gpio.mdc_num = -1; ec.smi_gpio.mdio_num = -1;
    esp_eth_mac_t *mac = esp_eth_mac_new_esp32(&ec, &mc);
    esp_eth_phy_t *phy = esp_eth_phy_new_ksz8863(&(eth_phy_config_t)ETH_PHY_DEFAULT_CONFIG());
    esp_eth_config_t config = ETH_KSZ8863_DEFAULT_CONFIG(mac, phy);
    esp_eth_driver_install(&config, &eth_handle);

    // 5. Start the internal switch engine
    uint32_t gc0; ksz8863_phy_reg_read(NULL, 0, 0x01, &gc0);
    ksz8863_phy_reg_write(NULL, 0, 0x01, gc0 | 0x01);

    // 6. Start Ethernet and get MAC
    esp_eth_start(eth_handle);
    esp_eth_ioctl(eth_handle, ETH_CMD_G_MAC_ADDR, my_mac);
    
    // 7. Promiscuous Mode & Custom Input Handler
    bool p = true; 
    esp_eth_ioctl(eth_handle, ETH_CMD_S_PROMISCUOUS, &p);
    esp_eth_update_input_path(eth_handle, pnio_input_handler, NULL);

    // 8. Launch Watchdog Task
    xTaskCreate(safety_task, "safety", 2048, NULL, 10, NULL);
    
    ESP_LOGI(TAG, "🚀 READY. MAC: %02x:%02x:%02x:%02x:%02x:%02x | IP: 192.168.1.20", 
             my_mac[0], my_mac[1], my_mac[2], my_mac[3], my_mac[4], my_mac[5]);
}