#pragma once
#include <array>
#include <algorithm>
#include <cstdint>
#include <cstring>
namespace kuaiyi::lifecycle {
using packet=std::array<unsigned char,64>;
enum class mode:unsigned char {normal=1,bound_death=2,before_bind=3,watchdog=4};
enum class phase:unsigned char {started=1,bound=2,authorize=3,initialized=4,local_released=5,quiescent=6,final_released=7,parked=8,parent_settled=9,init_failed=10,soft_timeout=11};
template<class T> T field(const packet& p,std::size_t offset){T value{};std::memcpy(&value,p.data()+offset,sizeof(T));return value;}
template<class T> void put(packet& p,std::size_t offset,T value){std::memcpy(p.data()+offset,&value,sizeof(T));}
inline bool valid(const packet& p){
    if(field<std::uint32_t>(p,0)!=0x434c594b||p[4]!=1||p[5]<1||p[5]>11||p[6]>2||p[7]<1||p[7]>4||field<std::int64_t>(p,8)<0||field<int>(p,32)<=0||field<std::int64_t>(p,36)<=0||field<int>(p,44)<0||field<int>(p,44)>600)return false;
    return std::any_of(p.begin()+16,p.begin()+32,[](unsigned char c){return c!=0;})&&std::all_of(p.begin()+48,p.end(),[](unsigned char c){return c==0;});
}
inline packet make(phase stage,unsigned char sequence,mode scenario,std::int64_t origin,const std::array<unsigned char,16>& nonce,int pid,std::int64_t creation,int age){
    packet p{};put<std::uint32_t>(p,0,0x434c594b);p[4]=1;p[5]=static_cast<unsigned char>(stage);p[6]=sequence;p[7]=static_cast<unsigned char>(scenario);put(p,8,origin);std::copy(nonce.begin(),nonce.end(),p.begin()+16);put(p,32,pid);put(p,36,creation);put(p,44,age);return p;
}
}
