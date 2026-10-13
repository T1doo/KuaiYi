#pragma once
#include "SelectionPolicy.h"
#include <deque>
#include <optional>
#include <span>
#include <vector>
namespace kuaiyi::policy {
enum class candidate_source {none=0,focus=1,hit=2,bfs=3};
enum class timeout_check {none=0,focus_ancestors=1,hit_ancestors=2,before_candidate=3,before_range=4,final_identity=5,after_runtime=6,after_candidate=7,before_bfs=8,bfs_expansion=9,final_empty=10,finish_snapshot=11,before_copy=12};
// A path is admitted only after reaching the original root; partial/cross-process paths are discarded.
template<class Candidate, class Owned, class Equal, class Parent, class Expired>
std::vector<Candidate> bound_ancestors(Candidate element, const Candidate& root,
    Owned owned, Equal equal, Parent parent, Expired expired) {
    std::vector<Candidate> path;
    for(int level=0;level<8&&element&&!expired();++level) {
        if(!owned(element))break;
        path.push_back(element);
        if(equal(element,root))return path;
        element=parent(element);
    }
    return {};
}
// No BFS provider call occurs until all admitted ancestors have failed evaluation.
// Callbacks share the request's original clock and evaluator; this scheduler has no retry or new budget.
template<class Result, class Candidate, class Evaluate, class Expand, class Record, class Expired>
std::optional<Result> evaluate_ordered(std::span<const Candidate> ancestors, const Candidate& root,
    Evaluate evaluate, Expand expand, Record record, Expired expired) {
    const auto initial=ancestors.size();
    if(initial>static_cast<std::size_t>(candidate_limit))return std::nullopt;
    for(const auto& candidate:ancestors) {
        if(expired())return std::nullopt;
        if(auto result=evaluate(candidate))return result;
    }
    if(expired())return std::nullopt;
    std::deque<Candidate> queue;
    queue.push_back(root);
    auto visited=initial;
    while(!queue.empty()&&visited<static_cast<std::size_t>(candidate_limit)&&!expired()) {
        auto candidate=queue.front();queue.pop_front();
        ++visited;record(candidate);
        if(auto result=evaluate(candidate))return result;
        if(visited<static_cast<std::size_t>(candidate_limit)&&!expired())expand(candidate,queue,visited);
    }
    return std::nullopt;
}
// Exactly one search: focus binding/evaluation, then lazy hit binding/evaluation, then one BFS.
template<class Result,class Candidate,class Focus,class Hit,class Evaluate,class Expand,class Record,class Expired>
std::optional<Result> evaluate_focus_then_hit(const Candidate& root,Focus focus,Hit hit,
    Evaluate evaluate,Expand expand,Record record,Expired expired) {
    std::size_t visited=0;
    for(int phase=1;phase<=2;++phase){
        if(visited>=static_cast<std::size_t>(candidate_limit)||expired(phase==1?timeout_check::focus_ancestors:timeout_check::hit_ancestors))return std::nullopt;
        auto path=phase==1?focus():hit();
        for(const auto& candidate:path){
            if(visited>=static_cast<std::size_t>(candidate_limit)||expired(timeout_check::before_candidate))return std::nullopt;
            ++visited;record(candidate,phase==1?candidate_source::focus:candidate_source::hit);
            if(auto found=evaluate(candidate))return found;
        }
    }
    if(visited>=static_cast<std::size_t>(candidate_limit)||expired(timeout_check::before_bfs))return std::nullopt;
    std::deque<Candidate> queue;queue.push_back(root);
    while(!queue.empty()&&visited<static_cast<std::size_t>(candidate_limit)&&!expired(timeout_check::before_candidate)){
        auto candidate=queue.front();queue.pop_front();++visited;record(candidate,candidate_source::bfs);
        if(auto found=evaluate(candidate))return found;
        if(visited<static_cast<std::size_t>(candidate_limit)&&!expired(timeout_check::bfs_expansion))expand(candidate,queue,visited);
    }
    return std::nullopt;
}
}
