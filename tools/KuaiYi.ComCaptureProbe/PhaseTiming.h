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
    int active_source=0;
    std::array<std::int64_t,7> progress{0,0,0,-2,-2,-2,-2};
    phase_timings(bool active,Clock read):enabled(active),clock(read){values.fill(-2);}
    void candidate(int source){if(enabled){progress[2]=source;for(std::size_t i=3;i<7;++i)progress[i]=-2;}}
    void ranges(int total){if(enabled){progress[3]=total>=0&&total<=64?total:-2;progress[4]=total>=0&&total<=64?0:-2;}}
    void range_complete(){if(enabled&&progress[4]>=0&&progress[4]<progress[3])++progress[4];}
    void text_start(){if(enabled){progress[5]=bounded(clock());progress[6]=-2;}}
    void text_end(){if(enabled)progress[6]=bounded(clock());}
    void timeout(int checkpoint){if(enabled&&progress[0]==0){progress[0]=checkpoint;progress[1]=active_source;}}
    class evaluation {
        phase_timings& owner;
    public:
        explicit evaluation(phase_timings& trace):owner(trace){if(owner.enabled)owner.active_source=static_cast<int>(owner.progress[2]);}
        evaluation(const evaluation&)=delete;
        ~evaluation(){owner.active_source=0;}
    };
    evaluation evaluating(){return evaluation(*this);}
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
