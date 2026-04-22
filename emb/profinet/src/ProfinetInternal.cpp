#include "ProfinetInternal.h"
#include "dapModule.h"
#include "networktools.h"
#include "pnet_api.h"

#include <cstring>
#include <memory>
#include <functional>
#include <chrono>
#include <thread>
#include <sys/stat.h>
#include <sys/types.h>
#include <sstream>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/semphr.h"
#include "esp_timer.h" 
#include <cstdarg>
#include <cstdio>
#include "esp_log.h"
#include "driver/ledc.h"
#include "driver/gpio.h"

#define PIN_MOTOR_EN   15     // D15 — enable pin
#define PWM_DUTY_50   127     // 50% on 8-bit timer

inline constexpr static uint32_t arepNull{UINT32_MAX};

// ============================================================================
// Thread-Safety Architecture for P-Net
// ============================================================================
static SemaphoreHandle_t g_pnet_mutex = NULL;
static TaskHandle_t g_main_task_handle = NULL;

#define LOCK_PNET()   if(g_pnet_mutex) xSemaphoreTakeRecursive(g_pnet_mutex, portMAX_DELAY)
#define UNLOCK_PNET() if(g_pnet_mutex) xSemaphoreGiveRecursive(g_pnet_mutex)
// ============================================================================

