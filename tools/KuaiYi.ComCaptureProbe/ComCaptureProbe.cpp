#include <windows.h>
#include <ole2.h>
#include <UIAutomation.h>
#include <wrl/client.h>
#include "CandidateOrder.h"
#include "PhaseTiming.h"
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
using Microsoft::WRL::ComPtr;
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
    SAFEARRAY* value=nullptr;
    ~array_owner() {
        if(value)SafeArrayDestroy(value);
    }array_owner()=default;
    array_owner(const array_owner&)=delete;
};
struct bstr_owner  {
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
        SafeArrayUnaccessData(value);
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
    static std::wstring name(const GUID& id)  {
        wchar_t text[39] {
        };
        StringFromGUID2(id,text,39);
        std::wstring n=L"Local\\KuaiYi-Boundary-";
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
    std::string status="EmptyOrUnsupported";
    int ranges=0,candidates=0,stage=1,exception=0;
    HRESULT hr=S_OK;
    std::int64_t elapsed=0,start=0;
    std::vector<int> runtime;
    std::vector<double> rectangles;
    std::array<double,4> control {
    };
    bool has_control=false;
    std::unique_ptr<copy_evidence> copy;
    std::wstring text;
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
    std::wstring path(32768,L'\0');
    DWORD length=static_cast<DWORD>(path.size());
    wincheck(QueryFullProcessImageNameW(source,0,path.data(),&length)!=FALSE);
    path.resize(length);
    auto cut=path.find_last_of(L"\\/");
    auto name=path.substr(cut==std::wstring::npos?0:cut+1);
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
} template<class Trace> result capture(const input& in,marker& marks,clock_type::time_point origin,result& info,Trace& trace)  {
    auto preflight=trace.observe(0);
    p::soft_observation soft;
    auto expired=[&] {
        return soft.observe(elapsed(origin),info.stage);
    };
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
    auto com_phase=trace.observe(1);
    apartment com;
    com_phase.end();
    ComPtr<IUIAutomation> api;
    auto create_phase=trace.observe(2);
    check(CoCreateInstance(__uuidof(CUIAutomation),nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(api.GetAddressOf())));
    create_phase.end();
    info.stage=3;
    auto root_phase=trace.observe(3);
    ComPtr<IUIAutomationElement> root;
    check(api->ElementFromHandle(in.hwnd,&root));
    if(!root)throw failure {
        E_POINTER
    };
    root_phase.end();
    info.stage=4;
    auto focus_phase=trace.observe(4);
    ComPtr<IUIAutomationElement> focused;
    check(api->GetFocusedElement(&focused));
    focus_phase.end();
    info.stage=5;
    auto hit_phase=trace.observe(5);
    ComPtr<IUIAutomationElement> hit;
    check(api->ElementFromPoint(POINT {
        in.x,in.y
    },&hit));
    hit_phase.end();
    info.stage=6;
    auto password_phase=trace.observe(6);
    if((focused&&password(focused.Get()))||password(hit.Get()))return finish("Sensitive");
    password_phase.end();
    auto walker_phase=trace.observe(7);
    ComPtr<IUIAutomationTreeWalker> walker;
    check(api->get_ControlViewWalker(&walker));
    if(!walker)throw failure {
        E_POINTER
    };
    walker_phase.end();
    std::vector<ComPtr<IUIAutomationElement>> candidates;
    std::vector<double> copy_ranges;
    auto ancestors=[&](ComPtr<IUIAutomationElement> element) {
        auto path=p::bound_ancestors(element,root,
            [&](const auto& e){return process_id(e.Get())==static_cast<int>(in.pid);},
            [&](const auto& e,const auto& original){return equal(api.Get(),e.Get(),original.Get());},
            [&](const auto& e){ComPtr<IUIAutomationElement> next;check(walker->GetParentElement(e.Get(),&next));return next;},expired);
        auto count=std::min(path.size(),static_cast<std::size_t>(p::candidate_limit)-candidates.size());
        candidates.insert(candidates.end(),path.begin(),path.begin()+static_cast<std::ptrdiff_t>(count));
    };
    info.stage=7;
    auto ancestors_phase=trace.observe(8);
    ancestors(focused);
    ancestors_phase.end();
    info.stage=8;
    ancestors(hit);
    info.candidates=static_cast<int>(candidates.size());
    auto evaluate=[&](const ComPtr<IUIAutomationElement>& candidate)->std::optional<result> {
        if(expired())return finish("SoftTimeout");
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
        check(pattern->GetSelection(&selections));
        marks.reach(7,true);
        if(!selections)return std::nullopt;
        int count=0;
        check(selections->get_Length(&count));
        if(count<0)throw failure {
            E_INVALIDARG
        };
        if(count==0)return std::nullopt;
        if(count>64)return finish("TooLarge",count);
        std::vector<std::wstring> parts;
        std::vector<double> rectangles;
        int units=0,observed=0;
        for(int i=0;i<count;++i)  {
            if(expired())return finish("SoftTimeout");
            ComPtr<IUIAutomationTextRange> range;
            check(selections->GetElement(i,&range));
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
            std::wstring part;
            UINT size=0;
            marks.reach(9,false);
            {
                bstr_owner text;
                check(range->GetText(100002-units,&text.value));
                size=SysStringLen(text.value);
                if(size<=static_cast<UINT>(100002-units))part.assign(text.value?text.value:L"",size);
            }
            marks.reach(9,true);
            if(size>static_cast<UINT>(100002-units))return finish("TooLarge",count);
            units+=static_cast<int>(size);
            if(units>=100002)return finish("TooLarge",count);
            if(!p::blank(std::wstring_view(part)))parts.push_back(std::move(part));
        } std::wstring text;
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
        if(expired())return finish("SoftTimeout");
        info.stage=17;
        auto runtime=runtime_id(candidate.Get());
        if(runtime.empty())return finish("UnsupportedOrProtected");
        if(expired())return finish("SoftTimeout");
        auto out=finish("Success",static_cast<int>(parts.size()));
        if(soft.observe(out.elapsed,info.stage))return finish("SoftTimeout");
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
        check(walker->GetFirstChildElement(parent.Get(),&child));
        while(child&&queue.size()+visited<static_cast<std::size_t>(p::candidate_limit)&&!expired()) {
            if(process_id(child.Get())==static_cast<int>(in.pid))queue.push_back(child);
            ComPtr<IUIAutomationElement> next;
            check(walker->GetNextSiblingElement(child.Get(),&next));child=next;
        }
    };
    auto record=[&](const auto& candidate){candidates.push_back(candidate);info.candidates=static_cast<int>(candidates.size());};
    if(auto selected=p::evaluate_ordered<result>(std::span<const ComPtr<IUIAutomationElement>>(candidates),root,evaluate,expand,record,expired))return std::move(*selected);
    if(!expired())info.stage=18;

    std::unique_ptr<copy_evidence> copy;
    if(!expired()&&source_current(in,source.value,start)&&focused)  {
        bool included=std::any_of(candidates.begin(),candidates.end(),[&](const auto& c) {
            return equal(api.Get(),c.Get(),focused.Get());
        });
        ComPtr<IUIAutomationElement> current;
        check(api->GetFocusedElement(&current));
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
    bool timeout=expired();
    auto out=finish(timeout?"SoftTimeout":"EmptyOrUnsupported");
    if(soft.observe(out.elapsed,info.stage)){out.status="SoftTimeout";out.stage=soft.stage_or(info.stage);}
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
} std::string metadata(const result& r,const std::array<std::int64_t,18>* timings)  {
    std::ostringstream out;
    out.imbue(std::locale::classic());
    out.precision(17);
    out<<"{\"Status\":\""<<r.status<<"\",\"RangeCount\":"<<r.ranges<<",\"CandidateCount\":"<<r.candidates<<",\"ElapsedMs\":"<<r.elapsed<<",\"ProcessStartUtcTicks\":"<<r.start<<",\"Diagnostic\":{\"Stage\":"<<r.stage<<",\"Exception\":"<<r.exception<<",\"HResult\":";
    if(r.exception)out<<static_cast<std::int32_t>(r.hr);
    else out<<"null";
    out<<'}';
    if(!r.runtime.empty()) {
        out<<",\"RuntimeId\":";
        json_array(out,r.runtime);
    }if(!r.rectangles.empty()) {
        out<<",\"RangeRectangles\":";
        json_array(out,r.rectangles);
    }if(r.has_control) {
        out<<",\"ControlRectangle\":";
        json_array(out,r.control);
    }if(r.copy) {
        out<<",\"Copy\":{\"FocusHandle\":"<<r.copy->focus<<",\"RuntimeId\":";
        json_array(out,r.copy->runtime);
        out<<",\"ControlRectangle\":";
        json_array(out,r.copy->control);
        out<<",\"SelectionRectangles\":";
        json_array(out,r.copy->selection);
        out<<",\"AutomaticTrusted\":"<<(r.copy->automatic?"true":"false")<<'}';
    }if(timings) {
        out<<",\"PhaseTimings\":[";
        bool first=true;
        for(auto value:*timings){if(!first)out<<',';first=false;if(value==-2)out<<"null";else out<<value;}
        out<<']';
    }out<<'}';
    return out.str();
} std::string utf8(const std::wstring& text)  {
    if(text.empty())return  {
    };
    int count=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,text.data(),static_cast<int>(text.size()),nullptr,0,nullptr,nullptr);
    wincheck(count>0);
    std::string bytes(static_cast<std::size_t>(count),'\0');
    wincheck(WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,text.data(),static_cast<int>(text.size()),bytes.data(),count,nullptr,nullptr)==count);
    return bytes;
} bool write_all(HANDLE out,const void* data,DWORD length)  {
    auto bytes=static_cast<const char*>(data);
    while(length) {
        DWORD done=0;
        if(!WriteFile(out,bytes,length,&done,nullptr)||done==0)return false;
        bytes+=done;
        length-=done;
    }return true;
} bool frame(const GUID& id,std::uint16_t type,const std::string& payload)  {
    if(payload.size()>(type==5?1048576U:65536U))return false;
    std::array<unsigned char,28> header {
    };
    std::uint32_t magic=0x3159594b,size=static_cast<std::uint32_t>(payload.size());
    std::uint16_t version=1;
    std::memcpy(header.data(),&magic,4);
    std::memcpy(header.data()+4,&version,2);
    std::memcpy(header.data()+6,&type,2);
    std::memcpy(header.data()+8,&size,4);
    std::memcpy(header.data()+12,&id,16);
    HANDLE out=GetStdHandle(STD_OUTPUT_HANDLE);
    return write_all(out,header.data(),28)&&write_all(out,payload.data(),size);
} bool number(const wchar_t* text,std::int64_t& value)  {
    if(!text||!*text)return false;
    wchar_t* end=nullptr;
    errno=0;
    value=_wcstoi64(text,&end,10);
    return errno==0&&end&&*end==0;
} int wmain(int argc,wchar_t** argv)  {
    static_assert(sizeof(wchar_t)==2&&sizeof(void*)==8);
    if(argc<10||argc>12)return 2;
    input in;
    std::int64_t hwnd=0,pid=0,x=0,y=0,dx=0,dy=0;
    if(!number(argv[1],hwnd)||!number(argv[2],pid)||pid<=0||pid>UINT32_MAX||!number(argv[3],x)||!number(argv[4],y)||!number(argv[6],dx)||!number(argv[7],dy)||!number(argv[9],in.start)||in.start<=0||x<INT_MIN||x>INT_MAX||y<INT_MIN||y>INT_MAX||dx<INT_MIN||dx>INT_MAX||dy<INT_MIN||dy>INT_MAX)return 2;
    std::wstring guid=L"{"+std::wstring(argv[5])+L"}";
    if(FAILED(CLSIDFromString(guid.c_str(),&in.id))||IsEqualGUID(in.id,GUID_NULL))return 2;
    if(std::wcscmp(argv[8],L"explicit")!=0&&std::wcscmp(argv[8],L"gesture")!=0)return 2;
    for(int i=10;i<argc;++i){
        if(std::wcscmp(argv[i],L"--boundary-marker")==0&&!in.markers)in.markers=true;
        else if(std::wcscmp(argv[i],L"--phase-timing")==0&&!in.timing)in.timing=true;
        else return 2;
    }
    in.hwnd=reinterpret_cast<HWND>(hwnd);
    in.pid=static_cast<DWORD>(pid);
    in.x=static_cast<int>(x);
    in.y=static_cast<int>(y);
    in.down_x=static_cast<int>(dx);
    in.down_y=static_cast<int>(dy);
    in.automatic=std::wcscmp(argv[8],L"gesture")==0;
    auto origin=clock_type::now();
    marker marks(in.id,in.markers);
    p::phase_timings trace(in.timing,[&]{return elapsed(origin);});
    result info,out;
    try {
        marks.reach(10,false);
        out=capture(in,marks,origin,info,trace);
        marks.reach(10,true);
    }catch(const failure& f) {
        out.status=f.hr==UIA_E_ELEMENTNOTAVAILABLE?"Stale":"UnsupportedOrProtected";
        out.stage=info.stage;
        out.exception=f.hr==UIA_E_ELEMENTNOTAVAILABLE?5:f.type;
        out.hr=f.hr;
        out.elapsed=elapsed(origin);
    } marks.reach(11,false);
    auto meta=metadata(out,in.timing?&trace.values:nullptr);
    auto text=utf8(out.text);
    marks.reach(11,true);
    marks.reach(12,false);
    bool sent=frame(in.id,1,meta)&&frame(in.id,5,text);
    if(!text.empty())SecureZeroMemory(text.data(),text.size());
    if(!out.text.empty())SecureZeroMemory(out.text.data(),out.text.size()*sizeof(wchar_t));
    if(!sent)return 20;
    marks.reach(12,true);
    marks.reach(13,false);
    return 0;
}
