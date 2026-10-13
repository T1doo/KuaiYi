#include "../../tools/KuaiYi.SelectionReuseNative/NativeProtocol.h"
#include <iostream>
#include <cstdlib>
namespace r=kuaiyi::reuse;
int checks=0;void require(bool value){if(!value)std::abort();++checks;}
int main(){
    r::packet begin{};begin[0]=2;begin[1]=1;begin[2]=1;begin[5]=25;r::put<int>(begin,12,44);r::put<std::int64_t>(begin,16,98765);r::put<std::int64_t>(begin,24,101);begin[32]=1;r::put<int>(begin,64,-10);r::put<int>(begin,72,300);begin[80]=2;r::put<std::int64_t>(begin,81,1234);
    require(r::valid_request(begin,1));require(!r::valid_request(begin,2));require(r::field<int>(begin,64)==-10&&r::field<int>(begin,72)==300);
    for(int offset:{0,1,2,3,4,5,6,8,10,11,56,89}){auto bad=begin;bad[static_cast<std::size_t>(offset)]=99;require(!r::valid_request(bad,1));}
    {auto bad=begin;bad[80]=0;require(!r::valid_request(bad,1));bad=begin;std::fill(bad.begin()+32,bad.begin()+48,0);require(!r::valid_request(bad,1));}
    auto failed=r::reply(begin,5,7,5,false,false);require(failed[1]==5&&failed[4]==7&&r::field<std::uint16_t>(failed,6)==0&&failed[5]==0);
    auto soft=r::reply(begin,3,6,251,true,false);require(soft[4]==6&&soft[5]==0&&r::field<std::int64_t>(soft,48)==251);
    auto quiet=r::reply(begin,4,6,252,true,true);require(quiet[3]==2&&quiet[5]==0&&r::field<std::uint16_t>(quiet,8)==0&&quiet[10]==0);
    require(std::equal(begin.begin()+12,begin.begin()+48,quiet.begin()+12));
    for(const auto& value:std::initializer_list<std::pair<std::string_view,unsigned char>>{{"Success",1},{"EmptyOrUnsupported",2},{"Sensitive",3},{"SensitiveOrElevated",3},{"Stale",4},{"TooLarge",5},{"TooLargeOrInvalidUnicode",5},{"SoftTimeout",6},{"UnsupportedOrProtected",9},{"BrokerFailure",8},{"Unknown",8},{"",8}})require(r::status_code(value.first)==value.second);
    require(r::status_code("UnsupportedOrProtected")!=r::status_code("Sensitive"));
    std::cout<<"REUSE_NATIVE_PROTOCOL passed="<<checks<<" failed=0; portable packet layout, Windows worker NotRun\n";
}