namespace profinet
{
ProfinetInternal::ProfinetInternal() : 
   device(), alarmAllowed{true}, arep{arepNull}, initialized{false}, arepForReady(arepNull)
{
}

ProfinetInternal::~ProfinetInternal()
{
}

static inline std::string strPrintf (const char* format, ...)
{
    std::va_list args;
    std::string retval;
    va_start (args, format);
    retval.resize (vsnprintf (0, 0, format, args));
    vsnprintf (&retval[0], retval.size () + 1, format, args);
    va_end (args);
    return retval;
}

void ProfinetInternal::Log(LogLevel logLevel, const char* format, ...) noexcept
{
   if(!logFun) return;
   va_list args;
   std::string message;
   va_start (args, format);
   message.resize (vsnprintf (0, 0, format, args));
   vsnprintf (&message[0], message.size () + 1, format, args);
   va_end (args);
   logFun(logLevel, std::move(message));
}

bool ProfinetInternal::Initialize(const Profinet& configuration_, LoggerType logger)
{
   logFun = logger;
   configuration = configuration_;

   auto& deviceConfiguration{configuration.GetDevice()};
   device.Initialize(deviceConfiguration);

   pnetCfg = InitializePnetConfig();

   auto availableNetworkInterfaces = tools::GetNetworkInterfaces();
   mainNetworkInterface = configuration.GetProperties().mainNetworkInterface;
   networkInterfaces = configuration.GetProperties().networkInterfaces;
   tools::NetworkInterface mainInterfaceInfos;
   
   if (mainNetworkInterface.empty()) 
   {ESP_LOGE("Ezhil...............","mainInterfaceEmpty...");return false;}
   
   auto search = availableNetworkInterfaces.find(mainNetworkInterface);
   if(search == availableNetworkInterfaces.end()) 
   {ESP_LOGE("Ezhil...............","8888888888888888mainInterfaceEmpty...");return false;}
   mainInterfaceInfos = search->second;

   if (networkInterfaces.empty()) networkInterfaces.push_back(mainNetworkInterface);

   pnetCfg.num_physical_ports = 2; 

   pnet_if_cfg_t networkInterfaceConfig{};
   networkInterfaceConfig.main_netif_name = mainNetworkInterface.c_str();
   
   for (int i = 0; i < pnetCfg.num_physical_ports; i++)
   {
      if (i < networkInterfaces.size() && !networkInterfaces[i].empty()) {
          networkInterfaceConfig.physical_ports[i].netif_name = networkInterfaces[i].c_str();
      } else {
          networkInterfaceConfig.physical_ports[i].netif_name = mainNetworkInterface.c_str();
      }
      networkInterfaceConfig.physical_ports[i].default_mau_type = deviceConfiguration.properties.defaultMautype;
   }

   auto copyIP = [](uint32_t source, pnet_cfg_ip_addr_t& dest) {
      dest.a = ((source >> 24) & 0xFF);
      dest.b = ((source >> 16) & 0xFF);
      dest.c = ((source >> 8) & 0xFF);
      dest.d = (source & 0xFF);
   };
   copyIP(mainInterfaceInfos.ipRaw, networkInterfaceConfig.ip_cfg.ip_addr);
   copyIP(mainInterfaceInfos.gatewayRaw, networkInterfaceConfig.ip_cfg.ip_gateway);
   copyIP(mainInterfaceInfos.maskRaw, networkInterfaceConfig.ip_cfg.ip_mask);

   networkInterfaceConfig.ip_cfg.ip_addr = {192,168,1,20};
networkInterfaceConfig.ip_cfg.ip_mask = {255,255,255,0};
networkInterfaceConfig.ip_cfg.ip_gateway = {192,168,1,1};
ESP_LOGW("PNET_CFG", "========== PROFINET CONFIG ==========");

ESP_LOGW("PNET_CFG", "Interface   : %s", mainNetworkInterface.c_str());

ESP_LOGW("PNET_CFG", "IP Address  : %d.%d.%d.%d",
    networkInterfaceConfig.ip_cfg.ip_addr.a,
    networkInterfaceConfig.ip_cfg.ip_addr.b,
    networkInterfaceConfig.ip_cfg.ip_addr.c,
    networkInterfaceConfig.ip_cfg.ip_addr.d);

ESP_LOGW("PNET_CFG", "Subnet Mask : %d.%d.%d.%d",
    networkInterfaceConfig.ip_cfg.ip_mask.a,
    networkInterfaceConfig.ip_cfg.ip_mask.b,
    networkInterfaceConfig.ip_cfg.ip_mask.c,
    networkInterfaceConfig.ip_cfg.ip_mask.d);

ESP_LOGW("PNET_CFG", "Gateway     : %d.%d.%d.%d",
    networkInterfaceConfig.ip_cfg.ip_gateway.a,
    networkInterfaceConfig.ip_cfg.ip_gateway.b,
    networkInterfaceConfig.ip_cfg.ip_gateway.c,
    networkInterfaceConfig.ip_cfg.ip_gateway.d);

ESP_LOGW("PNET_CFG", "=====================================");

   pnetCfg.if_cfg = std::move(networkInterfaceConfig);

   pnetCfg.pnal_cfg.snmp_thread.prio = configuration.GetProperties().snmpThreadPriority;
   pnetCfg.pnal_cfg.snmp_thread.stack_size = configuration.GetProperties().snmpThreadStacksize;
   pnetCfg.pnal_cfg.eth_recv_thread.prio = configuration.GetProperties().ethThreadPriority;
   pnetCfg.pnal_cfg.eth_recv_thread.stack_size = configuration.GetProperties().ethThreadStacksize;
   pnetCfg.pnal_cfg.bg_worker_thread.prio = configuration.GetProperties().bgWorkerThreadPriority;
   pnetCfg.pnal_cfg.bg_worker_thread.stack_size = configuration.GetProperties().bgWorkerThreadStacksize;

   std::string storagePath = configuration.GetProperties().pathStorageDirectory;
   if (storagePath.empty()) storagePath = "/spiffs"; 
   while (storagePath.size() > 1 && storagePath.back() == '/') storagePath.pop_back();

   strcpy (pnetCfg.file_directory, storagePath.c_str());

   alarmAllowed = true;
   arep = arepNull;

   profinetStack = pnet_init(&pnetCfg);

if (!profinetStack) return false;

// ── Temporary diagnostic ──────────────────────────────────────────
ESP_LOGE("DIAG", "Configured station_name = [%s]", pnetCfg.station_name);
   
   initialized = true;
   return true;
}

bool ProfinetInternal::PlugDap(pnet_t* pnet, uint16_t number_of_ports)
{
   const pnet_data_cfg_t emptyDataCfg = { .data_dir = PNET_DIR_NO_IO, .insize = 0, .outsize = 0 };
   auto api = configuration.GetDevice().properties.api;

    // ✅ Use 0x00000002 to match DAP_2 in GSDML
    const uint32_t DAP_MODULE_ID    = 0x00000002;
    const uint32_t SUB_IDENTITY_ID  = 0x00000001;
    const uint32_t SUB_INTERFACE_ID = 0x00000002;
    const uint32_t SUB_PORT1_ID     = 0x00000003;
    const uint32_t SUB_PORT2_ID     = 0x00000004;

   CallbackExpModuleInd(pnet, api, dap::slot, dap::moduleId);
   CallbackExpSubmoduleInd(pnet, api, dap::slot, dap::subslotIdentity, dap::moduleId, dap::submoduleIdIdentity, &emptyDataCfg);
   CallbackExpSubmoduleInd(pnet, api, dap::slot, dap::subslotInterface1, dap::moduleId, dap::submoduleIdInterface1, &emptyDataCfg);
   CallbackExpSubmoduleInd(pnet, api, dap::slot, dap::subslotInterface1Port1, dap::moduleId,dap:: submoduleIdInterface1Port1, &emptyDataCfg);
   CallbackExpSubmoduleInd(pnet, api, dap::slot, dap::subslotInterface1Port2, dap::moduleId, dap::submoduleIdInterface1Port2, &emptyDataCfg);

   return true;
}

void ProfinetInternal::SetLed(bool on) {}

void ProfinetInternal::HandleCyclicData()
{
   auto api = configuration.GetDevice().properties.api;
 
   /* ── Send input data to PLC (ESP32 → PLC) ── */
   static uint8_t tx_buf[16] = {0};
   tx_buf[0] = 0x01;   // status byte — always 1 — PLC sees this at IB128
 
   pnet_input_set_data_and_iops(
      profinetStack, api,
      1, 0x0001,
      tx_buf, 16,
      PNET_IOXS_GOOD
   );
 
   /* ── Receive output data from PLC (PLC → ESP32) ── */
   bool     updated = false;
   uint8_t  rx_buf[16] = {0};
   uint8_t  iops = PNET_IOXS_BAD;
   uint16_t len  = 16;
 
   pnet_output_get_data_and_iops(
      profinetStack, api,
      2, 0x0001,
      &updated,
      rx_buf, &len,
      &iops
   );
 
   /* ── Log all 8 bytes so you can see exactly what PLC sends ── */
   ESP_LOGI("CYCLIC",
      "updated=%d iops=%d | "
      "b0=%02X b1=%02X b2=%02X b3=%02X b4=%02X b5=%02X b6=%02X b7=%02X",
      updated, iops,
      rx_buf[0], rx_buf[1], rx_buf[2], rx_buf[3],
      rx_buf[4], rx_buf[5], rx_buf[6], rx_buf[7]);
 
   /* ── Motor control ──
      QB128 = rx_buf[0]
      When PLC writes 1 to QB128:
        - PWM duty goes to PWM_DUTY_50 (50%)
        - D15 (enable pin) goes HIGH
      When PLC writes 0:
        - PWM duty goes to 0
        - D15 (enable pin) goes LOW                    */
   if (rx_buf[0] > 0)
   {
      ledc_set_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0, PWM_DUTY_50);
      ledc_update_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0);
      gpio_set_level((gpio_num_t)PIN_MOTOR_EN, 1);   // D15 HIGH
      ESP_LOGE("MOTOR", "RUNNING  EN=HIGH  duty=%d  rx[0]=%d",
               PWM_DUTY_50, rx_buf[0]);
   }
   else
   {
      ledc_set_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0, 0);
      ledc_update_duty(LEDC_LOW_SPEED_MODE, LEDC_CHANNEL_0);
      gpio_set_level((gpio_num_t)PIN_MOTOR_EN, 0);   // D15 LOW
   }
 
   /* ── Pass data to module ── */
   ModuleInstance* mod2 = device.GetModule(2);
   if (mod2)
   {
      SubmoduleInstance* sub2 = mod2->GetSubmodule(0x0001);
      if (sub2)
      {
         sub2->SetInput(rx_buf, len);
      }
   }
}

