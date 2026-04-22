/*********************************************************************
 * ESP32 Platform Abstraction Layer (pnal) implementation
 * Replaces src/ports/linux/pnal.c + pnal_eth.c + pnal_udp.c +
 * pnal_filetools.c + pnal_snmp.c for the ESP-IDF / FreeRTOS target.
 ********************************************************************/

#include "pnal.h"
#include "pnal_config.h"
#include "osal.h"
#include "osal_log.h"
#include "raw_lldp_task.h"
/* pnet_api.h defines PNET_INTERFACE_NAME_MAX_SIZE (16) */
#include "pnet_api.h"

#include "esp_eth.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "esp_netif.h"
#include "esp_timer.h"
#include "esp_vfs.h"
#include "lwip/sockets.h"
#include "lwip/netif.h"
#include "lwip/tcpip.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <errno.h>

/* =========================================================================
 * Buffer management  (pnal_buf_alloc / pnal_buf_free / pnal_buf_header)
 * ========================================================================= */

uint32_t pnal_buf_alloc_cnt = 0;

pnal_buf_t * pnal_buf_alloc (uint16_t length)
{
   pnal_buf_t * p = malloc (sizeof (pnal_buf_t) + length);
   if (p != NULL)
   {
      p->payload = (void *)((uint8_t *)p + sizeof (pnal_buf_t));
      p->len     = length;
      pnal_buf_alloc_cnt++;
   }
   return p;
}

void pnal_buf_free (pnal_buf_t * p)
{
   if (p != NULL)
   {
      free (p);
      pnal_buf_alloc_cnt--;
   }
}

uint8_t pnal_buf_header (pnal_buf_t * p, int16_t header_size_increment)
{
   /* Not used on this platform */
   return 255;
}

/* =========================================================================
 * File persistence  (SPIFFS / LittleFS via standard POSIX on ESP-IDF VFS)
 * ========================================================================= */

int pnal_save_file (
   const char * fullpath,
   const void * object_1,
   size_t       size_1,
   const void * object_2,
   size_t       size_2)
{
   FILE * f = fopen (fullpath, "wb");
   if (f == NULL)
   {
      LOG_ERROR (PF_PNAL_LOG, "PNAL: Cannot open %s for write\n", fullpath);
      return -1;
   }
   if (size_1 > 0 && fwrite (object_1, 1, size_1, f) != size_1)
   {
      fclose (f);
      return -1;
   }
   if (size_2 > 0 && fwrite (object_2, 1, size_2, f) != size_2)
   {
      fclose (f);
      return -1;
   }
   fclose (f);
   return 0;
}

void pnal_clear_file (const char * fullpath)
{
   remove (fullpath);
}

int pnal_load_file (
   const char * fullpath,
   void *       object_1,
   size_t       size_1,
   void *       object_2,
   size_t       size_2)
{
   FILE * f = fopen (fullpath, "rb");
   if (f == NULL)
   {
      return -1;
   }
   if (size_1 > 0 && fread (object_1, 1, size_1, f) != size_1)
   {
      fclose (f);
      return -1;
   }
   if (size_2 > 0 && fread (object_2, 1, size_2, f) != size_2)
   {
      fclose (f);
      return -1;
   }
   fclose (f);
   return 0;
}

/* =========================================================================
 * System uptime
 * ========================================================================= */

uint32_t pnal_get_system_uptime_10ms (void)
{
   /* esp_timer_get_time() returns µs since boot */
   return (uint32_t)(esp_timer_get_time() / 10000ULL);
}

/* =========================================================================
 * IP / network helpers
 * ========================================================================= */

static esp_netif_t * find_netif (const char * interface_name)
{
   esp_netif_t * netif = NULL;
   esp_netif_t * it    = NULL;

   while ((it = esp_netif_next_unsafe (it)) != NULL)
   {
      const char * key = esp_netif_get_ifkey (it);
      if (key && strncmp (key, interface_name, strlen (interface_name)) == 0)
      {
         netif = it;
         break;
      }
   }
   return netif;
}

int pnal_get_macaddress (const char * interface_name, pnal_ethaddr_t * mac_addr)
{
   esp_netif_t * netif = find_netif (interface_name);
   if (netif == NULL)
   {
      /* Fallback: read base MAC from eFuse */
      return (esp_efuse_mac_get_default (mac_addr->addr) == ESP_OK) ? 0 : -1;
   }
   return (esp_netif_get_mac (netif, mac_addr->addr) == ESP_OK) ? 0 : -1;
}

