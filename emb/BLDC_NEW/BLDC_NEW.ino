/*
 * KSZ8863 Ethernet Switch & Dual Modbus Motor Controller
 * ZPA Master/Slave Network Mode (RTC Memory Architecture)
 * Target: ESP32 (Core v3.x Compatible)
 */

#include <Arduino.h>
#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_netif.h"
#include "esp_eth.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_system.h"
#include "driver/gpio.h"
#include "driver/spi_master.h"

// Networking headers
#include <unistd.h>
#include <sys/fcntl.h>
#include <sys/ioctl.h>
#include "lwip/prot/ethernet.h"
#include "errno.h"
#include "arpa/inet.h"
#include "lwip/sockets.h"

extern "C" {
  #include "esp_eth_ksz8863.h"
}

// ==========================================
// HARDWARE PINS & CONFIGURATION
// ==========================================
#define ETH_SPI_HOST         SPI2_HOST 
#define ETH_SPI_SCLK_GPIO    18
#define ETH_SPI_MOSI_GPIO    23
#define ETH_SPI_MISO_GPIO    32
#define ETH_SPI_CS_GPIO      5
#define ETH_SPI_INT_GPIO     13  // Hardware Reset
#define ETH_SPI_CLOCK_MHZ    16

// Dual Motor Control Configuration (DRV8306 EVMs)
#define LEFT_PWM_PIN         12 
#define RIGHT_PWM_PIN        14 
#define PWM_FREQ             25000 
#define PWM_RES              8     

// ZPA Configuration
#define ZPA_BUTTON_PIN       33  // Pin for ZPA Mode switch (Connect to GND to trigger)

uint16_t L_Speed = 100, L_Accel = 50, L_Decel = 50, L_Brake = 0,L_Curr = 25 ,L_Motortp=1;
uint16_t R_Speed = 100, R_Accel = 50, R_Decel = 50, R_Brake = 0,R_Curr = 25 ,R_Motortp=1;
// ==========================================
// GLOBALS & RTC MEMORY
// ==========================================
// RTC Memory survives soft reboots perfectly
RTC_NOINIT_ATTR uint32_t zpa_state_memory;
#define ZPA_STATE_MASTER 0xAAAAAAAA
#define ZPA_STATE_SLAVE  0xBBBBBBBB

static const char *TAG = "KSZ8863_MotorApp";
esp_netif_t *eth_netif = NULL;

esp_eth_handle_t host_eth_handle = NULL;
esp_eth_handle_t p1_eth_handle = NULL;
esp_eth_handle_t p2_eth_handle = NULL;

volatile bool has_dynamic_ip = false; // Tracks if we received an IP from a Master

// Task Prototypes
void tcpServer_task(void *pvParameters);
void zpa_button_task(void *pvParameters);
void ip_fallback_task(void *pvParameters);

// ==========================================
// NETWORK EVENT HANDLERS
// ==========================================

static void eth_event_handler(void *arg, esp_event_base_t event_base, int32_t event_id, void *event_data) {
    if (event_data == NULL) return;

    // Extract the handle of the port that triggered this event
    esp_eth_handle_t eth_handle = *(esp_eth_handle_t *)event_data;
    
    const char *port_name = "Unknown Port";
    if (eth_handle == host_eth_handle) {
        port_name = "Internal Host (ESP32)";
    } else if (eth_handle == p1_eth_handle) {
        port_name = "Port A (Port 1)";
    } else if (eth_handle == p2_eth_handle) {
        port_name = "Port B (Port 2)";
    }

    switch (event_id) {
        case ETHERNET_EVENT_CONNECTED:
            Serial.printf("\n[Ethernet] Link UP detected on %s.\n", port_name);
            
            // Hot-Plug Logic: If Slave, a new cable might mean a Master appeared
            if (zpa_state_memory == ZPA_STATE_SLAVE) {
                Serial.println("[Network] Restarting search for ZPA Master...");
                
                has_dynamic_ip = false; 
                esp_netif_dhcpc_stop(eth_netif); 
                esp_netif_dhcpc_start(eth_netif); 
                
                xTaskCreate(ip_fallback_task, "IP_Fallback", 2048, NULL, 5, NULL);
            }
            break;
            
        case ETHERNET_EVENT_DISCONNECTED:
            Serial.printf("\n[Ethernet] Link DOWN detected on %s.\n", port_name);
            break;
    }
}

