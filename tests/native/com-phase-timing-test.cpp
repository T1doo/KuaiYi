#include "../../tools/KuaiYi.ComCaptureProbe/PhaseTiming.h"
#include <cstdlib>
#include <iostream>
#include <vector>
namespace p=kuaiyi::policy;
int checks=0;
void require(bool value){if(!value)std::abort();++checks;}
int main(){
    std::int64_t now=0;int reads=0;auto clock=[&]{++reads;return now;};
    p::phase_timings off(false,clock);{auto span=off.observe(0);now=5;span.end();}require(reads==0);for(auto value:off.values)require(value==-2);
    p::phase_timings on(true,clock);{auto span=on.observe(0);now=200;span.end();span.end();}require(reads==2);require(on.values[0]==5&&on.values[1]==200);require(on.values[2]==-2);
    now=700;{auto span=on.observe(1);now=900;}require(on.values[2]==-1&&on.values[3]==-1);
    now=-1;{auto span=on.observe(2);now=0;}require(on.values[4]==-1&&on.values[5]==0);
    auto run=[](bool enabled,bool rejected){
        std::int64_t tick=0;std::vector<int> calls;p::phase_timings trace(enabled,[&]{return tick;});
        {auto span=trace.observe(0);calls.push_back(0);tick=20;if(rejected)return calls;}
        {auto span=trace.observe(1);calls.push_back(1);tick=240;}
        {auto span=trace.observe(2);calls.push_back(2);tick=251;}
        return calls;
    };
    require(run(false,false)==run(true,false));require(run(false,true)==run(true,true));
    require(run(true,true)==std::vector<int>{0});
    std::cout<<"COM_PHASE_TIMING passed="<<checks<<" failed=0; observer order/clock-only contracts, not Windows UIA proof\n";
}