pnal_ipaddr_t pnal_get_ip_address (const char * interface_name)
{
   esp_netif_t * netif = find_netif (interface_name);
   if (netif == NULL)
      return PNAL_IPADDR_INVALID;

   esp_netif_ip_info_t ip_info;
   if (esp_netif_get_ip_info (netif, &ip_info) != ESP_OK)
      return PNAL_IPADDR_INVALID;

   return ntohl (ip_info.ip.addr);
}

pnal_ipaddr_t pnal_get_netmask (const char * interface_name)
{
   esp_netif_t * netif = find_netif (interface_name);
   if (netif == NULL)
      return PNAL_IPADDR_INVALID;

   esp_netif_ip_info_t ip_info;
   if (esp_netif_get_ip_info (netif, &ip_info) != ESP_OK)
      return PNAL_IPADDR_INVALID;

   return ntohl (ip_info.netmask.addr);
}

pnal_ipaddr_t pnal_get_gateway (const char * interface_name)
{
   esp_netif_t * netif = find_netif (interface_name);
   if (netif == NULL)
      return PNAL_IPADDR_INVALID;

   esp_netif_ip_info_t ip_info;
   if (esp_netif_get_ip_info (netif, &ip_info) != ESP_OK)
      return PNAL_IPADDR_INVALID;

   return ntohl (ip_info.gw.addr);
}

int pnal_get_hostname (char * hostname)
{
   const char * name = CONFIG_LWIP_LOCAL_HOSTNAME;  /* set in sdkconfig */
   strncpy (hostname, name, PNAL_HOSTNAME_MAX_SIZE - 1);
   hostname[PNAL_HOSTNAME_MAX_SIZE - 1] = '\0';
   return 0;
}

int pnal_get_ip_suite (
   const char *   interface_name,
   pnal_ipaddr_t * p_ipaddr,
   pnal_ipaddr_t * p_netmask,
   pnal_ipaddr_t * p_gw,
   char *          hostname)
{
   *p_ipaddr  = pnal_get_ip_address (interface_name);
   *p_netmask = pnal_get_netmask (interface_name);
   *p_gw      = pnal_get_gateway (interface_name);
   return pnal_get_hostname (hostname);
}

int pnal_set_ip_suite (
   const char *          interface_name,
   const pnal_ipaddr_t * p_ipaddr,
   const pnal_ipaddr_t * p_netmask,
   const pnal_ipaddr_t * p_gw,
   const char *          hostname,
   bool                  permanent)
{
   esp_netif_t * netif = find_netif (interface_name);
   if (netif == NULL)
      return -1;

   esp_netif_ip_info_t cur_info;
   esp_netif_get_ip_info (netif, &cur_info);

   // ✅ p_gw and p_ipaddr are already host byte order — NO htonl here
   uint32_t new_gw = *p_gw;
   uint32_t new_ip = *p_ipaddr;

   if (new_gw == new_ip || new_gw == 0)
   {
      ////////////ESP_LOGW ("pnal", "pnal_set_ip_suite: ignoring bad gw, keeping existing");
      // cur_info.gw.addr is in network byte order, convert back to host
      new_gw = ntohl (cur_info.gw.addr);
   }

   esp_netif_ip_info_t ip_info;
   // ✅ convert to network byte order only here
   ip_info.ip.addr      = htonl (*p_ipaddr);
   ip_info.netmask.addr = htonl (*p_netmask);
   ip_info.gw.addr      = htonl (new_gw);

   esp_netif_dhcpc_stop (netif);
   esp_err_t err = esp_netif_set_ip_info (netif, &ip_info);

   // ////////////ESP_LOGI ("pnal", "set_ip_suite: ip=%lu.%lu.%lu.%lu mask=%lu.%lu.%lu.%lu gw=%lu.%lu.%lu.%lu -> %s",
   //    (*p_ipaddr  >> 24) & 0xFF, (*p_ipaddr  >> 16) & 0xFF,
   //    (*p_ipaddr  >>  8) & 0xFF,  *p_ipaddr  & 0xFF,
   //    (*p_netmask >> 24) & 0xFF, (*p_netmask >> 16) & 0xFF,
   //    (*p_netmask >>  8) & 0xFF,  *p_netmask & 0xFF,
   //    (new_gw     >> 24) & 0xFF, (new_gw     >> 16) & 0xFF,
   //    (new_gw     >>  8) & 0xFF,  new_gw     & 0xFF,
   //    err == ESP_OK ? "OK" : esp_err_to_name(err));

   return (err == ESP_OK) ? 0 : -1;
}