static void got_ip_event_handler(void *arg, esp_event_base_t event_base, int32_t event_id, void *event_data) {
    ip_event_got_ip_t *event = (ip_event_got_ip_t *)event_data;
    Serial.printf("[Network] Got IP: " IPSTR "\n", IP2STR(&event->ip_info.ip));
    has_dynamic_ip = true; 
}

// ==========================================
// BACKGROUND TASKS
// ==========================================

void ip_fallback_task(void *pvParameters) {
    // 5-second countdown to see if a ZPA Master answers
    vTaskDelay(pdMS_TO_TICKS(5000)); 

    if (!has_dynamic_ip) {
        // No Master responded. Lock in the App IP.
        Serial.println("\n[Network] No ZPA Master found (Timeout).");
        Serial.println("[Network] Falling back to App Mode Static IP...");
        
        esp_netif_dhcpc_stop(eth_netif); 
        
        esp_netif_ip_info_t ip_info;
        ip_info.ip.addr = ESP_IP4TOADDR(192, 168, 150, 20); 
        ip_info.netmask.addr = ESP_IP4TOADDR(255, 255, 255, 0);
        ip_info.gw.addr = ESP_IP4TOADDR(192, 168, 150, 1);
        esp_err_t err = esp_netif_set_ip_info(eth_netif, &ip_info);
        
        if (err == ESP_OK) {
            Serial.println("[Network] Ready! App IP is fixed at 192.168.150.20.");
        } else {
            Serial.printf("[Network] IP Fallback Failed: %s\n", esp_err_to_name(err));
        }
    } else {
        Serial.println("[Network] Operating securely as a SLAVE under a ZPA Master.");
    }
    
    vTaskDelete(NULL); 
}

// ==========================================
// KSZ8863 INITIALIZATION
// ==========================================

esp_err_t ksz8863_board_specific_init(esp_eth_handle_t eth_handle) {
    spi_bus_config_t buscfg = {};
    buscfg.mosi_io_num = ETH_SPI_MOSI_GPIO;
    buscfg.miso_io_num = ETH_SPI_MISO_GPIO;
    buscfg.sclk_io_num = ETH_SPI_SCLK_GPIO;
    buscfg.quadwp_io_num = -1;
    buscfg.quadhd_io_num = -1;
    ESP_ERROR_CHECK(spi_bus_initialize(ETH_SPI_HOST, &buscfg, SPI_DMA_CH_AUTO));

    ksz8863_ctrl_spi_config_t spi_dev_config = {};
    spi_dev_config.host_id = ETH_SPI_HOST;
    spi_dev_config.clock_speed_hz = ETH_SPI_CLOCK_MHZ * 1000 * 1000;
    spi_dev_config.spics_io_num = ETH_SPI_CS_GPIO;

    ksz8863_ctrl_intf_config_t ctrl_intf_cfg = {};
    ctrl_intf_cfg.host_mode = KSZ8863_SPI_MODE;
    ctrl_intf_cfg.spi_dev_config = &spi_dev_config;

    ESP_ERROR_CHECK(ksz8863_ctrl_intf_init(&ctrl_intf_cfg));
    ESP_ERROR_CHECK(ksz8863_hw_reset(ETH_SPI_INT_GPIO));
    return ksz8863_sw_reset(eth_handle);
}

// ==========================================
// MAIN ARDUINO SETUP
// ==========================================

