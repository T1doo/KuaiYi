#pragma once
#include "SelectionPolicy.h"
#include <deque>
#include <optional>
#include <span>
#include <vector>
namespace kuaiyi::policy {
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
}