int pnal_get_interface_index (const char * interface_name)
{
   /* netif_find() expects a short lwIP name like "en0" or "et0", not
    * the ESP-IDF ifkey like "ETH_DEF".  Iterate all netifs and return
    * the index of the first link-up interface as a safe fallback. */
   struct netif * nif;
   NETIF_FOREACH (nif)
   {
      if (netif_is_link_up (nif))
         return (int)netif_get_index (nif);
   }
   return 1; /* minimum valid ifIndex per RFC 2863 */
}

/* =========================================================================
 * Ethernet link status
 * ========================================================================= */

int pnal_eth_get_status (
   const char *      interface_name,
   pnal_eth_status_t * status)
{
   /* Report 100 Mbit full-duplex; adapt if you can read the KSZ8863 PHY. */
   status->is_autonegotiation_supported           = true;
   status->is_autonegotiation_enabled             = true;
   status->operational_mau_type                   = PNAL_ETH_MAU_COPPER_100BaseTX_FULL_DUPLEX;
   status->autonegotiation_advertised_capabilities =
      PNAL_ETH_AUTONEG_CAP_100BaseTX_FULL_DUPLEX |
      PNAL_ETH_AUTONEG_CAP_100BaseTX_HALF_DUPLEX |
      PNAL_ETH_AUTONEG_CAP_10BaseT_FULL_DUPLEX   |
      PNAL_ETH_AUTONEG_CAP_10BaseT_HALF_DUPLEX;

   /* Link state: netif_find() needs lwIP short name (e.g. "en0"), but
    * p-net passes the pnet_port_cfg netif_name which may be "ETH_DEF" or
    * similar.  Try netif_find() first; if that fails, fall back to
    * iterating all netifs and returning true if any link is up. */
   struct netif * nif = netif_find (interface_name);
   if (nif == NULL)
   {
      /* Fallback: walk all netifs, pick first link-up one */
      struct netif * n;
      NETIF_FOREACH (n)
      {
         if (netif_is_link_up (n))
         {
            nif = n;
            break;
         }
      }
   }
   status->running = (nif != NULL) && netif_is_link_up (nif);

   return 0;
}

/* =========================================================================
 * Port statistics
 * ========================================================================= */

int pnal_get_port_statistics (
   const char *       interface_name,
   pnal_port_stats_t * port_stats)
{
   /* lwIP does not expose per-netif rx/tx byte counters via the standard API.
    * Return zeroes – the Profinet stack only uses these for diagnostics. */
   memset (port_stats, 0, sizeof (*port_stats));
   return 0;
}

/* =========================================================================
 * Raw Ethernet send/receive  (pnal_eth_*)
 * pnal_eth_init is called by pf_eth_init_netif; the receive callback is
 * invoked from the Ethernet driver ISR/task via pf_eth_recv().
 * ========================================================================= */

struct pnal_eth_handle
{
   char                    ifname[PNET_INTERFACE_NAME_MAX_SIZE]; /* 16, from pnet_api.h */
   pnal_eth_callback_t   * callback;                             /* function pointer */
   void *                  arg;
   esp_eth_handle_t        eth_handle;
};

/* Single global handle – extend to an array if you have multiple ports. */
pnal_eth_handle_t g_eth_handle;

/* lwIP netif for forwarding non-PROFINET frames; set by pnal_eth_set_esp_handle() */
static esp_netif_t * s_eth_netif = NULL;

/* Called by the ESP-IDF Ethernet driver for every received frame.
 *
 * Frames are dispatched by EtherType:
 *   0x8892 (PROFINET RT) and 0x88CC (LLDP) → p-net stack via callback
 *   everything else (ARP=0x0806, IP=0x0800, etc.) → lwIP via saved glue fn
 *
 * This ensures ARP, IP, TCP/UDP continue to work alongside PROFINET.
 */