void setup() {
    Serial.begin(115200);
    
    // Hardware Reset Pulse for KSZ8863
    pinMode(ETH_SPI_INT_GPIO, OUTPUT);
    digitalWrite(ETH_SPI_INT_GPIO, LOW);  
    delay(100);                           
    digitalWrite(ETH_SPI_INT_GPIO, HIGH); 
    delay(250);                           
    
    Serial.println("\n--- KSZ8863 Switch (Dual Mode Architecture) ---");

    // Default to SLAVE/App Mode on cold power-up
    if (esp_reset_reason() != ESP_RST_SW) {
        zpa_state_memory = ZPA_STATE_SLAVE;
    }

    bool is_zpa_master = (zpa_state_memory == ZPA_STATE_MASTER);

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    eth_mac_config_t mac_config = ETH_MAC_DEFAULT_CONFIG();
    eth_phy_config_t phy_config = ETH_PHY_DEFAULT_CONFIG();
    eth_esp32_emac_config_t esp32_emac_config = ETH_ESP32_EMAC_DEFAULT_CONFIG();
    esp32_emac_config.smi_gpio.mdc_num = -1;
    esp32_emac_config.smi_gpio.mdio_num = -1;

    esp_eth_mac_t *host_mac = esp_eth_mac_new_esp32(&esp32_emac_config, &mac_config);
    phy_config.phy_addr = -1; 
    esp_eth_phy_t *host_phy = esp_eth_phy_new_ksz8863(&phy_config);
    esp_eth_config_t host_config = ETH_KSZ8863_DEFAULT_CONFIG(host_mac, host_phy);
    host_config.on_lowlevel_init_done = ksz8863_board_specific_init;
    ESP_ERROR_CHECK(esp_eth_driver_install(&host_config, &host_eth_handle));

    ksz8863_eth_mac_config_t ksz_pmac_cfg = {}; 
    ksz_pmac_cfg.pmac_mode = KSZ8863_SWITCH_MODE;
    
    ksz_pmac_cfg.port_num = KSZ8863_PORT_1;
    esp_eth_mac_t *p1_mac = esp_eth_mac_new_ksz8863(&ksz_pmac_cfg, &mac_config);
    phy_config.phy_addr = KSZ8863_PORT_1;
    esp_eth_phy_t *p1_phy = esp_eth_phy_new_ksz8863(&phy_config);
    esp_eth_config_t p1_config = ETH_KSZ8863_DEFAULT_CONFIG(p1_mac, p1_phy);
    ESP_ERROR_CHECK(esp_eth_driver_install(&p1_config, &p1_eth_handle));

    ksz_pmac_cfg.port_num = KSZ8863_PORT_2; 
    esp_eth_mac_t *p2_mac = esp_eth_mac_new_ksz8863(&ksz_pmac_cfg, &mac_config);
    phy_config.phy_addr = KSZ8863_PORT_2;
    esp_eth_phy_t *p2_phy = esp_eth_phy_new_ksz8863(&phy_config);
    esp_eth_config_t p2_config = ETH_KSZ8863_DEFAULT_CONFIG(p2_mac, p2_phy);
    ESP_ERROR_CHECK(esp_eth_driver_install(&p2_config, &p2_eth_handle));

    // --- NETWORK MODE SELECTION ---
    if (is_zpa_master) {
        Serial.println("[Network] ZPA Mode Active. Becoming the Network Master...");

        esp_netif_inherent_config_t base_cfg = ESP_NETIF_INHERENT_DEFAULT_ETH();
        base_cfg.flags = (esp_netif_flags_t)(ESP_NETIF_DHCP_SERVER | ESP_NETIF_FLAG_AUTOUP);
        esp_netif_config_t cfg = { .base = &base_cfg, .driver = NULL, .stack = ESP_NETIF_NETSTACK_DEFAULT_ETH };
        eth_netif = esp_netif_new(&cfg);
        ESP_ERROR_CHECK(esp_netif_attach(eth_netif, esp_eth_new_netif_glue(host_eth_handle)));

        esp_netif_dhcps_stop(eth_netif);

        // Master claims Gateway IP
        esp_netif_ip_info_t ip_info;
        ip_info.ip.addr = ESP_IP4TOADDR(192, 168, 150, 1); 
        ip_info.netmask.addr = ESP_IP4TOADDR(255, 255, 255, 0);
        ip_info.gw.addr = ESP_IP4TOADDR(192, 168, 150, 1);
        ESP_ERROR_CHECK(esp_netif_set_ip_info(eth_netif, &ip_info));

        esp_err_t err = esp_netif_dhcps_start(eth_netif);
        if (err == ESP_OK) {
            Serial.println("[Network] ZPA Master ready! My IP is 192.168.150.1. Handing out IPs to others...");
        } else {
            Serial.printf("[Network] DHCP Server Start Failed: %s\n", esp_err_to_name(err));
        }
    }
    else {
        Serial.println("[Network] App Mode Active. Searching for ZPA Master...");
        has_dynamic_ip = false; 
        
        esp_netif_config_t cfg = ESP_NETIF_DEFAULT_ETH();
        eth_netif = esp_netif_new(&cfg);
        ESP_ERROR_CHECK(esp_netif_attach(eth_netif, esp_eth_new_netif_glue(host_eth_handle)));

        // Start looking for a router/Master
        esp_netif_dhcpc_start(eth_netif);

        // Spawn the 5-second countdown timer
        xTaskCreate(ip_fallback_task, "IP_Fallback", 2048, NULL, 5, NULL);
    }

    // Register Handlers & Start Drivers
    ESP_ERROR_CHECK(esp_event_handler_register(ETH_EVENT, ESP_EVENT_ANY_ID, &eth_event_handler, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT, IP_EVENT_ETH_GOT_IP, &got_ip_event_handler, NULL));

    ESP_ERROR_CHECK(esp_eth_start(host_eth_handle));
    ESP_ERROR_CHECK(esp_eth_start(p1_eth_handle));
    ESP_ERROR_CHECK(esp_eth_start(p2_eth_handle));

    // Initialize Dual Motor PWM
    ledcAttach(LEFT_PWM_PIN, PWM_FREQ, PWM_RES);
    ledcWrite(LEFT_PWM_PIN, 0); 
    ledcAttach(RIGHT_PWM_PIN, PWM_FREQ, PWM_RES);
    ledcWrite(RIGHT_PWM_PIN, 0); 
    
    // Spawn User Tasks
    xTaskCreate(tcpServer_task, "TCP_Server", 4096, NULL, 5, NULL);
    xTaskCreate(zpa_button_task, "ZPA_Button", 4096, NULL, 5, NULL); 

    Serial.println("Setup Complete.");
}

