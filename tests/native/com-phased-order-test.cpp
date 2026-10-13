#include "../../tools/KuaiYi.ComCaptureProbe/CandidateOrder.h"
#include <cstdlib>
#include <iostream>
#include <stdexcept>
namespace p=kuaiyi::policy;
int checks=0;void require(bool v){if(!v)std::abort();++checks;}
struct fixture {
    int focus_calls=0,hit_calls=0,bfs_calls=0,tick=0,winner=-1,terminal=0;
    int supplier_cost=0,eval_cost=0;
    std::vector<int> focus{10,11},hit{20,21},events;
    std::optional<int> run(){return p::evaluate_focus_then_hit<int>(1,
        [&]{++focus_calls;tick+=supplier_cost;events.push_back(-1);return focus;},
        [&]{++hit_calls;events.push_back(-2);return hit;},
        [&](int n)->std::optional<int>{events.push_back(n);tick+=eval_cost;if(terminal)return terminal;return n==winner?std::optional<int>(n):std::nullopt;},
        [&](int n,auto& q,std::size_t visited){++bfs_calls;for(int next:{n*2,n*2+1})if(q.size()+visited<64)q.push_back(next);},
        [](int,p::candidate_source){},[&](p::timeout_check){return p::soft_expired(tick);});}
};
int main(){
    fixture focus;focus.winner=10;require(focus.run()==10);require(focus.focus_calls==1&&focus.hit_calls==0&&focus.bfs_calls==0);require(focus.events==std::vector<int>({-1,10}));
    fixture hit;hit.winner=20;require(hit.run()==20);require(hit.focus_calls==1&&hit.hit_calls==1&&hit.bfs_calls==0);require(hit.events==std::vector<int>({-1,10,11,-2,20}));
    for(int terminal:{-1,-2,-3,-4}){fixture reject;reject.terminal=terminal;require(reject.run()==terminal);require(reject.hit_calls==0&&reject.bfs_calls==0);}
    fixture bfs;bfs.winner=4;require(bfs.run()==4);require(bfs.focus_calls==1&&bfs.hit_calls==1&&bfs.bfs_calls==3);require(bfs.events==std::vector<int>({-1,10,11,-2,20,21,1,2,3,4}));
    fixture limit;require(!limit.run());require(limit.events.size()==66&&limit.focus_calls==1&&limit.hit_calls==1&&limit.bfs_calls==59);
    fixture full;full.focus=std::vector<int>(64,10);require(!full.run());require(full.hit_calls==0&&full.bfs_calls==0&&full.events.size()==65);
    fixture supply;supply.supplier_cost=250;require(!supply.run());require(supply.hit_calls==0&&supply.bfs_calls==0&&supply.events==std::vector<int>{-1});
    fixture shared;shared.eval_cost=125;require(!shared.run());require(shared.hit_calls==0&&shared.bfs_calls==0&&shared.tick==250);
    int hit_called=0;
    try{(void)p::evaluate_focus_then_hit<int>(1,[]{return std::vector<int>{10};},[&]{++hit_called;return std::vector<int>{20};},[](int)->std::optional<int>{throw std::runtime_error("synthetic");},[](int,auto&,std::size_t){},[](int,p::candidate_source){},[](p::timeout_check){return false;});require(false);}catch(const std::runtime_error&){require(hit_called==0);}
    std::cout<<"COM_PHASED_ORDER passed="<<checks<<" failed=0; portable production scheduler, not Windows UIA proof\n";
}
