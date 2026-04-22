#include "networktools.h"

#include <cstring>
#include <string>
#include <map>

#include "esp_netif.h"
#include "lwip/ip4_addr.h"

namespace profinet
{
namespace tools
{

std::map<std::string, NetworkInterface> GetNetworkInterfaces()
{
   std::map<std::string, NetworkInterface> result;

   esp_netif_t * netif = NULL;
   while ((netif = esp_netif_next_unsafe (netif)) != NULL)
   {
      const char * key = esp_netif_get_ifkey (netif);
      if (key == nullptr)
         continue;

      esp_netif_ip_info_t ip_info;
      if (esp_netif_get_ip_info (netif, &ip_info) != ESP_OK)
         continue;

      NetworkInterface iface;
      iface.name       = std::string (key);
      iface.ipRaw      = ntohl (ip_info.ip.addr);
      iface.maskRaw    = ntohl (ip_info.netmask.addr);
      iface.gatewayRaw = ntohl (ip_info.gw.addr);

      // Format as dotted-decimal strings
      char buf[20];
      esp_ip4addr_ntoa (&ip_info.ip,      buf, sizeof (buf));
      iface.ip = buf;
      esp_ip4addr_ntoa (&ip_info.netmask, buf, sizeof (buf));
      iface.mask = buf;
      esp_ip4addr_ntoa (&ip_info.gw,      buf, sizeof (buf));
      iface.gateway = buf;

      result[iface.name] = iface;
   }

   return result;
}

} // namespace tools
} // namespace profinet
