#include "../../src/KuaiYi.CaptureNative/GdiReadContract.hpp"
#include <stdexcept>
#include <vector>
#include <thread>
#include <iostream>
void require(bool condition) { if (!condition) throw std::runtime_error("GDI ordering contract failed"); }
int main() try {
    int cases=0;
    for (int scenario=0;scenario<5;scenario++) {
        auto owner=std::this_thread::get_id();bool drawing=true;std::vector<int> events;
        auto flush=[&] { require(owner==std::this_thread::get_id());events.push_back(1);if(scenario==1)return false;drawing=false;return true; };
        auto cancel=[&] { require(!drawing);events.push_back(2);return scenario==2; };
        auto read=[&] { require(!drawing);require(owner==std::this_thread::get_id());events.push_back(3); };
        int code=kuaiyi::ReadGdiPixels(scenario!=3,flush,cancel,read);
        if(scenario==0||scenario==4){require(code==0);require((events==std::vector<int>{1,2,3}));}
        if(scenario==1){require(code==5);require((events==std::vector<int>{1}));}
        if(scenario==2){require(code==2);require((events==std::vector<int>{1,2}));}
        if(scenario==3){require(code==5);require((events==std::vector<int>{1}));}
        cases++;
    }
    for(bool success:{false,true}) {
        bool drawing=true,cleared=false;auto owner=std::this_thread::get_id();
        bool result=kuaiyi::ClearGdiPixels([&]{require(owner==std::this_thread::get_id());if(success)drawing=false;return success;},[&]{require(!drawing);require(owner==std::this_thread::get_id());cleared=true;});
        require(result==success);require(cleared==success);cases++;
    }
    std::cout<<"GDI portable contract: "<<cases<<" passed; synthetic flush/drawing only, no Windows GDI claim.\n";
} catch (const std::exception& error) { std::cerr<<error.what()<<"\n"; return 3; }
