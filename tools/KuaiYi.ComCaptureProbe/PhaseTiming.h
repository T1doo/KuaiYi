#pragma once
#include <array>
#include <cstdint>
namespace kuaiyi::policy {
// Diagnostic-only observation. No deadline decision, provider call or persistent storage.
template<class Clock> class phase_timings {
    bool enabled;
    Clock clock;
    static std::int64_t bounded(std::int64_t value){return value>=0&&value<=600?value:-1;}
public:
    std::array<std::int64_t,18> values;
    phase_timings(bool active,Clock read):enabled(active),clock(read){values.fill(-2);}
    class scope {
        phase_timings& owner;int index;bool active;
    public:
        scope(phase_timings& trace,int phase):owner(trace),index(phase),active(trace.enabled&&phase>=0&&phase<9){if(active)owner.values[static_cast<std::size_t>(index)*2]=bounded(owner.clock());}
        scope(const scope&)=delete;
        void end(){if(active){owner.values[static_cast<std::size_t>(index)*2+1]=bounded(owner.clock());active=false;}}
        ~scope(){end();}
    };
    scope observe(int phase){return scope(*this,phase);}
};
}
