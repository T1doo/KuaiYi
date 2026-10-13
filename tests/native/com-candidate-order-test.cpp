#include "../../tools/KuaiYi.ComCaptureProbe/CandidateOrder.h"
#include <cstdlib>
#include <iostream>
#include <stdexcept>
#include <memory>
namespace p=kuaiyi::policy;
int checks=0;
void require(bool valid){if(!valid)std::abort();++checks;}
struct fixture {
    std::vector<int> evaluated,expanded,recorded;
    int tick=0,step=0,winner=-1;
    bool current=true,password=false;
    std::optional<int> run(const std::vector<int>& ancestors) {
        return p::evaluate_ordered<int>(std::span<const int>(ancestors),1,
            [&](int node)->std::optional<int>{evaluated.push_back(node);tick+=step;if(!current||password)return -2;return node==winner?std::optional<int>(node):std::nullopt;},
            [&](int node,auto& queue,std::size_t visited){expanded.push_back(node);for(int next:{node*2,node*2+1})if(queue.size()+visited<static_cast<std::size_t>(p::candidate_limit))queue.push_back(next);},
            [&](int node){recorded.push_back(node);},[&]{return p::soft_expired(tick);});
    }
};
int main(){
    fixture early;early.winner=10;require(early.run({10,11,20,21})==10);require(early.evaluated==std::vector<int>{10});require(early.expanded.empty()&&early.recorded.empty());
    fixture last;last.winner=21;require(last.run({10,11,20,21})==21);require(last.evaluated==std::vector<int>({10,11,20,21}));require(last.expanded.empty());
    fixture bfs;bfs.winner=4;require(bfs.run({10,11,20,21})==4);require(bfs.evaluated==std::vector<int>({10,11,20,21,1,2,3,4}));require(bfs.expanded==std::vector<int>({1,2,3}));require(bfs.recorded==std::vector<int>({1,2,3,4}));
    fixture exhausted;require(!exhausted.run({10,11,20,21}));require(exhausted.evaluated.size()==64);require(exhausted.recorded.size()==60);require(exhausted.expanded.size()==59);
    fixture full;require(!full.run(std::vector<int>(64,9)));require(full.evaluated.size()==64&&full.expanded.empty());
    fixture invalid;require(!invalid.run(std::vector<int>(65,9)));require(invalid.evaluated.empty()&&invalid.expanded.empty());
    fixture budget;budget.step=125;require(!budget.run({10,11,20,21}));require(budget.evaluated==std::vector<int>({10,11}));require(budget.tick==250&&budget.expanded.empty());
    fixture shared;shared.tick=200;shared.step=20;require(!shared.run({10,11}));require(shared.evaluated==std::vector<int>({10,11,1}));require(shared.tick==260&&shared.expanded.empty());
    fixture cancelled;cancelled.tick=250;require(!cancelled.run({10}));require(cancelled.evaluated.empty()&&cancelled.expanded.empty());
    fixture sensitive;sensitive.password=true;require(sensitive.run({10,11})==-2);require(sensitive.evaluated.size()==1&&sensitive.expanded.empty());
    fixture stale;stale.current=false;require(stale.run({10,11})==-2);require(stale.evaluated.size()==1&&stale.expanded.empty());
    for(int terminal:{-3,-4,-5}) {
        int expanded=0,evaluated=0;
        auto stopped=p::evaluate_ordered<int>(std::span<const int>(std::array<int,2>{10,11}),1,
            [&](int)->std::optional<int>{++evaluated;return terminal;},[&](int,auto&,std::size_t){++expanded;},[](int){},[]{return false;});
        require(stopped==terminal&&evaluated==1&&expanded==0); // TooLarge/SoftTimeout/protected terminal result.
    }
    auto move_only=p::evaluate_ordered<std::unique_ptr<int>>(std::span<const int>(std::array<int,1>{8}),1,
        [](int)->std::optional<std::unique_ptr<int>>{return std::make_unique<int>(8);},[](int,auto&,std::size_t){},[](int){},[]{return false;});
    require(move_only&&**move_only==8);
    p::soft_observation soft;require(!soft.observe(249,7));require(soft.stage_or(19)==19);require(soft.observe(250,8));require(soft.observe(400,19));require(soft.stage_or(19)==8);
    auto parent=[](int node){return node/2;};auto equal=[](int a,int b){return a==b;};auto owned=[](int){return true;};auto fresh=[](){return false;};
    require(p::bound_ancestors(8,1,owned,equal,parent,fresh)==std::vector<int>({8,4,2,1}));
    require(p::bound_ancestors(8,3,owned,equal,parent,fresh).empty()); // Same process, different HWND root.
    require(p::bound_ancestors(8,1,[](int n){return n!=2;},equal,parent,fresh).empty());
    require(p::bound_ancestors(256,1,owned,equal,parent,fresh).empty()); // Root at ninth level is not admitted.
    require(p::bound_ancestors(128,1,owned,equal,parent,fresh).size()==8);
    int calls=0;require(p::bound_ancestors(8,1,owned,equal,parent,[&]{return ++calls==3;}).empty());
    int child_calls=0;
    try{(void)p::evaluate_ordered<int>(std::span<const int>(std::array<int,1>{8}),1,
        [](int)->std::optional<int>{throw std::runtime_error("synthetic");},
        [&](int,auto&,std::size_t){++child_calls;},[](int){},fresh);require(false);}
    catch(const std::runtime_error&){require(child_calls==0);}
    std::cout<<"COM_CANDIDATE_ORDER passed="<<checks<<" failed=0; portable production scheduler, not Windows UIA proof\n";
}
