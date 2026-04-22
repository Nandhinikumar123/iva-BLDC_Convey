#include <stdio.h>
#include <string.h>
#include <vector>
#include <array>
#include <memory>
#include <cstring>
#include <sys/stat.h>

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
#include "esp_spiffs.h"

#include "esp_eth_ksz8863.h"
#include "ksz8863_ctrl.h"
#include "ksz8863.h"
#include "raw_lldp_task.h"

#include "Profinet.h"
#include "Device.h"
#include "Module.h"
#include "Submodule.h"
#include "standardconversions.h"

/* =========================================================
   PROFINET CONVERSION
   ========================================================= */
namespace profinet {
template<>
inline bool toProfinet<std::array<uint8_t,16>,16>(
    uint8_t* buffer, std::size_t numbytes, std::array<uint8_t,16> value)
{
    if (numbytes < 16) return false;
    std::memcpy(buffer, value.data(), 16);
    return true;
}
template<>
inline bool fromProfinet<std::array<uint8_t,16>,16>(
    const uint8_t* buffer, std::size_t numbytes, std::array<uint8_t,16>* value)
{
    if (numbytes < 16) return false;
    std::memcpy(value->data(), buffer, 16);
    return true;
}
}

#define TAG "PN"

/* ── Pin & PROFINET identity ── */
const uint8_t UNIQUE_ESP_MAC[6] = {0x14, 0xB1, 0x26, 0x07, 0x25, 0x6F};

#define PN_STATION_NAME  "esp32-pn-dev1"
#define VENDOR_ID        0x0025
#define DEVICE_ID        0x0101

/* ── PWM ── */
#define PIN_MOTOR_PWM    33          // PWM signal pin
#define PIN_MOTOR_EN     15          // D15 — enable pin, HIGH when PWM > 0
#define PWM_FREQ         25000
#define PWM_DUTY_50      127         // 50% duty on 8-bit timer

/* ── SPI / Ethernet ── */
#define PIN_MOSI  23
#define PIN_MISO  32
#define PIN_SCLK  18
#define PIN_CS     5
#define PIN_RST   13

extern "C" void pnal_eth_set_esp_handle(esp_eth_handle_t esp_handle);
static esp_eth_handle_t eth_handle_global = NULL;

/* =========================================================
   SPIFFS
   ========================================================= */
static bool init_spiffs()
{
    esp_vfs_spiffs_conf_t conf = {
        .base_path          = "/spiffs",
        .partition_label    = NULL,
        .max_files          = 10,
        .format_if_mount_failed = true,
    };
    if (esp_vfs_spiffs_register(&conf) != ESP_OK) {
        ESP_LOGE(TAG, "SPIFFS mount failed");
        return false;
    }
    return true;
}

/* =========================================================
   PWM + ENABLE PIN INIT
   ========================================================= */
static void init_pwm()
{
    /* Enable pin D15 — starts LOW (motor disabled) */
    gpio_reset_pin((gpio_num_t)PIN_MOTOR_EN);
    gpio_set_direction((gpio_num_t)PIN_MOTOR_EN, GPIO_MODE_OUTPUT);
    gpio_set_level((gpio_num_t)PIN_MOTOR_EN, 0);

    /* PWM timer */
    ledc_timer_config_t lt = {};
    lt.speed_mode      = LEDC_LOW_SPEED_MODE;
    lt.timer_num       = LEDC_TIMER_0;
    lt.duty_resolution = LEDC_TIMER_8_BIT;
    lt.freq_hz         = PWM_FREQ;
    lt.clk_cfg         = LEDC_AUTO_CLK;
    ESP_ERROR_CHECK(ledc_timer_config(&lt));

    /* PWM channel — starts at 0 duty */
    ledc_channel_config_t lc = {};
    lc.speed_mode = LEDC_LOW_SPEED_MODE;
    lc.channel    = LEDC_CHANNEL_0;
    lc.timer_sel  = LEDC_TIMER_0;
    lc.gpio_num   = PIN_MOTOR_PWM;
    lc.duty       = 0;
    lc.hpoint     = 0;
    ESP_ERROR_CHECK(ledc_channel_config(&lc));

    ESP_LOGI(TAG, "PWM init: pin=%d  EN pin=%d  freq=%d Hz",
             PIN_MOTOR_PWM, PIN_MOTOR_EN, PWM_FREQ);
}

/* =========================================================
   ETHERNET
   ========================================================= */
static esp_netif_t* s_eth_netif = NULL;

