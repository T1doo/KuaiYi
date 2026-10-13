#include "../../tools/KuaiYi.SelectionReuseNative/RequestLifetime.h"
#include <vector>
#include <iostream>
#include <cstdlib>
namespace r=kuaiyi::reuse;
int checks=0,wipes=0,releases=0;std::vector<int> order;
void require(bool yes){if(!yes)std::abort();++checks;}
void erased(const void* data,std::size_t size){auto p=static_cast<const unsigned char*>(data);for(std::size_t i=0;i<size;i++)if(p[i]!=0)std::abort();++wipes;order.push_back(1);}
struct fake_interface {
    int refs=1;
    void AddRef()noexcept{++refs;}
    void Release()noexcept{--refs;++releases;order.push_back(2);}
};
void produce(fake_interface* value,fake_interface** output){*output=value;}
int main(){
    r::erased_for_test=erased;
    for(int path:{0,1,2}){
        r::request_scope scope;fake_interface root,focus,hit,pattern,range,cache;
        try{
            auto operation=[&]{
            r::request_com<fake_interface> a,b,c,d,e,f;
            *a.GetAddressOf()=&root;*b.GetAddressOf()=&focus;*c.GetAddressOf()=&hit;*d.GetAddressOf()=&pattern;*e.GetAddressOf()=&range;*f.GetAddressOf()=&cache;
            r::request_wstring text(L"private synthetic text");r::request_bytes encoded("synthetic bytes");
            require(text.capacity()>=64&&encoded.capacity()>=64);
            auto copy=text.substr(1);auto moved=std::move(copy);moved+=L'x';
            unsigned char bytes[32];std::fill_n(bytes,32,7);r::owned_buffer own(bytes,32);
            require(!scope.state.settled());
            if(path==1)throw std::runtime_error("injected");
            if(path==2){require(!scope.state.settled());return;} // Early timeout return must release before quiescence.
            };operation();
        }catch(const std::runtime_error&){}
        require(scope.state.settled());require(root.refs==0&&range.refs==0&&cache.refs==0);
        require(wipes>0&&releases>=6);
    }
    {
        r::request_scope scope;fake_interface root;
        {r::request_com<fake_interface> a;*a.GetAddressOf()=&root;auto b=a;auto c=std::move(b);require(root.refs==2);require(!scope.state.settled());}
        require(root.refs==0);require(scope.state.settled());
    }
    {
        r::request_scope scope;{r::request_wstring text(L"short");auto moved=std::move(text);text=L"again";require(text.capacity()>=64);require(moved==L"short");}
        require(scope.state.settled());
    }
    {r::request_scope scope;fake_interface value;{r::request_com<fake_interface> holder;produce(&value,holder.GetAddressOf());require(holder.Get()==&value);bool rejected=false;try{holder.GetAddressOf();}catch(const std::logic_error&){rejected=true;}require(rejected);}require(value.refs==0);require(scope.state.settled());}
    {r::request_scope scope;scope.state.clean=false;require(!scope.state.settled());}
    require(order.size()>=4);require(order[0]==1); // Buffers/text zero before later interface release.
    std::cout<<"REUSE_REQUEST_LIFETIME passed="<<checks<<" failed=0; injected owners/zeroization, Windows COM NotRun\n";
}