esp_err_t esp32_eth_recv_cbbb (
   esp_eth_handle_t  eth_handle,
   uint8_t *         buffer,
   uint32_t          length,
   void *            priv)
{
   if (buffer == NULL || length < 14)
   {
      free (buffer);
      return ESP_OK;
   }

   /* EtherType is at byte offset 12-13 in the Ethernet frame */
   uint16_t ethertype = (uint16_t)((buffer[12] << 8) | buffer[13]);

   /* Skip VLAN tag if present (0x8100) */
   if (ethertype == 0x8100 && length >= 18)
   {
      ethertype = (uint16_t)((buffer[16] << 8) | buffer[17]);
   }

   bool is_profinet = (ethertype == 0x8892); /* PROFINET RT      */
   bool is_lldp     = (ethertype == 0x88CC); /* LLDP             */

   if (is_profinet || is_lldp)
   {
      /* Forward to p-net stack */
      pnal_eth_handle_t * handle = (pnal_eth_handle_t *)priv;
      if (handle == NULL || handle->callback == NULL)
      {
         ////////////ESP_LOGW ("pnal", "recv: PROFINET/LLDP frame (0x%04X) but callback not set!", ethertype);
         free (buffer);
         return ESP_OK;
      }
      ////////////ESP_LOGD ("pnal", "recv: -> p-net EtherType=0x%04X len=%lu", ethertype, (unsigned long)length);

      pnal_buf_t * p = malloc (sizeof (pnal_buf_t) + length);
      if (p == NULL)
      {
         free (buffer);
         return ESP_ERR_NO_MEM;
      }
      p->payload = (void *)((uint8_t *)p + sizeof (pnal_buf_t));
      p->len     = (uint16_t)length;
      memcpy (p->payload, buffer, length);
      free (buffer);

      pnal_buf_alloc_cnt++;
      handle->callback (handle, handle->arg, p);
      return ESP_OK;
   }
   else
   {
      /* Forward to lwIP (ARP 0x0806, IP 0x0800, etc.)
       * esp_netif_receive() passes the buffer to the lwIP stack.
       * Note: esp_netif_receive takes ownership of buffer. */
      if (s_eth_netif != NULL)
      {
         ////////////ESP_LOGD ("pnal", "recv: -> lwIP EtherType=0x%04X len=%lu", ethertype, (unsigned long)length);
         return esp_netif_receive (s_eth_netif, buffer, length, NULL);
      }
      ////////////ESP_LOGW ("pnal", "recv: non-PROFINET frame (0x%04X) dropped, s_eth_netif=NULL!", ethertype);
      free (buffer);
      return ESP_OK;
   }
}
esp_err_t esp32_eth_recv_cb (
   esp_eth_handle_t  eth_handle,
   uint8_t *         buffer,
   uint32_t          length,
   void *            priv)
{
   if (buffer == NULL || length < 14)
   {
      free(buffer);
      return ESP_OK;
   }

   /* Extract EtherType */
   uint16_t ethertype = (uint16_t)((buffer[12] << 8) | buffer[13]);

   /* Handle VLAN tagged frames */
   if (ethertype == 0x8100 && length >= 18)
   {
      ethertype = (uint16_t)((buffer[16] << 8) | buffer[17]);
   }

   /* =========================
    * DEBUG LOGGING
    * ========================= */
   ////////////ESP_LOGI("ETH", "Frame received: Ethertype=0x%04X len=%lu",
          //  ethertype, (unsigned long)length);

   if (ethertype == 0x0806)
   {
      ////////////ESP_LOGI("ETH", ">>> ARP FRAME");
   }
   else if (ethertype == 0x0800)
   {
      ////////////ESP_LOGI("ETH", ">>> IP FRAME");
   }
   else if (ethertype == 0x8892)
   {
      uint16_t frame_id = 0;
      if (length >= 16)
      {
         frame_id = (uint16_t)((buffer[14] << 8) | buffer[15]);
      }
      ////////////ESP_LOGI("ETH", ">>> PROFINET FRAME (FrameID=0x%04X)", frame_id);

      if (frame_id == 0xFEFE)
      {
         ////////////ESP_LOGI("ETH", ">>> DCP Identify");
      }
      else if (frame_id == 0xFEFD)
      {
         ////////////ESP_LOGI("ETH", ">>> DCP Set");
      }
   }
   else if (ethertype == 0x88CC)
   {
      ////////////ESP_LOGI("ETH", ">>> LLDP FRAME");
   }

   /* =========================
    * PROFINET / LLDP handling
    * ========================= */
   bool is_profinet = (ethertype == 0x8892);
   bool is_lldp     = (ethertype == 0x88CC);

   if (is_profinet || is_lldp)
   {
      pnal_eth_handle_t * handle = (pnal_eth_handle_t *)priv;

      if (handle == NULL || handle->callback == NULL)
      {
         ////////////ESP_LOGW("ETH", "Callback missing, dropping frame");
         free(buffer);
         return ESP_OK;
      }

      pnal_buf_t * p = malloc(sizeof(pnal_buf_t) + length);
      if (p == NULL)
      {
         ////////////ESP_LOGE("ETH", "Memory allocation failed");
         free(buffer);
         return ESP_ERR_NO_MEM;
      }

      p->payload = (void *)((uint8_t *)p + sizeof(pnal_buf_t));
      p->len     = (uint16_t)length;

      memcpy(p->payload, buffer, length);
      free(buffer);

      pnal_buf_alloc_cnt++;

      ////////////ESP_LOGI("ETH", "Forwarding to PROFINET stack");

      handle->callback(handle, handle->arg, p);
      return ESP_OK;
   }

   /* =========================
    * LWIP forwarding (ARP/IP)
    * ========================= */
   if (s_eth_netif != NULL)
   {
      ////////////ESP_LOGI("ETH", "Forwarding to LWIP (0x%04X)", ethertype);
      return esp_netif_receive(s_eth_netif, buffer, length, NULL);
   }

   ////////////ESP_LOGE("ETH", "LWIP netif NULL → dropping frame!");
   free(buffer);
   return ESP_OK;
}

