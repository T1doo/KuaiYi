#pragma once
#include <array>
#include <cstdint>
#include <cstring>
#include <bit>
#include <algorithm>
namespace kuaiyi::reuse {
using packet=std::array<unsigned char,96>;
template<class T> T field(const packet& bytes,std::size_t offset){T value{};std::memcpy(&value,bytes.data()+offset,sizeof(T));return value;}
template<class T> void put(packet& bytes,std::size_t offset,T value){std::memcpy(bytes.data()+offset,&value,sizeof(T));}
inline bool valid_request(const packet& b,int sequence){
    static_assert(std::endian::native==std::endian::little&&sizeof(int)==4);
    if(b[0]!=2||b[1]!=1||b[2]!=sequence||b[3]!=(sequence==2?1:0)||b[4]!=0||b[5]!=25||field<std::uint16_t>(b,6)!=0||field<std::uint16_t>(b,8)!=0||b[10]!=0||b[11]!=0||field<int>(b,12)<=0||field<std::int64_t>(b,16)<=0||field<std::int64_t>(b,24)==0||field<std::int64_t>(b,48)!=0||(b[80]&~3)!=0||(b[80]&2)==0||field<std::int64_t>(b,81)<=0)return false;
    for(std::size_t i=56;i<64;i++)if(b[i]!=0)return false;
    for(std::size_t i=89;i<96;i++)if(b[i]!=0)return false;
    return std::any_of(b.begin()+32,b.begin()+48,[](unsigned char c){return c!=0;});
}
inline packet reply(const packet& request,unsigned char kind,unsigned char outcome,std::int64_t age,bool ready,bool reset){packet out{};out[0]=2;out[1]=kind;out[2]=request[2];out[3]=static_cast<unsigned char>((request[3]&1)|(reset?2:0));out[4]=outcome;put<std::uint16_t>(out,6,static_cast<std::uint16_t>(ready?3:0));std::memcpy(out.data()+12,request.data()+12,36);put(out,48,age);return out;}
}