static void init_ethernet_hardware()
{
    gpio_set_direction((gpio_num_t)PIN_RST, GPIO_MODE_OUTPUT);
    gpio_set_level((gpio_num_t)PIN_RST, 0);
    vTaskDelay(pdMS_TO_TICKS(50));
    gpio_set_level((gpio_num_t)PIN_RST, 1);
    vTaskDelay(pdMS_TO_TICKS(200));

    spi_bus_config_t b = {};
    b.mosi_io_num = PIN_MOSI;
    b.miso_io_num = PIN_MISO;
    b.sclk_io_num = PIN_SCLK;
    ESP_ERROR_CHECK(spi_bus_initialize(SPI2_HOST, &b, SPI_DMA_CH_AUTO));

    ksz8863_ctrl_spi_config_t ks = {};
    ks.host_id        = SPI2_HOST;
    ks.clock_speed_hz = 1000000;
    ks.spics_io_num   = PIN_CS;

    ksz8863_ctrl_intf_config_t kc = {};
    kc.host_mode      = KSZ8863_SPI_MODE;
    kc.spi_dev_config = &ks;
    ESP_ERROR_CHECK(ksz8863_ctrl_intf_init(&kc));

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    esp_netif_config_t netif_cfg = ESP_NETIF_DEFAULT_ETH();
    s_eth_netif = esp_netif_new(&netif_cfg);

    esp_netif_ip_info_t ip_info = {};
    esp_netif_set_ip4_addr(&ip_info.ip,      192, 168, 1, 20);
    esp_netif_set_ip4_addr(&ip_info.gw,      192, 168, 1,  1);
    esp_netif_set_ip4_addr(&ip_info.netmask, 255, 255, 255, 0);
    esp_netif_dhcpc_stop(s_eth_netif);
    esp_netif_set_ip_info(s_eth_netif, &ip_info);

    eth_mac_config_t        mc  = ETH_MAC_DEFAULT_CONFIG();
    eth_esp32_emac_config_t ec  = ETH_ESP32_EMAC_DEFAULT_CONFIG();
    ec.smi_gpio.mdc_num  = -1;
    ec.smi_gpio.mdio_num = -1;

    esp_eth_mac_t*   mac = esp_eth_mac_new_esp32(&ec, &mc);
    eth_phy_config_t phy_cfg = ETH_PHY_DEFAULT_CONFIG();
    esp_eth_phy_t*   phy = esp_eth_phy_new_ksz8863(&phy_cfg);

    esp_eth_config_t config = ETH_KSZ8863_DEFAULT_CONFIG(mac, phy);

    ESP_ERROR_CHECK(esp_eth_driver_install(&config, &eth_handle_global));
    esp_eth_ioctl(eth_handle_global, ETH_CMD_S_MAC_ADDR, (uint8_t*)UNIQUE_ESP_MAC);

    bool pr = true;
    esp_eth_ioctl(eth_handle_global, ETH_CMD_S_PROMISCUOUS, &pr);

    esp_eth_netif_glue_handle_t glue = esp_eth_new_netif_glue(eth_handle_global);
    esp_netif_attach(s_eth_netif, glue);

    ESP_ERROR_CHECK(esp_eth_start(eth_handle_global));
    vTaskDelay(pdMS_TO_TICKS(500));
    esp_netif_action_connected(s_eth_netif, NULL, 0, NULL);

    ESP_LOGI(TAG, "ETH ready  MAC=%02X:%02X:%02X:%02X:%02X:%02X  IP=192.168.1.20",
        UNIQUE_ESP_MAC[0], UNIQUE_ESP_MAC[1], UNIQUE_ESP_MAC[2],
        UNIQUE_ESP_MAC[3], UNIQUE_ESP_MAC[4], UNIQUE_ESP_MAC[5]);
}

/* =========================================================
   ARP tasks (unchanged)
   ========================================================= */
void arp_task(void *arg)
{
    uint8_t arp[60] = {
        0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
        0x14,0xB1,0x26,0x07,0x25,0x6F,
        0x08,0x06,
        0x00,0x01,0x08,0x00,0x06,0x04,0x00,0x01,
        0x14,0xB1,0x26,0x07,0x25,0x6F,
        192,168,1,20,
        0,0,0,0,0,0,
        192,168,1,19,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };
    TickType_t last = xTaskGetTickCount();
    while (1) {
        esp_eth_transmit(eth_handle_global, arp, 60);
        vTaskDelayUntil(&last, pdMS_TO_TICKS(200));
    }
}