pnal_eth_handle_t * pnal_eth_init (
   const char *          if_name,
   pnal_ethertype_t      receive_type,
   const pnal_cfg_t *    pnal_cfg,
   pnal_eth_callback_t * callback,
   void *                arg)
{
   (void)receive_type; /* we receive all frame types via esp_eth_update_input_path */
   (void)pnal_cfg;     /* thread config handled by the p-net stack */

   pnal_eth_handle_t * handle = &g_eth_handle;
   strncpy (handle->ifname, if_name, sizeof (handle->ifname) - 1);
   handle->ifname[sizeof (handle->ifname) - 1] = '\0';
   handle->callback = callback;
   handle->arg      = arg;
   /* Do NOT reset eth_handle here: pnal_eth_set_esp_handle() may have already
    * set it before pnet_init() called us. If not yet set, it will be set
    * by pnal_eth_set_esp_handle() immediately after pnet_init() returns. */

   return handle;
}

int pnal_eth_send (pnal_eth_handle_t * handle, pnal_buf_t * buf)
{
   if (handle == NULL || handle->eth_handle == NULL || buf == NULL)
   {
      ////////////ESP_LOGW ("pnal", "pnal_eth_send: DROPPED - handle=%p eth_handle=%p buf=%p",
            //    handle, handle ? handle->eth_handle : NULL, buf);
      return -1;
   }
   ////////////ESP_LOGD ("pnal", "pnal_eth_send: TX %u bytes", buf->len);
   /* esp_eth_transmit makes its own copy, so we pass the payload directly. */
   esp_err_t err = esp_eth_transmit (
      handle->eth_handle,
      buf->payload,
      buf->len);

 return (err == ESP_OK) ? (int)buf->len : -1;
}

/**
 * Called from profinet.cpp after esp_eth_start() to wire the ESP-IDF
 * Ethernet driver to the pnal receive path.
 *
 * This is the ONLY function in pnal_esp32.c that needs to be called from
 * application code; it keeps struct pnal_eth_handle opaque to callers.
 *
 * @param esp_handle   The handle returned by esp_eth_driver_install().
 */
void pnal_eth_set_esp_handle (esp_eth_handle_t esp_handle)
{
   g_eth_handle.eth_handle = esp_handle;

   /* Find the netif attached to this eth handle so we can forward
    * non-PROFINET frames (ARP, IP) back to lwIP. */
   esp_netif_t * netif = NULL;
   while ((netif = esp_netif_next_unsafe (netif)) != NULL)
   {
      /* The netif attached via esp_eth_new_netif_glue has ifkey "ETH_DEF" */
      const char * key = esp_netif_get_ifkey (netif);
      if (key && strcmp (key, "ETH_DEF") == 0)
      {
         s_eth_netif = netif;
         break;
      }
      if (key && strcmp (key, "eth0") == 0)
      {
        
         s_eth_netif = netif;
         break;
      }
   }

   esp_eth_update_input_path (esp_handle, esp32_eth_recv_cb, &g_eth_handle);
}

/* =========================================================================
 * UDP sockets  (uses LwIP sockets via ESP-IDF VFS)
 * ========================================================================= */

