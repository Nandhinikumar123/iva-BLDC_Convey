#ifndef PROFINETPROPERTIES_HPP
#define PROFINETPROPERTIES_HPP

#pragma once

//#include <cstdint>
#include <string>
#include <vector>

namespace profinet
{
    struct ProfinetProperties
    {
        uint32_t snmpThreadPriority{1};
        size_t snmpThreadStacksize{256 * 1024};

        uint32_t ethThreadPriority{10};
        size_t ethThreadStacksize{4096};

        uint32_t bgWorkerThreadPriority{5};
        size_t bgWorkerThreadStacksize{4096};

        uint32_t cycleTimerPriority{30};
        uint32_t cycleWorkerPriority{15};
        uint32_t cycleTimeUs = 1000;

        std::string pathStorageDirectory{""};
        std::string mainNetworkInterface{"eth0"};
        std::vector<std::string> networkInterfaces{};
    };
}

#endif