inline bool ProfinetInternal::IsConnectedToController()
{
   return arep != arepNull;
}

inline bool ProfinetInternal::SendApplicationReady(uint32_t arep)
{
   ESP_LOGE("APPREADY", "Calling pnet_application_ready with arep=%lu arepForReady=%lu", 
         (unsigned long)arep, (unsigned long)arepForReady);
   int ret = pnet_application_ready (profinetStack, arep);
   if (ret != 0) {
       Log(logError, "pnet_application_ready failed! Code: %d", ret);
       return false;
   }
   return true;
}

inline bool ProfinetInternal::HandleSendAlarmAck()
{
   pnet_pnio_status_t pnio_status = {0, 0, 0, 0};
   int ret = pnet_alarm_send_ack (profinetStack, arep, &lastAlarmArguments, &pnio_status);
   if (ret != 0) return false;
   return true;
}
void ProfinetInternal::loop()
{
   arep = arepNull;
   SetLed(false);
   
   LOCK_PNET();
   PlugDap(profinetStack, networkInterfaces.size());
   UNLOCK_PNET();

   Log(logInfo, "Waiting for PLC connect request...");

   while (true) {
    vTaskDelay(pdMS_TO_TICKS(1));
      
      LOCK_PNET();
      
      // ✅ Run cyclic data as soon as arep is set (from STARTUP event)
      
         HandleCyclicData();
      
      
      pnet_handle_periodic(profinetStack);
      
      UNLOCK_PNET();
   }
}