int pnal_udp_open (pnal_ipaddr_t addr, pnal_ipport_t port)
{
   int sock = socket (AF_INET, SOCK_DGRAM, IPPROTO_UDP);
   if (sock < 0)
   {
      ////////////ESP_LOGE ("pnal", "pnal_udp_open: socket() failed for port %u: %d", port, errno);
      return -1;
   }
   ////////////ESP_LOGI ("pnal", "pnal_udp_open: socket %d bound to port %u", sock, port);

   int broadcast = 1;
   setsockopt (sock, SOL_SOCKET, SO_BROADCAST, &broadcast, sizeof (broadcast));

   int reuse = 1;
   setsockopt (sock, SOL_SOCKET, SO_REUSEADDR, &reuse, sizeof (reuse));

   /* Increase socket receive buffer for RPC packets (default 4K is too small) */
   int rcvbuf = 65536;
   setsockopt (sock, SOL_SOCKET, SO_RCVBUF, &rcvbuf, sizeof (rcvbuf));

   struct sockaddr_in sa;
   memset (&sa, 0, sizeof (sa));
   sa.sin_family      = AF_INET;
   sa.sin_addr.s_addr = htonl (addr);
   sa.sin_port        = htons (port);

   if (bind (sock, (struct sockaddr *)&sa, sizeof (sa)) < 0)
   {
      close (sock);
      return -1;
   }
   return sock;
}

int pnal_udp_sendto (
   uint32_t          id,
   pnal_ipaddr_t     dst_addr,
   pnal_ipport_t     dst_port,
   const uint8_t *   data,
   int               size)
{
   struct sockaddr_in dest;
   memset (&dest, 0, sizeof (dest));
   dest.sin_family      = AF_INET;
   dest.sin_addr.s_addr = htonl (dst_addr);
   dest.sin_port        = htons (dst_port);

   int ret = sendto (
      (int)id,
      data,
      size,
      0,
      (struct sockaddr *)&dest,
      sizeof (dest));

   ////////////ESP_LOGI ("pnal", "pnal_udp_sendto: sock=%d -> port %u size=%d ret=%d errno=%d",
           //  (int)id, dst_port, size, ret, ret < 0 ? errno : 0);
   return ret;
}

int pnal_udp_recvfrom (
   uint32_t        id,
   pnal_ipaddr_t * src_addr,
   pnal_ipport_t * src_port,
   uint8_t *       data,
   int             size)
{
   struct sockaddr_in src;
   socklen_t          src_len = sizeof (src);

   int ret = recvfrom (
      (int)id,
      data,
      size,
      MSG_DONTWAIT,
      (struct sockaddr *)&src,
      &src_len);

   if (ret > 0)
   {
      if (src_addr)
         *src_addr = ntohl (src.sin_addr.s_addr);
      if (src_port)
         *src_port = ntohs (src.sin_port);
      ////////////ESP_LOGD ("pnal", "pnal_udp_recvfrom: sock=%d got %d bytes from port %u",
             //   (int)id, ret, ntohs (src.sin_port));
   }
   return ret;
}

void pnal_udp_close (uint32_t id)
{
   close ((int)id);
}
void pnal_apply_ip_to_lwip(pnal_ipaddr_t ip, pnal_ipaddr_t mask, pnal_ipaddr_t gw)
{
    if (s_eth_netif == NULL) return;
    
    esp_netif_ip_info_t ip_info;
    ip_info.ip.addr      = htonl(ip);
    ip_info.netmask.addr = htonl(mask);
    ip_info.gw.addr      = htonl(gw);
    
    esp_netif_dhcpc_stop(s_eth_netif);
    esp_netif_set_ip_info(s_eth_netif, &ip_info);
    
   //  ////////////ESP_LOGI("pnal", "IP applied to lwIP: %d.%d.%d.%d",
   //           (ip>>24)&0xFF, (ip>>16)&0xFF, (ip>>8)&0xFF, ip&0xFF);
}

/* =========================================================================
 * SNMP  – not supported on ESP32; stub out to avoid linker error.
 * pnet_api.c calls pnal_snmp_init(); we simply return 0.
 * ========================================================================= */

int pnal_snmp_init (pnet_t * pnet, const pnal_cfg_t * pnal_cfg)
{
   (void)pnet;
   (void)pnal_cfg;
   LOG_INFO (PF_PNAL_LOG, "PNAL: SNMP not supported on ESP32, skipping.\n");
   return 0;
}
