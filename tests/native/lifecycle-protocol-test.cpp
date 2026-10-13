#include "../../tools/KuaiYi.ReuseLifecycleNative/LifecycleProtocol.h"
#include <iostream>
#include <cstdlib>
namespace l=kuaiyi::lifecycle;
int checks=0;void check(bool v){if(!v)std::abort();checks++;}
int main(){std::array<unsigned char,16> nonce{};nonce[0]=1;auto p=l::make(l::phase::started,0,l::mode::normal,0,nonce,44,98765,0);check(l::valid(p));check(p.size()==64);check(l::field<int>(p,32)==44&&l::field<std::int64_t>(p,36)==98765);for(int at:{0,4,5,6,7,48,63}){auto bad=p;bad[static_cast<std::size_t>(at)]=255;check(!l::valid(bad));}check(!l::valid(l::make(l::phase::started,0,l::mode::normal,0,nonce,0,1,0)));check(!l::valid(l::make(l::phase::quiescent,2,l::mode::normal,1234,nonce,44,98765,601)));std::fill(nonce.begin(),nonce.end(),0);check(!l::valid(l::make(l::phase::started,0,l::mode::normal,0,nonce,44,98765,0)));std::cout<<"LIFECYCLE_NATIVE_PACKET passed="<<checks<<" failed=0; portable layout only, Windows NotRun\n";}