void loop() {
    vTaskDelay(pdMS_TO_TICKS(1000));
}

// ==========================================
// ZPA BUTTON LOGIC
// ==========================================

void zpa_button_task(void *pvParameters) {
    pinMode(ZPA_BUTTON_PIN, INPUT_PULLUP);

    while (1) {
        if (digitalRead(ZPA_BUTTON_PIN) == LOW) {
            vTaskDelay(pdMS_TO_TICKS(3000)); // 3-second hold
            
            if (digitalRead(ZPA_BUTTON_PIN) == LOW) { 
                Serial.println("\n[ZPA] *** LONG PRESS DETECTED ***");
                
                if (zpa_state_memory == ZPA_STATE_MASTER) {
                    Serial.println("[ZPA] Downgrading to App Mode (Slave)...");
                    zpa_state_memory = ZPA_STATE_SLAVE;
                } else {
                    Serial.println("[ZPA] Upgrading to ZPA Master Mode...");
                    zpa_state_memory = ZPA_STATE_MASTER;
                }
                
                Serial.println("[ZPA] Rebooting network stack in 1 second...");
                vTaskDelay(pdMS_TO_TICKS(1000));
                esp_restart(); 
            }
        }
        vTaskDelay(pdMS_TO_TICKS(100)); 
    }
}

// ==========================================
// MODBUS TCP SERVER & MOTOR CONTROL
// ==========================================

// --- Helper: Send a single Modbus TCP Register to the PC ---
void sendModbusFrame(int sock, uint8_t slaveId, uint16_t reg, uint16_t val) {
    uint8_t packet[12];
    packet[0] = 0x00; packet[1] = 0x01; // Transaction ID
    packet[2] = 0x00; packet[3] = 0x00; // Protocol ID
    packet[4] = 0x00; packet[5] = 0x06; // Length (6 bytes follow)
    packet[6] = slaveId;                // Unit ID
    packet[7] = 0x03;                   // Function (Read Holding Register Response)
    packet[8] = (uint8_t)(reg >> 8);    // Reg Hi (Stored for C# to identify)
    packet[9] = (uint8_t)(reg & 0xFF);  // Reg Lo
    packet[10] = (uint8_t)(val >> 8);   // Data Hi
    packet[11] = (uint8_t)(val & 0xFF); // Data Lo

    send(sock, packet, 12, 0);
}