void raw_arp_task(void *arg)
{
    static uint8_t arp[60] = {
        0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
        0x14,0xB1,0x26,0x07,0x25,0x6F,
        0x08,0x06,
        0x00,0x01,0x08,0x00,0x06,0x04,0x00,0x01,
        0x14,0xB1,0x26,0x07,0x25,0x6F,
        192,168,1,20,
        0,0,0,0,0,0,
        192,168,1,19,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };
    TickType_t last = xTaskGetTickCount();
    while (1) {
        esp_eth_transmit(eth_handle_global, arp, 60);
        vTaskDelayUntil(&last, pdMS_TO_TICKS(200));
    }
}

/* =========================================================
   APP MAIN
   ========================================================= */
extern "C" void app_main(void)
{
    /* Init PWM + enable pin first — both start LOW/0 */
    init_pwm();

    bool spiffs_ok = init_spiffs();

    if (spiffs_ok) {
        const char* files_to_delete[] = {
            "/spiffs/pnet_data_ip.bin",
            "/spiffs/pnet_data_im.bin",
            "/spiffs/pnet_data_output.bin",
            "/spiffs/pnet_data_ar.bin",
            "/spiffs/pnet_data_diagnostics.bin",
        };
        for (auto f : files_to_delete) {
            struct stat st;
            if (stat(f, &st) == 0) {
                remove(f);
                ESP_LOGE(TAG, "DELETED: %s", f);
            } else {
                ESP_LOGE(TAG, "NOT FOUND: %s", f);
            }
        }
    }

    init_ethernet_hardware();
    pnal_eth_set_esp_handle(eth_handle_global);

    ESP_LOGI(TAG, "Starting PROFINET...");

    static profinet::Profinet pn;

    profinet::ProfinetProperties& props = pn.GetProperties();
    props.mainNetworkInterface    = "ETH_DEF";
    props.networkInterfaces       = {};
    props.pathStorageDirectory    = spiffs_ok ? "/spiffs" : "";
    props.ethThreadPriority       = 5;
    props.ethThreadStacksize      = 16384;
    props.bgWorkerThreadPriority  = 3;
    props.bgWorkerThreadStacksize = 16384;
    props.snmpThreadPriority      = 1;
    props.snmpThreadStacksize     = 8192;
    props.cycleTimerPriority      = 5;
    props.cycleWorkerPriority     = 4;
    props.cycleTimeUs             = 10000;

    profinet::Device& device = pn.GetDevice();
    device.properties.vendorID          = VENDOR_ID;
    device.properties.vendorName        = "ESP32_CONVEYOR";
    device.properties.deviceID          = DEVICE_ID;
    device.properties.deviceName        = "ESP32 Control With Reduced I/O";
    device.properties.stationName       = PN_STATION_NAME;
    device.properties.numSlots          = 3;
    device.properties.minDeviceInterval = 128;
    device.properties.productName       = "ESP32 Conveyor Control";
    device.properties.orderID           = "ESP32-01";
    device.properties.serialNumber      = "00000001";

    /* Slot 1 — INPUT to PLC (ESP32 → PLC)
       NOTE: motor control is handled entirely in ProfinetInternal::HandleCyclicData()
             This callback is registered but not called by the current wrapper.
             Keeping it for completeness only. */
    auto* mod1 = device.modules.Create(0x00000032, 1);
    if (mod1) {
        auto* sub1 = mod1->module.submodules.Create(0x00000001);
        if (sub1) {
            sub1->outputs.Create<std::array<uint8_t,16>, 16>(
                []() -> std::array<uint8_t, 16> {
                    std::array<uint8_t, 16> buf{};
                    buf[0] = 0x01;   // status byte — always 1, visible at IB128 on PLC
                    return buf;
                }
            );
        }
    }

    /* Slot 2 — OUTPUT from PLC (PLC → ESP32)
       NOTE: actual motor control runs in ProfinetInternal::HandleCyclicData()
             This callback is NOT invoked by the current wrapper. */
    auto* mod2 = device.modules.Create(0x00000033, 2);
    if (mod2) {
        auto* sub2 = mod2->module.submodules.Create(0x00000001);
        if (sub2) {
            sub2->inputs.Create<std::array<uint8_t,16>, 16>(
                [](std::array<uint8_t,16> data) {
                    (void)data; // not called by wrapper — handled in HandleCyclicData
                }
            );
        }
    }

    static std::unique_ptr<profinet::ProfinetControl> pnio =
        pn.Initialize(profinet::logging::CreateConsoleLogger());

    if (!pnio) {
        ESP_LOGE(TAG, "PROFINET Initialize() failed");
        return;
    }

    if (!pnio->Start()) {
        ESP_LOGE(TAG, "PROFINET Start() failed");
        return;
    }

    /* Let PROFINET tasks do all the work — nothing to do here */
    while (true) {
        vTaskDelay(pdMS_TO_TICKS(5000));
    }
}