bool ProfinetInternal::Start()
{
   if (!initialized) return false;
   Log(logInfo, "Starting profinet interface...");

   g_pnet_mutex = xSemaphoreCreateRecursiveMutex();
   static constexpr int ESP32_SAFE_PRIO = configMAX_PRIORITIES - 3;

   // Use 1ms timer — fast enough for 128ms PLC watchdog
   esp_timer_handle_t periodic_timer;
   esp_timer_create_args_t timer_args = {};
   timer_args.callback = [](void* arg) {
       if (g_main_task_handle) xTaskNotifyGive(g_main_task_handle);
   };
   timer_args.arg = this;
   timer_args.name = "pnet_cycle_timer";
   timer_args.skip_unhandled_events = true;
   
   esp_timer_create(&timer_args, &periodic_timer);
   esp_timer_start_periodic(periodic_timer, 1000); // 1ms

   xTaskCreatePinnedToCore(
      [](void* arg) {
         static_cast<ProfinetInternal*>(arg)->loop();
         vTaskDelete(NULL);
      },
      "PnetMain", 32768, this, ESP32_SAFE_PRIO, &g_main_task_handle, 1
   );

   return true;
}
template<auto F, typename Result, typename... Args>
Result wrapFunction(pnet_t* pnet, void* state, Args...args)
{
   ProfinetInternal* connection = static_cast<ProfinetInternal*>(state);
   return std::invoke(F, *connection, pnet, args...);
}
pnet_cfg_t ProfinetInternal::InitializePnetConfig()
{
   pnet_cfg_t pnet_cfg{};
   memset (&pnet_cfg, 0, sizeof (pnet_cfg_t));

   auto highByte = [](const uint16_t value)-> uint8_t{return static_cast<uint8_t>((value >> 8) & 0xFF);};
   auto lowByte = [](const uint16_t value)-> uint8_t{return static_cast<uint8_t>(value & 0xFF);};

   pnet_cfg.tick_us = configuration.GetProperties().cycleTimeUs;
   auto& props{configuration.GetDevice().properties};

   pnet_cfg.im_0_data.im_vendor_id_hi = highByte(props.vendorID);
   pnet_cfg.im_0_data.im_vendor_id_lo = lowByte (props.vendorID);

   memset(pnet_cfg.im_0_data.im_order_id, ' ', sizeof(pnet_cfg.im_0_data.im_order_id));
   memcpy(pnet_cfg.im_0_data.im_order_id, "ESP32-01", 8);
   ESP_LOGE("Ezhil..............................", "im_order_id set to: %.20s", pnet_cfg.im_0_data.im_order_id);

   memset(pnet_cfg.im_0_data.im_serial_number, ' ', sizeof(pnet_cfg.im_0_data.im_serial_number));
   memcpy(pnet_cfg.im_0_data.im_serial_number, "00000001", 8);

   pnet_cfg.im_0_data.im_hardware_revision = 1;
   pnet_cfg.im_0_data.im_sw_revision_prefix = 'V';
   pnet_cfg.im_0_data.im_sw_revision_functional_enhancement = 1;
   pnet_cfg.im_0_data.im_sw_revision_bug_fix = 0;
   pnet_cfg.im_0_data.im_sw_revision_internal_change = 0;
   pnet_cfg.im_0_data.im_revision_counter = 0;
   pnet_cfg.im_0_data.im_profile_id = 0;
   pnet_cfg.im_0_data.im_profile_specific_type = 0;

   pnet_cfg.device_id.vendor_id_hi = highByte (props.vendorID);
   pnet_cfg.device_id.vendor_id_lo = lowByte (props.vendorID);
   pnet_cfg.device_id.device_id_hi = highByte (props.deviceID);
   pnet_cfg.device_id.device_id_lo = lowByte (props.deviceID);
   snprintf (pnet_cfg.station_name, sizeof (pnet_cfg.station_name), "%s", props.stationName.c_str());

   snprintf(pnet_cfg.product_name, sizeof(pnet_cfg.product_name), "%s", props.productName.c_str());
   pnet_cfg.send_hello = true;

   if (props.minDeviceInterval == 0) {
       pnet_cfg.min_device_interval = 32;
   } else {
       pnet_cfg.min_device_interval = props.minDeviceInterval;
   }

   pnet_cfg.state_cb = wrapFunction<&ProfinetInternal::CallbackStateInd>;
   pnet_cfg.connect_cb = wrapFunction<&ProfinetInternal::CallbackConnectInd>;
   pnet_cfg.release_cb = wrapFunction<&ProfinetInternal::CallbackReleaseInd>;
   pnet_cfg.dcontrol_cb = wrapFunction<&ProfinetInternal::CallbackDControlInd>;
   pnet_cfg.ccontrol_cb = wrapFunction<&ProfinetInternal::CallbackCControlCnf>;
   pnet_cfg.read_cb = wrapFunction<&ProfinetInternal::CallbackReadInd>;
   pnet_cfg.write_cb = wrapFunction<&ProfinetInternal::CallbackWriteInd>;
   pnet_cfg.exp_module_cb = wrapFunction<&ProfinetInternal::CallbackExpModuleInd>;
   pnet_cfg.exp_submodule_cb = wrapFunction<&ProfinetInternal::CallbackExpSubmoduleInd>;
   pnet_cfg.new_data_status_cb = wrapFunction<&ProfinetInternal::CallbackNewDataStatusInd>;
   pnet_cfg.alarm_ind_cb = wrapFunction<&ProfinetInternal::CallbackAlarmInd>;
   pnet_cfg.alarm_cnf_cb = wrapFunction<&ProfinetInternal::CallbackAlarmCnf>;
   pnet_cfg.alarm_ack_cnf_cb = wrapFunction<&ProfinetInternal::CallbackAlarmAckCnf>;
   pnet_cfg.reset_cb = wrapFunction<&ProfinetInternal::CallbackResetInd>;
   pnet_cfg.signal_led_cb = wrapFunction<&ProfinetInternal::CallbackSignalLedInd>;
   pnet_cfg.cb_arg = (void *)this;

   return pnet_cfg;
}