void tcpServer_task(void *pvParameters) {
    struct sockaddr_in dest_addr;
    dest_addr.sin_addr.s_addr = htonl(INADDR_ANY);
    dest_addr.sin_family = AF_INET;
    dest_addr.sin_port = htons(502);

    int listen_sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listen_sock < 0) {
        ESP_LOGE(TAG, "Unable to create socket: errno %d", errno);
        vTaskDelete(NULL);
        return;
    }

    int opt = 1;
    setsockopt(listen_sock, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

    if (bind(listen_sock, (struct sockaddr *)&dest_addr, sizeof(dest_addr)) < 0) {
        ESP_LOGE(TAG, "Socket unable to bind: errno %d", errno);
        close(listen_sock);
        vTaskDelete(NULL);
        return;
    }

    listen(listen_sock, 1);
    ESP_LOGI(TAG, "Modbus TCP Server started on port 502");

    while (1) {
        struct sockaddr_storage source_addr;
        socklen_t addr_len = sizeof(source_addr);
        int sock = accept(listen_sock, (struct sockaddr *)&source_addr, &addr_len);

        if (sock < 0) {
            vTaskDelay(pdMS_TO_TICKS(10));
            continue;
        }

        // Set to NON-BLOCKING so we can broadcast telemetry while waiting for commands
        fcntl(sock, F_SETFL, O_NONBLOCK);
        ESP_LOGI(TAG, "PC Connected. Waiting for Handshake...");

        uint8_t rx_buffer[128];
        unsigned long last_telemetry = 0;
        bool handshake_complete = false; // CRITICAL: Gating flag

        while (1) {
            // Using FreeRTOS ticks converted to ms for timing
            unsigned long now = xTaskGetTickCount() * portTICK_PERIOD_MS;

            // --- 1. RECEIVE COMMANDS FROM PC ---
            int len = recv(sock, rx_buffer, sizeof(rx_buffer), 0);

            if (len > 0) {
                // Handle Handshake (01 01)
                if (rx_buffer[0] == 0x01 && rx_buffer[1] == 0x01) {
                    send(sock, rx_buffer, len, 0); // Echo back
                    handshake_complete = true;     // UNLOCK TELEMETRY
                    ESP_LOGI(TAG, "Handshake Successful. Telemetry Unlocked.");
                }
                // Handle Modbus Write (Function 06)
                else if (len >= 12 && rx_buffer[7] == 0x06) {
                    uint16_t reg = (rx_buffer[8] << 8) | rx_buffer[9];
                    uint16_t val = (rx_buffer[10] << 8) | rx_buffer[11];

                    if (reg == 0x0027) L_Speed = val;
                    else if (reg == 0x003F) R_Speed = val;
                    // Add other register mappings here...

                    send(sock, rx_buffer, len, 0); // Echo confirmation
                }
            }
            // Check for disconnection
            else if (len == 0 || (len < 0 && errno != EAGAIN && errno != EWOULDBLOCK)) {
                ESP_LOGW(TAG, "PC Disconnected.");
                break;
            }

            // --- 2. BROADCAST ALL DATA (Every 200ms - ONLY AFTER HANDSHAKE) ---
            if (handshake_complete && (now - last_telemetry > 200)) {
                last_telemetry = now;

                // --- Left Motor Block ---
                sendModbusFrame(sock, 0x01, 0x0027, L_Speed);
                sendModbusFrame(sock, 0x01, 0x002A, L_Accel);
                sendModbusFrame(sock, 0x01, 0x002B, L_Decel);
                sendModbusFrame(sock, 0x01, 0x002E, L_Brake);
                sendModbusFrame(sock, 0x01, 0x0036, L_Curr);
                sendModbusFrame(sock, 0x01, 0x0001, L_Motortp);
                //sendModbusFrame(sock, 0x01, 0x003A, L_Status_Word);

                // --- Right Motor Block ---
                sendModbusFrame(sock, 0x01, 0x003F, R_Speed);
                sendModbusFrame(sock, 0x01, 0x0042, R_Accel);
                sendModbusFrame(sock, 0x01, 0x0043, R_Decel);
                sendModbusFrame(sock, 0x01, 0x0046, R_Brake);
                sendModbusFrame(sock, 0x01, 0x004E, R_Curr);
                sendModbusFrame(sock, 0x01, 0x0002, R_Motortp);
                //sendModbusFrame(sock, 0x01, 0x0052, R_Status_Word);
            }

            vTaskDelay(pdMS_TO_TICKS(5)); // Prevents WDT reset
        }

        // Cleanup before waiting for next connection
        close(sock);
        ESP_LOGI(TAG, "Socket closed. Waiting for new connection...");
    }
}