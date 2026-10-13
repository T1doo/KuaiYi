#include <windows.h>
#include <ole2.h>
#include <UIAutomation.h>
#include <cwchar>
#include <bit>
#include "LifecycleProtocol.h"
#include "../KuaiYi.SelectionReuseNative/RequestLifetime.h"
namespace l=kuaiyi::lifecycle;
namespace r=kuaiyi::reuse;
enum class input_state {complete,closed,invalid};
input_state read_packet(l::packet& p){std::size_t offset=0;while(offset<p.size()){DWORD count=0;if(!ReadFile(GetStdHandle(STD_INPUT_HANDLE),p.data()+offset,static_cast<DWORD>(p.size()-offset),&count,nullptr))return GetLastError()==ERROR_BROKEN_PIPE&&offset==0?input_state::closed:input_state::invalid;if(count==0)return offset==0?input_state::closed:input_state::invalid;offset+=count;}return input_state::complete;}
bool write_packet(const l::packet& p){std::size_t offset=0;while(offset<p.size()){DWORD count=0;if(!WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),p.data()+offset,static_cast<DWORD>(p.size()-offset),&count,nullptr)||count==0)return false;offset+=count;}return true;}
int age(std::int64_t origin){LARGE_INTEGER now{},frequency{};if(!QueryPerformanceCounter(&now)||!QueryPerformanceFrequency(&frequency)||frequency.QuadPart<=0||origin<=0||origin>now.QuadPart)throw 1;auto value=static_cast<long double>(now.QuadPart-origin)*1000/static_cast<long double>(frequency.QuadPart);return value>=600?600:static_cast<int>(value);}
struct persistent {
    IUIAutomation* api=nullptr;bool initialized=false;
    void finish()noexcept{if(api){auto held=api;api=nullptr;held->Release();}if(initialized){initialized=false;CoUninitialize();}}
    ~persistent(){finish();}
};
int hex(wchar_t c){if(c>=L'0'&&c<=L'9')return c-L'0';if(c>=L'a'&&c<=L'f')return c-L'a'+10;return -1;}
int wmain(int argc,wchar_t** argv){
    static_assert(sizeof(void*)==8&&std::endian::native==std::endian::little);
    if(argc!=4||std::wcscmp(argv[1],L"--lifecycle-worker")!=0||std::wcslen(argv[2])!=1||argv[2][0]<L'1'||argv[2][0]>L'4'||std::wcslen(argv[3])!=32)return 2;
    auto mode=static_cast<l::mode>(argv[2][0]-L'0');std::array<unsigned char,16> nonce{};
    for(std::size_t i=0;i<nonce.size();i++){int high=hex(argv[3][i*2]),low=hex(argv[3][i*2+1]);if(high<0||low<0)return 2;nonce[i]=static_cast<unsigned char>(high*16+low);}
    FILETIME created{},exited{},kernel{},user{};if(!GetProcessTimes(GetCurrentProcess(),&created,&exited,&kernel,&user))return 3;
    auto ticks=static_cast<std::int64_t>((static_cast<std::uint64_t>(created.dwHighDateTime)<<32)|created.dwLowDateTime)+504911232000000000LL;
    int pid=static_cast<int>(GetCurrentProcessId());auto started=l::make(l::phase::started,0,mode,0,nonce,pid,ticks,0);if(!write_packet(started))return 3;
    persistent retained;unsigned char completed=0;
    try {
        for(unsigned char sequence=1;sequence<=2;sequence++){
            l::packet begin{};r::owned_buffer clear(begin.data(),begin.size());auto state=read_packet(begin);
            if(state==input_state::closed){if(completed==0)return 0;retained.finish();auto final=l::make(l::phase::final_released,completed,mode,0,nonce,pid,ticks,0);return write_packet(final)?0:3;}
            if(state!=input_state::complete||!l::valid(begin)||begin[5]!=static_cast<unsigned char>(l::phase::authorize)||begin[6]!=sequence||begin[7]!=static_cast<unsigned char>(mode)||l::field<int>(begin,32)!=pid||l::field<std::int64_t>(begin,36)!=ticks)return 3;
            std::array<unsigned char,16> request{};std::copy(begin.begin()+16,begin.begin()+32,request.begin());if(sequence==1?request!=nonce:request==nonce)return 3;nonce=request;
            // No COM before verified authorization. Before-bind mode can never initialize COM.
            BOOL attached=FALSE;if(mode==l::mode::before_bind||!IsProcessInJob(GetCurrentProcess(),nullptr,&attached)||!attached)return 3;
            auto origin=l::field<std::int64_t>(begin,8);
            auto emit=[&](l::phase stage){auto p=l::make(stage,sequence,mode,origin,nonce,pid,ticks,age(origin));return write_packet(p);};
            if(age(origin)>=250){if(!emit(l::phase::soft_timeout))return 3;completed=sequence;break;}
            if(mode==l::mode::bound_death||mode==l::mode::watchdog){if(!emit(l::phase::parked))return 3;Sleep(INFINITE);return 3;} // Not stdin-dependent; no COM in synthetic hang.
            if(sequence==1){
                HRESULT hr=CoInitializeEx(nullptr,COINIT_MULTITHREADED);retained.initialized=SUCCEEDED(hr);
                if(SUCCEEDED(hr))hr=CoCreateInstance(__uuidof(CUIAutomation),nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&retained.api));
                if(FAILED(hr)){retained.finish();return emit(l::phase::init_failed)?0:3;}
                if(!emit(l::phase::initialized))return 3;
            }
            if(age(origin)>=250){if(!emit(l::phase::soft_timeout))return 3;completed=sequence;break;}
            {r::request_scope scope;{r::request_com<IUIAutomation> local(retained.api);local.Reset();if(!emit(l::phase::local_released))return 3;}if(!scope.state.settled())return 3;}
            // Only actual call returns and our explicit owner ledger are asserted, never provider internals.
            if(!emit(l::phase::quiescent))return 3;completed=sequence;
        }
        // No third request, no target call and no text. Parent must close stdin before final cleanup.
        l::packet tail{};r::owned_buffer clear(tail.data(),tail.size());if(read_packet(tail)!=input_state::closed)return 3;
        retained.finish();auto final=l::make(l::phase::final_released,completed,mode,0,nonce,pid,ticks,0);return write_packet(final)?0:3;
    }catch(...){return 3;} // No arbitrary diagnostic output; destructor returns are never claimed after forced termination.
}