int ProfinetInternal::CallbackConnectInd (pnet_t * net, uint32_t arep, pnet_result_t * p_result) { return 0; }
int ProfinetInternal::CallbackReleaseInd (pnet_t * net, uint32_t arep, pnet_result_t * p_result)
 {
    ESP_LOGE("RELEASE", "ReleaseInd arep=%lu — clearing AR", (unsigned long)arep);
   device.SetDefaultInputsAll();
   this->arep = arepNull;
   arepForReady = arepNull;
   return 0;

   }
int ProfinetInternal::CallbackDControlInd (pnet_t * net, uint32_t arep, pnet_control_command_t control_command, pnet_result_t * p_result) { return 0; }
int ProfinetInternal::CallbackCControlCnf (pnet_t * net, uint32_t arep, pnet_result_t * p_result) 
{
    ESP_LOGE("CCONTROL", "CControlCnf arep=%lu error_code=0x%02X 0x%02X 0x%02X 0x%02X",
            (unsigned long)arep,
            p_result ? p_result->pnio_status.error_code : 0,
            p_result ? p_result->pnio_status.error_decode : 0,
            p_result ? p_result->pnio_status.error_code_1 : 0,
            p_result ? p_result->pnio_status.error_code_2 : 0);

   // If CControl failed, release the AR so next connect can succeed
   if (p_result && p_result->pnio_status.error_code != 0) {
      ESP_LOGE("CCONTROL", "CControl failed — releasing AR");
      pnet_ar_abort(net, arep);
      this->arep = arepNull;
      arepForReady = arepNull;
   }
   return 0;
    }

