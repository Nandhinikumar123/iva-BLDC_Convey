/*********************************************************************
 *  Generated pf.h (for ESP32 - manual configuration)
 *********************************************************************/

#ifndef PNET_OPTIONS_H
#define PNET_OPTIONS_H

/*************************************************************
 * Profinet Stack Options (disabled for minimal build)
 *************************************************************/
#define PNET_OPTION_FAST_STARTUP        0
#define PNET_OPTION_PARAMETER_SERVER    0
#define PNET_OPTION_IR                 0
#define PNET_OPTION_SR                 0
#define PNET_OPTION_REDUNDANCY         0
#define PNET_OPTION_AR_VENDOR_BLOCKS   0
#define PNET_OPTION_RS                 0
#define PNET_OPTION_MC_CR              0
#define PNET_OPTION_SRL                0
#define PNET_OPTION_SNMP               1
#define PNET_OPTION_DRIVER_ENABLE      0

#define PNET_USE_ATOMICS               0

/*************************************************************
 * Memory Configuration (small footprint for ESP32)
 *************************************************************/
#define PNET_MAX_AR                    1
#define PNET_MAX_API                   1
#define PNET_MAX_CR                    2
#define PNET_MAX_SLOTS                 5  /* DAP(slot0) + slot1 + slot2 + spare */
#define PNET_MAX_SUBSLOTS              8  /* DAP has 3 subslots, plus data subslots */
#define PNET_MAX_DFP_IOCR              0
#define PNET_MAX_PHYSICAL_PORTS        2

#define PNET_MAX_LOG_BOOK_ENTRIES      10
#define PNET_MAX_ALARMS                2
#define PNET_MAX_ALARM_PAYLOAD_DATA_SIZE 64

#define PNET_MAX_DIAG_ITEMS            10
#define PNET_MAX_DIAG_MANUF_DATA_SIZE  32

#define PNET_MAX_MAN_SPECIFIC_FAST_STARTUP_DATA_LENGTH 0
#define PNET_MAX_SESSION_BUFFER_SIZE   4096 /* Must be >= largest RPC packet (525+ bytes) */
#define PNET_MAX_DIRECTORYPATH_SIZE    64
#define PNET_MAX_FILENAME_SIZE         64
#define PNET_MAX_PORT_DESCRIPTION_SIZE 64

/*************************************************************
 * Logging (disable for now)
 *************************************************************/
#define LOG_LEVEL                      LOG_LEVEL_INFO

#define PF_ETH_LOG                     0
#define PF_LLDP_LOG                    0
#define PF_SNMP_LOG                    0
#define PF_CPM_LOG                     0
#define PF_PPM_LOG                     0
#define PF_DCP_LOG                     0x80 /* LOG_STATE_ON */
#define PF_RPC_LOG                     0x80 /* LOG_STATE_ON - enables RPC logging */
#define PF_ALARM_LOG                   0
#define PF_AL_BUF_LOG                  0
#define PF_PNAL_LOG                    0
#define PNET_LOG                       0x80 /* LOG_STATE_ON - enables CMDEV logging */

#endif /* PNET_OPTIONS_H */