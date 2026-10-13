#include "SelectionCapture.h"
#include "NativeProtocol.h"
#include <optional>
#include <array>
#include <cstdint>
#include <bit>

namespace r=kuaiyi::reuse;
using r::packet;using r::field;using r::put;using r::reply;using r::valid_request;
enum class input_state { complete,closed,truncated,error };
input_state read_packet(packet& bytes){std::size_t offset=0;while(offset<bytes.size()){DWORD done=0;if(!ReadFile(GetStdHandle(STD_INPUT_HANDLE),bytes.data()+offset,static_cast<DWORD>(bytes.size()-offset),&done,nullptr)){return GetLastError()==ERROR_BROKEN_PIPE?(offset==0?input_state::closed:input_state::truncated):input_state::error;}if(done==0)return offset==0?input_state::closed:input_state::truncated;offset+=done;}return input_state::complete;}
bool write_packet(const packet& bytes){std::size_t offset=0;while(offset<bytes.size()){DWORD done=0;if(!WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),bytes.data()+offset,static_cast<DWORD>(bytes.size()-offset),&done,nullptr)||done==0)return false;offset+=done;}return true;}
std::int64_t request_age(std::int64_t origin){LARGE_INTEGER now{},frequency{};wincheck(QueryPerformanceCounter(&now)!=FALSE&&QueryPerformanceFrequency(&frequency)!=FALSE);if(frequency.QuadPart<=0||origin<=0||origin>now.QuadPart)throw failure{E_INVALIDARG};auto ms=static_cast<long double>(now.QuadPart-origin)*1000/static_cast<long double>(frequency.QuadPart);return ms>=600?600:static_cast<std::int64_t>(ms);}
unsigned char status_code(const result& value){if(value.status=="Success")return 1;if(value.status=="EmptyOrUnsupported")return 2;if(value.status=="Sensitive"||value.status=="SensitiveOrElevated"||value.status=="UnsupportedOrProtected")return 3;if(value.status=="Stale")return 4;if(value.status=="TooLarge"||value.status=="TooLargeOrInvalidUnicode")return 5;if(value.status=="SoftTimeout")return 6;return 8;}
r::request_bytes encode_text(const r::request_wstring& text){if(text.empty())return {};if(text.size()>100002)throw failure{E_INVALIDARG};int n=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,text.data(),static_cast<int>(text.size()),nullptr,0,nullptr,nullptr);wincheck(n>0);r::request_bytes bytes(static_cast<std::size_t>(n),'\0');wincheck(WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,text.data(),static_cast<int>(text.size()),bytes.data(),n,nullptr,nullptr)==n);return bytes;}
int wmain(int argc,wchar_t** argv){
    static_assert(sizeof(wchar_t)==2&&sizeof(void*)==8&&std::endian::native==std::endian::little);
    if(argc!=2||std::wcscmp(argv[1],L"--native-reuse-worker")!=0)return 2;
    // No HWND, no target API, no COM initialization before a request authorized by the owning host.
    std::optional<apartment> mta;Microsoft::WRL::ComPtr<IUIAutomation> automation;
    DWORD first_pid=0;HWND first_hwnd=nullptr;std::int64_t first_start=0;GUID previous{};
    for(int sequence=1;sequence<=2;sequence++){
        packet begin{};auto incoming=read_packet(begin);if(incoming!=input_state::complete){r::erase(begin.data(),begin.size());return incoming==input_state::closed?0:3;}
        r::owned_buffer erase_begin(begin.data(),begin.size());
        if(!valid_request(begin,sequence))return 3;
        input in;in.pid=static_cast<DWORD>(field<int>(begin,12));in.start=field<std::int64_t>(begin,16);in.hwnd=reinterpret_cast<HWND>(field<std::int64_t>(begin,24));in.id=field<GUID>(begin,32);
        in.down_x=field<int>(begin,64);in.down_y=field<int>(begin,68);in.x=field<int>(begin,72);in.y=field<int>(begin,76);in.automatic=(begin[80]&1)==0;
        if(sequence==2&&(in.pid!=first_pid||in.hwnd!=first_hwnd||in.start!=first_start||IsEqualGUID(in.id,previous)))return 3;
        previous=in.id;first_pid=in.pid;first_hwnd=in.hwnd;first_start=in.start;
        std::int64_t origin=field<std::int64_t>(begin,81);BOOL attached=FALSE;
        // Parent owns the unnamed noninheritable kill-on-close Job. No child Job handle exists.
        if(!IsProcessInJob(GetCurrentProcess(),nullptr,&attached)||!attached)return 3;
        try{
            auto age=request_age(origin);
            if(sequence==1){
                if(age>=250){auto skipped=reply(begin,5,6,age,false,false);r::owned_buffer clear(skipped.data(),skipped.size());return write_packet(skipped)?0:20;}
                try{mta.emplace();check(CoCreateInstance(__uuidof(CUIAutomation),nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(automation.GetAddressOf())));}
                catch(const failure&){automation.Reset();mta.reset();auto failed=reply(begin,5,7,request_age(origin),false,false);r::owned_buffer clear(failed.data(),failed.size());return write_packet(failed)?0:20;}
                auto initialized=reply(begin,2,0,request_age(origin),true,false);r::owned_buffer clear(initialized.data(),initialized.size());if(!write_packet(initialized))return 20;
            }
            unsigned char outcome=8;
            {
                r::request_scope scope;
                {
                    result info,out;marker marks(in.id,false);auto capture_origin=clock_type::now()-std::chrono::milliseconds(request_age(origin));p::phase_timings trace(false,[&]{return elapsed(capture_origin);});
                    try{if(request_age(origin)>=250)out.status="SoftTimeout";else out=capture(in,marks,capture_origin,info,trace,automation.Get());}
                    catch(const failure& f){out.status=f.hr==UIA_E_ELEMENTNOTAVAILABLE?"Stale":"UnsupportedOrProtected";}
                    catch(const std::exception&){out.status="BrokerFailure";}
                    outcome=status_code(out);
                    // Native diagnostic never transmits selected text or a text-derived hash.
                    // Validate/erase the owned UTF-8 copy, then wipe all own wstrings on scope exit.
                    auto encoded=encode_text(out.text);(void)encoded;
                }
                if(!scope.state.settled())return 21;
            }
            auto completed_age=request_age(origin);if(outcome==1&&completed_age>=250)outcome=6;
            auto completed=reply(begin,3,outcome,completed_age,true,false);r::owned_buffer clear_completed(completed.data(),completed.size());if(!write_packet(completed))return 20;r::erase(completed.data(),completed.size());
            if(outcome==4)return 0; // Source denial is terminal; no second request or fallback.
            auto quiescent=reply(begin,4,outcome,request_age(origin),true,true);r::owned_buffer clear_quiescent(quiescent.data(),quiescent.size());if(!write_packet(quiescent))return 20;
        }
        catch(const failure&){return 22;}catch(const std::exception&){return 22;}
        // Request-local owners, SAFEARRAY/BSTRs, paths, caches and own text are gone before this point.
    }
    // Only the apartment and IUIAutomation survive between requests. No third request.
    packet tail{};r::owned_buffer erase_tail(tail.data(),tail.size());return read_packet(tail)==input_state::closed?0:3;
}