int ProfinetInternal::CallbackWriteInd (pnet_t * net, uint32_t arep, uint32_t api, uint16_t slot, uint16_t subslot, uint16_t idx, uint16_t sequence_number, uint16_t write_length, const uint8_t * p_write_data, pnet_result_t * p_result) { 
   LOCK_PNET();
   auto res = -1;
   ModuleInstance* moduleInstance = device.GetModule(slot);
   if(moduleInstance) {
       SubmoduleInstance* submoduleInstance = moduleInstance->GetSubmodule(subslot);  
       if(submoduleInstance) {
           ParameterInstance* parameterInstance = submoduleInstance->GetParameter(idx);
           if(parameterInstance && parameterInstance->Set(p_write_data, static_cast<std::size_t>(write_length))) {
               res = 0;
           }
       }
   }
   UNLOCK_PNET();
   return res;
}

int ProfinetInternal::CallbackReadInd (pnet_t * net, uint32_t arep, uint32_t api, uint16_t slot, uint16_t subslot, uint16_t idx, uint16_t sequence_number, uint8_t ** pp_read_data, uint16_t * p_read_length, pnet_result_t * p_result) { 
   LOCK_PNET();
   auto res = -1;
   ModuleInstance* moduleInstance = device.GetModule(slot);
   if(moduleInstance) {
       SubmoduleInstance* submoduleInstance = moduleInstance->GetSubmodule(subslot);  
       if(submoduleInstance) {
           ParameterInstance* parameterInstance = submoduleInstance->GetParameter(idx);
           if(parameterInstance) {
               size_t length = static_cast<std::size_t>(*p_read_length);
               if (parameterInstance->Get(pp_read_data, &length)) {
                   *p_read_length = static_cast<uint16_t>(length);
                   res = 0;
               }
           }
       }
   }
   UNLOCK_PNET();
   return res; 
}

bool ProfinetInternal::SetInitialDataAndIoxs()
{
   auto api = configuration.GetDevice().properties.api;
   static uint8_t zero_buf[256] = {0};
   int ret;

   // DAP
   static const uint16_t dapSubslots[] = {
      dap::subslotIdentity, dap::subslotInterface1,
      dap::subslotInterface1Port1, dap::subslotInterface1Port2
   };
   for (auto subslot : dapSubslots) {
      pnet_input_set_data_and_iops(profinetStack, api, dap::slot, subslot,
                                    zero_buf, 0, PNET_IOXS_GOOD);
      pnet_output_set_iocs(profinetStack, api, dap::slot, subslot, PNET_IOXS_GOOD);
   }

   // Slot 1: INPUT (device → PLC) 16 bytes
   ret = pnet_input_set_data_and_iops(profinetStack, api, 1, 0x0001,
                                       zero_buf, 16, PNET_IOXS_GOOD);
   ESP_LOGE("IOXS", "slot1 input_set ret=%d", ret);

   ret = pnet_output_set_iocs(profinetStack, api, 1, 0x0001, PNET_IOXS_GOOD);
   ESP_LOGE("IOXS", "slot1 output_iocs ret=%d", ret);

   // Slot 2: OUTPUT (PLC → device) — only set IOCS
   ret = pnet_output_set_iocs(profinetStack, api, 2, 0x0001, PNET_IOXS_GOOD);
   ESP_LOGE("IOXS", "slot2 output_iocs ret=%d", ret);

   return true;
}
int ProfinetInternal::CallbackStateInd(pnet_t* net, uint32_t arep, pnet_event_values_t event)
{
   ESP_LOGE("STATECB", "event=%d arep=%lu", (int)event, (unsigned long)arep);

   if (event == PNET_EVENT_ABORT) {
    uint16_t err_cls = 0;
    uint16_t err_code = 0;
    pnet_get_ar_error_codes(profinetStack, arep, &err_cls, &err_code);
    ESP_LOGE("STATECB", "ABORT err_cls=0x%04X err_code=0x%04X", err_cls, err_code);
    dataExchangeRunning = false;
    device.SetDefaultInputsAll();
    this->arep = arepNull;
    arepForReady = arepNull;
    pnet_set_provider_state(profinetStack, false);
}
   else if (event == PNET_EVENT_STARTUP) {
   this->arep = arep;
   SetInitialDataAndIoxs();
   pnet_set_provider_state(profinetStack, true);
   
   // Pre-load cyclic data BEFORE DATA event fires
   auto api = configuration.GetDevice().properties.api;
   static uint8_t tx_buf[16] = {0};
   int ret = pnet_input_set_data_and_iops(profinetStack, api, 1, 0x0001,
                                           tx_buf, 16, PNET_IOXS_GOOD);
   ESP_LOGE("STATECB", "STARTUP pre-load ret=%d", ret);
}
   else if (event == PNET_EVENT_PRMEND) {
   arepForReady = arep;
   pnet_set_provider_state(profinetStack, true);
   
   // Re-confirm data is ready before application_ready
   auto api = configuration.GetDevice().properties.api;
   static uint8_t tx_buf[16] = {0};
   pnet_input_set_data_and_iops(profinetStack, api, 1, 0x0001,
                                 tx_buf, 16, PNET_IOXS_GOOD);

   int ret = pnet_application_ready(net, arep);
   if (ret != 0) {
      this->arep = arepNull;
      arepForReady = arepNull;
   }
}
   else if (event == PNET_EVENT_DATA) {
      ESP_LOGE("STATECB", "*** DATA EXCHANGE RUNNING ***");
      dataExchangeRunning = true;
      synchronizationEvents.SignalCycle();  // kick off first cycle immediately
   }
   return 0;
}
int ProfinetInternal::CallbackResetInd (pnet_t * net, bool should_reset_application, uint16_t reset_mode) { return 0; }
int ProfinetInternal::CallbackSignalLedInd (pnet_t * net, bool led_state) { return 0; }

