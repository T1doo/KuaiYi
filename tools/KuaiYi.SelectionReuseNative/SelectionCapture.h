// Diagnostic-only scope adaptation of frozen SDK capture; production probe unchanged.
#pragma once
#include <windows.h>
#include <ole2.h>
#include <UIAutomation.h>
#include <wrl/client.h>
#include "../KuaiYi.ComCaptureProbe/CandidateOrder.h"
#include "RequestLifetime.h"
#include "../KuaiYi.ComCaptureProbe/PhaseTiming.h"
#include <algorithm>
#include <chrono>
#include <cstring>
#include <cwchar>
#include <cerrno>
#include <climits>
#include <limits>
#include <locale>
#include <memory>
#include <sstream>
#include <string>
#include <vector>
#include <deque>
template<class T> using ComPtr=kuaiyi::reuse::request_com<T>;
using kuaiyi::reuse::request_wstring;
using kuaiyi::reuse::request_bytes;
namespace p=kuaiyi::policy;
struct failure  {
    HRESULT hr;
    int type=1;
};
void check(HRESULT hr)  {
    if(FAILED(hr))throw failure {
        hr
    };
} void wincheck(bool valid)  {
    if(!valid)throw failure {
        HRESULT_FROM_WIN32(GetLastError()),3
    };
} struct handle  {
    HANDLE value=nullptr;
    explicit handle(HANDLE v=nullptr):value(v) {
    }~handle() {
        if(value&&value!=INVALID_HANDLE_VALUE)CloseHandle(value);
    }handle(const handle&)=delete;
    handle& operator=(const handle&)=delete;
};
struct apartment  {
    HRESULT hr=CoInitializeEx(nullptr,COINIT_MULTITHREADED);
    apartment() {
        check(hr);
    }~apartment() {
        CoUninitialize();
    }apartment(const apartment&)=delete;
};
struct array_owner  {
    kuaiyi::reuse::lease owned{&kuaiyi::reuse::ledger::owners};
    SAFEARRAY* value=nullptr;
    ~array_owner() {
        if(value&&FAILED(SafeArrayDestroy(value))&&owned.state)owned.state->clean=false;
    }array_owner()=default;
    array_owner(const array_owner&)=delete;
};
struct bstr_owner  {
    kuaiyi::reuse::lease owned{&kuaiyi::reuse::ledger::owners};
    BSTR value=nullptr;
    ~bstr_owner() {
        if(value) {
            SecureZeroMemory(value,static_cast<SIZE_T>(SysStringLen(value))*sizeof(wchar_t));
            SysFreeString(value);
        }
    }bstr_owner()=default;
    bstr_owner(const bstr_owner&)=delete;
};
struct array_access  {
    SAFEARRAY* value;
    void* data=nullptr;
    explicit array_access(SAFEARRAY* v):value(v) {
        check(SafeArrayAccessData(value,&data));
    }~array_access() {
        if(FAILED(SafeArrayUnaccessData(value))&&kuaiyi::reuse::current)kuaiyi::reuse::current->clean=false;
    }array_access(const array_access&)=delete;
};
template<class T> std::vector<T> read_array(SAFEARRAY* raw,VARTYPE wanted,std::size_t cap)  {
    if(!raw)return  {
    };
    VARTYPE type=VT_EMPTY;
    check(SafeArrayGetVartype(raw,&type));
    if(type!=wanted||SafeArrayGetDim(raw)!=1)throw failure {
        E_INVALIDARG
    };
    LONG lower=0,upper=-1;
    check(SafeArrayGetLBound(raw,1,&lower));
    check(SafeArrayGetUBound(raw,1,&upper));
    auto length=static_cast<std::int64_t>(upper)-lower+1;
    if(length<0||static_cast<std::uint64_t>(length)>cap)throw failure {
        E_INVALIDARG
    };
    if(length==0)return  {
    };
    array_access access(raw);
    if(!access.data)throw failure {
        E_POINTER
    };
    auto data=static_cast<const T*>(access.data);
    return  {
        data,data+static_cast<std::ptrdiff_t>(length)
    };
} struct marker  {
    handle mapping;
    void* view=nullptr;
    int sequence=0;
    marker(const GUID& id,bool enabled):mapping(enabled?OpenFileMappingW(FILE_MAP_READ|FILE_MAP_WRITE,FALSE,name(id).c_str()):nullptr)  {
        if(!mapping.value)return;
        view=MapViewOfFile(mapping.value,FILE_MAP_READ|FILE_MAP_WRITE,0,0,64);
        if(!view)return;
        std::uint32_t magic=0,version=0;
        GUID request {
        };
        std::memcpy(&magic,view,4);
        std::memcpy(&version,static_cast<char*>(view)+4,4);
        std::memcpy(&request,static_cast<char*>(view)+8,16);
        bool reserved=true;
        for(int i=32;i<64;++i)reserved=reserved&&static_cast<const unsigned char*>(view)[i]==0;
        if(magic!=0x3142594b||version!=1||!IsEqualGUID(request,id)||!reserved) {
            UnmapViewOfFile(view);
            view=nullptr;
        }
    } ~marker() {
        if(view)UnmapViewOfFile(view);
    }marker(const marker&)=delete;
    static request_wstring name(const GUID& id)  {
        wchar_t text[39] {
        };
        StringFromGUID2(id,text,39);
        request_wstring n=L"Local\\KuaiYi-Boundary-";
        for(wchar_t c:text)if(c&&c!=L'{'&&c!=L'}'&&c!=L'-')n+=c>=L'A'&&c<=L'F'?static_cast<wchar_t>(c+32):c;
        return n;
    } void reach(int boundary,bool returned)  {
        if(!view)return;
        ++sequence;
        auto word=sequence<=16384?(static_cast<std::uint64_t>(sequence)<<32)|0xb14d0000ULL|(static_cast<std::uint64_t>(returned?2:1)<<8)|static_cast<unsigned int>(boundary):0;
        InterlockedExchange64(reinterpret_cast<volatile LONG64*>(static_cast<char*>(view)+24),static_cast<LONG64>(word));
    }
};
struct input  {
    HWND hwnd=nullptr;
    DWORD pid=0;
    int x=0,y=0,down_x=0,down_y=0;
    GUID id {
    };
    std::int64_t start=0;
    bool automatic=false,markers=false,timing=false;
};
struct copy_evidence  {
    std::int64_t focus=0;
    std::vector<int> runtime;
    std::array<double,4> control {
    };
    std::vector<double> selection;
    bool automatic=false;
};
struct result  {
    request_bytes status="EmptyOrUnsupported";
    int ranges=0,candidates=0,stage=1,exception=0;
    HRESULT hr=S_OK;
    std::int64_t elapsed=0,start=0;
    std::vector<int> runtime;
    std::vector<double> rectangles;
    std::array<double,4> control {
    };
    bool has_control=false;
    std::unique_ptr<copy_evidence> copy;
    request_wstring text;
};
using clock_type=std::chrono::steady_clock;
std::int64_t elapsed(clock_type::time_point start)  {
    return std::chrono::duration_cast<std::chrono::milliseconds>(clock_type::now()-start).count();
} bool window_current(const input& in)  {
    DWORD pid=0;
    return IsWindow(in.hwnd)&&GetForegroundWindow()==in.hwnd&&GetWindowThreadProcessId(in.hwnd,&pid)!=0&&pid==in.pid;
} std::int64_t started(HANDLE process)  {
    FILETIME creation {
    },exit {
    },kernel {
    },user {
    };
    wincheck(GetProcessTimes(process,&creation,&exit,&kernel,&user)!=FALSE);
    ULARGE_INTEGER value {
    };
    value.LowPart=creation.dwLowDateTime;
    value.HighPart=creation.dwHighDateTime;
    if(value.QuadPart>static_cast<std::uint64_t>(INT64_MAX)-504911232000000000ULL)throw failure {
        E_INVALIDARG
    };
    return static_cast<std::int64_t>(value.QuadPart+504911232000000000ULL);
} bool source_current(const input& in,HANDLE process,std::int64_t expected)  {
    DWORD state=0;
    wincheck(GetExitCodeProcess(process,&state));
    DWORD actual=0;
    GetWindowThreadProcessId(in.hwnd,&actual);
    return p::current(IsWindow(in.hwnd)!=FALSE,GetForegroundWindow()==in.hwnd,in.pid,actual,state==STILL_ACTIVE,expected,started(process));
} DWORD integrity(DWORD pid,marker& marks,int first)  {
    handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid));
    wincheck(process.value!=nullptr);
    HANDLE raw=nullptr;
    wincheck(OpenProcessToken(process.value,TOKEN_QUERY,&raw)!=FALSE);
    handle token(raw);
    DWORD bytes=0;
    GetTokenInformation(token.value,TokenIntegrityLevel,nullptr,0,&bytes);
    if(bytes==0||bytes>4096)throw failure {
        E_INVALIDARG
    };
    std::vector<unsigned char> storage(bytes);
    wincheck(GetTokenInformation(token.value,TokenIntegrityLevel,storage.data(),bytes,&bytes)!=FALSE);
    marks.reach(first,false);
    PSID sid=reinterpret_cast<TOKEN_MANDATORY_LABEL*>(storage.data())->Label.Sid;
    marks.reach(first,true);
    if(!IsValidSid(sid))throw failure {
        E_INVALIDARG
    };
    marks.reach(first+1,false);
    BYTE count=*GetSidSubAuthorityCount(sid);
    marks.reach(first+1,true);
    if(count==0)throw failure {
        E_INVALIDARG
    };
    marks.reach(first+2,false);
    DWORD level=*GetSidSubAuthority(sid,static_cast<DWORD>(count-1));
    marks.reach(first+2,true);
    return level;
} bool excluded(HANDLE source)  {
    request_wstring path(32768,L'\0');
    DWORD length=static_cast<DWORD>(path.size());
    wincheck(QueryFullProcessImageNameW(source,0,path.data(),&length)!=FALSE);
    path.resize(length);
    auto cut=path.find_last_of(L"\\/");
    request_wstring name=path.substr(cut==request_wstring::npos?0:cut+1);
    return p::excluded_name(std::wstring_view(name));
} BOOL password(IUIAutomationElement* e)  {
    if(!e)throw failure {
        E_POINTER
    };
    BOOL value=FALSE;
    check(e->get_CurrentIsPassword(&value));
    return value;
} int process_id(IUIAutomationElement* e)  {
    int value=0;
    check(e->get_CurrentProcessId(&value));
    return value;
} bool equal(IUIAutomation* api,IUIAutomationElement* a,IUIAutomationElement* b)  {
    if(!a||!b)return a==b;
    BOOL same=FALSE;
    check(api->CompareElements(a,b,&same));
    return same!=FALSE;
} std::array<double,4> bounds(IUIAutomationElement* e)  {
    RECT rect {
    };
    check(e->get_CurrentBoundingRectangle(&rect));
    double width=static_cast<double>(rect.right)-rect.left,height=static_cast<double>(rect.bottom)-rect.top;
    if(width<0||height<0)throw failure {
        E_INVALIDARG
    };
    return  {
        static_cast<double>(rect.left),static_cast<double>(rect.top),width,height
    };
} std::vector<int> runtime_id(IUIAutomationElement* e)  {
    array_owner array;
    check(e->GetRuntimeId(&array.value));
    return read_array<int>(array.value,VT_I4,64);
} template<class Trace> result capture(const input& in,marker& marks,clock_type::time_point origin,result& info,Trace& trace,IUIAutomation* persistent)  {
    auto preflight=trace.observe(0);
    p::soft_observation soft;
    auto expired=[&] {return soft.observe(elapsed(origin),info.stage);};
    auto timed=[&](p::timeout_check check_point){if(check_point==p::timeout_check::focus_ancestors)info.stage=7;else if(check_point==p::timeout_check::hit_ancestors)info.stage=8;else if(check_point==p::timeout_check::before_bfs)info.stage=9;bool timeout=expired();if(timeout)trace.timeout(static_cast<int>(check_point));return timeout;};
    auto finish=[&](const char* status,int count=0) {
        result out;
        out.status=status;
        out.ranges=count;
        out.candidates=info.candidates;
        out.elapsed=elapsed(origin);
        out.stage=out.status=="SoftTimeout"?soft.stage_or(info.stage):info.stage;
        return out;
    };
    if(!window_current(in))return finish("Stale");
    handle source(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,in.pid));
    wincheck(source.value!=nullptr);
    auto start=started(source.value);
    if(start!=in.start)return finish("Stale");
    info.stage=2;
    if(excluded(source.value))return finish("SensitiveOrElevated");
    DWORD source_level=integrity(in.pid,marks,1);
    DWORD own_level=integrity(GetCurrentProcessId(),marks,4);
    if(p::elevated(source_level,own_level))return finish("SensitiveOrElevated");
    preflight.end();
    ComPtr<IUIAutomation> api(persistent);
    info.stage=3;
    auto root_phase=trace.observe(3);
    ComPtr<IUIAutomationElement> root;
    check(api->ElementFromHandle(in.hwnd,root.GetAddressOf()));
    if(!root)throw failure {
        E_POINTER
    };
    root_phase.end();
    info.stage=4;
    auto focus_phase=trace.observe(4);
    ComPtr<IUIAutomationElement> focused;
    check(api->GetFocusedElement(focused.GetAddressOf()));
    focus_phase.end();
    info.stage=5;
    auto hit_phase=trace.observe(5);
    ComPtr<IUIAutomationElement> hit;
    check(api->ElementFromPoint(POINT {
        in.x,in.y
    },hit.GetAddressOf()));
    hit_phase.end();
    info.stage=6;
    auto password_phase=trace.observe(6);
    if((focused&&password(focused.Get()))||password(hit.Get()))return finish("Sensitive");
    password_phase.end();
    auto walker_phase=trace.observe(7);
    ComPtr<IUIAutomationTreeWalker> walker;
    check(api->get_ControlViewWalker(walker.GetAddressOf()));
    if(!walker)throw failure {
        E_POINTER
    };
    walker_phase.end();
    std::vector<ComPtr<IUIAutomationElement>> candidates;
    std::vector<double> copy_ranges;
    auto ancestors=[&](ComPtr<IUIAutomationElement> element,p::timeout_check checkpoint) {
        return p::bound_ancestors(element,root,
            [&](const auto& e){return process_id(e.Get())==static_cast<int>(in.pid);},
            [&](const auto& e,const auto& original){return equal(api.Get(),e.Get(),original.Get());},
            [&](const auto& e){ComPtr<IUIAutomationElement> next;check(walker->GetParentElement(e.Get(),next.GetAddressOf()));return next;},[&]{return timed(checkpoint);});
    };
    auto focus_path=[&]{info.stage=7;auto phase=trace.observe(8);return ancestors(focused,p::timeout_check::focus_ancestors);};
    auto hit_path=[&]{info.stage=8;return ancestors(hit,p::timeout_check::hit_ancestors);};
    auto evaluate=[&](const ComPtr<IUIAutomationElement>& candidate)->std::optional<result> {
        auto evaluating=trace.evaluating();
        if(timed(p::timeout_check::before_candidate))return finish("SoftTimeout");
        if(!window_current(in))return finish("Stale");
        info.stage=10;
        if(password(candidate.Get()))return finish("Sensitive");
        info.stage=11;
        ComPtr<IUIAutomationTextPattern> pattern;
        HRESULT available=candidate->GetCurrentPatternAs(UIA_TextPatternId,IID_PPV_ARGS(pattern.GetAddressOf()));
        if(available==UIA_E_NOTSUPPORTED||(SUCCEEDED(available)&&!pattern))return std::nullopt;
        check(available);
        info.stage=12;
        auto control=bounds(candidate.Get());
        if(in.automatic&&(!p::contains(control,in.down_x,in.down_y)||!p::contains(control,in.x,in.y)))return std::nullopt;
        info.stage=13;
        ComPtr<IUIAutomationTextRangeArray> selections;
        marks.reach(7,false);
        check(pattern->GetSelection(selections.GetAddressOf()));
        marks.reach(7,true);
        if(!selections)return std::nullopt;
        int count=0;
        check(selections->get_Length(&count));
        if(count<0)throw failure {
            E_INVALIDARG
        };
        if(count>64)return finish("TooLarge",count);
        trace.ranges(count);
        if(count==0)return std::nullopt;
        std::vector<request_wstring> parts;
        std::vector<double> rectangles;
        int units=0,observed=0;
        for(int i=0;i<count;++i)  {
            if(timed(p::timeout_check::before_range))return finish("SoftTimeout");
            ComPtr<IUIAutomationTextRange> range;
            check(selections->GetElement(i,range.GetAddressOf()));
            if(!range)throw failure {
                E_POINTER
            };
            info.stage=14;
            std::vector<double> raw;
            marks.reach(8,false);
            {
                array_owner array;
                check(range->GetBoundingRectangles(&array.value));
                raw=read_array<double>(array.value,VT_R8,1024);
            }
            marks.reach(8,true);
            if(!p::append_rectangles(raw,rectangles,observed))return finish("UnsupportedOrProtected");
            info.stage=15;
            request_wstring part;
            UINT size=0;
            marks.reach(9,false);
            {
                bstr_owner text;
                trace.text_start();
                HRESULT text_result=range->GetText(100002-units,&text.value);
                trace.text_end();
                check(text_result);
                size=SysStringLen(text.value);
                if(size<=static_cast<UINT>(100002-units))part.assign(text.value?text.value:L"",size);
            }
            marks.reach(9,true);
            if(size>static_cast<UINT>(100002-units))return finish("TooLarge",count);
            units+=static_cast<int>(size);
            if(units>=100002)return finish("TooLarge",count);
            if(!p::blank(std::wstring_view(part)))parts.push_back(std::move(part));
            trace.range_complete();
        } request_wstring text;
        for(const auto& part:parts) {
            if(!text.empty())text+=L'\n';
            text+=part;
        } if(p::blank(std::wstring_view(text)))  {
            if(equal(api.Get(),candidate.Get(),focused.Get())&&p::contains(control,in.down_x,in.down_y)&&p::contains(control,in.x,in.y)&&p::associated(rectangles,in.down_x,in.down_y,in.x,in.y))copy_ranges=rectangles;
            return std::nullopt;
        } if(in.automatic&&!p::associated(rectangles,in.down_x,in.down_y,in.x,in.y))return std::nullopt;
        if(!p::valid_text(std::wstring_view(text)))return finish("TooLargeOrInvalidUnicode",count);
        info.stage=16;
        if(!source_current(in,source.value,start))return finish("Stale");
        if(timed(p::timeout_check::final_identity))return finish("SoftTimeout");
        info.stage=17;
        auto runtime=runtime_id(candidate.Get());
        if(runtime.empty())return finish("UnsupportedOrProtected");
        if(timed(p::timeout_check::after_runtime))return finish("SoftTimeout");
        auto out=finish("Success",static_cast<int>(parts.size()));
        if(soft.observe(out.elapsed,info.stage)){trace.timeout(static_cast<int>(p::timeout_check::finish_snapshot));return finish("SoftTimeout");}
        info.stage=19;out.stage=19;
        out.runtime=std::move(runtime);
        out.rectangles=std::move(rectangles);
        out.control=control;
        out.has_control=true;
        out.start=start;
        out.text=std::move(text);
        return out;
    };
    auto expand=[&](const ComPtr<IUIAutomationElement>& parent,auto& queue,std::size_t visited) {
        info.stage=9;
        ComPtr<IUIAutomationElement> child;
        check(walker->GetFirstChildElement(parent.Get(),child.GetAddressOf()));
        while(child&&queue.size()+visited<static_cast<std::size_t>(p::candidate_limit)&&!timed(p::timeout_check::bfs_expansion)) {
            if(process_id(child.Get())==static_cast<int>(in.pid))queue.push_back(child);
            ComPtr<IUIAutomationElement> next;
            check(walker->GetNextSiblingElement(child.Get(),next.GetAddressOf()));child=next;
        }
    };
    auto record=[&](const auto& candidate,p::candidate_source source_kind){candidates.push_back(candidate);info.candidates=static_cast<int>(candidates.size());trace.candidate(static_cast<int>(source_kind));};
    if(auto selected=p::evaluate_focus_then_hit<result>(root,focus_path,hit_path,evaluate,expand,record,timed))return std::move(*selected);
    if(!timed(p::timeout_check::after_candidate))info.stage=18;

    std::unique_ptr<copy_evidence> copy;
    if(!timed(p::timeout_check::before_copy)&&source_current(in,source.value,start)&&focused)  {
        bool included=std::any_of(candidates.begin(),candidates.end(),[&](const auto& c) {
            return equal(api.Get(),c.Get(),focused.Get());
        });
        ComPtr<IUIAutomationElement> current;
        check(api->GetFocusedElement(current.GetAddressOf()));
        BOOL enabled=FALSE;
        if(included&&equal(api.Get(),current.Get(),focused.Get())&&process_id(focused.Get())==static_cast<int>(in.pid)&&!password(focused.Get()))  {
            check(focused->get_CurrentIsEnabled(&enabled));
            if(enabled) {
                UIA_HWND native=nullptr;
                check(focused->get_CurrentNativeWindowHandle(&native));
                auto rectangle=bounds(focused.Get());
                auto runtime=runtime_id(focused.Get());
                CONTROLTYPEID type=0;
                check(focused->get_CurrentControlType(&type));
                GUITHREADINFO thread {
                };
                thread.cbSize=static_cast<DWORD>(sizeof(thread));
                DWORD tid=GetWindowThreadProcessId(in.hwnd,nullptr);
                BOOL got=GetGUIThreadInfo(tid,&thread);
                if(native&&got&&thread.hwndFocus==native&&GetAncestor(thread.hwndFocus,GA_ROOT)==in.hwnd&&!runtime.empty()&&(type==UIA_EditControlTypeId||type==UIA_DocumentControlTypeId||type==UIA_TextControlTypeId)) {
                    copy=std::make_unique<copy_evidence>();
                    copy->focus=reinterpret_cast<std::int64_t>(native);
                    copy->runtime=std::move(runtime);
                    copy->control=rectangle;
                    copy->selection=copy_ranges;
                    copy->automatic=!copy_ranges.empty();
                }
            }
        }
    } if(!source_current(in,source.value,start))return finish("Stale");
    bool timeout=timed(p::timeout_check::final_empty);
    auto out=finish(timeout?"SoftTimeout":"EmptyOrUnsupported");
    if(soft.observe(out.elapsed,info.stage)){trace.timeout(static_cast<int>(p::timeout_check::finish_snapshot));out.status="SoftTimeout";out.stage=soft.stage_or(info.stage);}
    else {info.stage=19;out.stage=19;}
    out.start=start;
    if(!p::soft_expired(out.elapsed))out.copy=std::move(copy);
    return out;
} template<class T> void json_array(std::ostringstream& stream,const T& values)  {
    stream<<'[';
    bool first=true;
    for(auto value:values) {
        if(!first)stream<<',';
        first=false;
        stream<<value;
    }stream<<']';
}
