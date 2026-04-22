#ifndef RAW_LLDP_TASK_H
#define RAW_LLDP_TASK_H

#ifdef __cplusplus
extern "C" {
#endif

void raw_lldp_task(void *arg);
#include "pnal.h"
extern pnal_eth_handle_t g_eth_handle;
#ifdef __cplusplus
}
#endif

#endif