int ProfinetInternal::CallbackExpModuleInd (pnet_t * net, uint32_t api, uint16_t slot, uint32_t moduleId)
{
   LOCK_PNET();
   pnet_pull_module (net, api, slot);
   
   // DO NOT DELETE THE USER CONFIGURATION
   // device.RemoveFromSlot(slot); 
   
   int res = pnet_plug_module (net, api, slot, moduleId);
   UNLOCK_PNET();
   return res;
}

int ProfinetInternal::CallbackExpSubmoduleInd (pnet_t * net, uint32_t api, uint16_t slot, uint16_t subslot, uint32_t moduleId, uint32_t submoduleId, const pnet_data_cfg_t * p_exp_data)
{
   ESP_LOGE("EXPSUB", "slot=%u sub=0x%04X modId=0x%lX submodId=0x%lX dir=%d insize=%u outsize=%u",
            slot, subslot, (unsigned long)moduleId, (unsigned long)submoduleId,
            (int)p_exp_data->data_dir, p_exp_data->insize, p_exp_data->outsize);

   LOCK_PNET();
   pnet_pull_submodule (net, api, slot, subslot);
   int ret = pnet_plug_submodule (net, api, slot, subslot, moduleId, submoduleId, 
                                   p_exp_data->data_dir, p_exp_data->insize, p_exp_data->outsize);
   ESP_LOGE("EXPSUB", "pnet_plug_submodule slot=%u sub=0x%04X ret=%d", slot, subslot, ret);
   UNLOCK_PNET();
   return ret;
}

int ProfinetInternal::CallbackNewDataStatusInd (pnet_t * net, uint32_t arep, uint32_t crep, uint8_t changes, uint8_t data_status)
{
   LOCK_PNET();
   bool isRunning = data_status & (1U << PNET_DATA_STATUS_BIT_PROVIDER_STATE);
   bool isValid = data_status & (1U << PNET_DATA_STATUS_BIT_DATA_VALID);
   if (!isRunning || !isValid) device.SetDefaultInputsAll();
   UNLOCK_PNET();
   return 0;
}

int ProfinetInternal::CallbackAlarmInd (pnet_t * net, uint32_t arep, const pnet_alarm_argument_t * p_alarm_argument, uint16_t data_len, uint16_t data_usi, const uint8_t * p_data) { return 0; }
int ProfinetInternal::CallbackAlarmCnf (pnet_t * net, uint32_t arep, const pnet_pnio_status_t * p_pnio_status) { return 0; }
int ProfinetInternal::CallbackAlarmAckCnf (pnet_t * net, uint32_t arep, int res) { return 0; }

}