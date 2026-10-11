#include <windows.h>
#include <oleidl.h>
#include <new>
using Callback = HRESULT (__stdcall*)(int, IDataObject*, DWORD, POINTL, DWORD*);
// Native identity has no IMarshal/IAgileObject/FTM. Standard COM marshaling binds callbacks to its creating OLE STA.
class Target final : public IDropTarget {
    LONG refs_=1; Callback callback_;
public:
    explicit Target(Callback callback) noexcept : callback_(callback) {}
    HRESULT __stdcall QueryInterface(REFIID id, void** result) override {
        if(!result)return E_POINTER;
        *result=nullptr;
        if(id==IID_IUnknown||id==IID_IDropTarget){*result=static_cast<IDropTarget*>(this);AddRef();return S_OK;}return E_NOINTERFACE;
    }
    ULONG __stdcall AddRef() override { return static_cast<ULONG>(InterlockedIncrement(&refs_)); }
    ULONG __stdcall Release() override { auto left=InterlockedDecrement(&refs_);if(!left)delete this;return static_cast<ULONG>(left); }
    HRESULT __stdcall DragEnter(IDataObject* data,DWORD keys,POINTL point,DWORD* effect) override {if(!effect)return E_POINTER;return callback_(1,data,keys,point,effect);}
    HRESULT __stdcall DragOver(DWORD keys,POINTL point,DWORD* effect) override {if(!effect)return E_POINTER;return callback_(2,nullptr,keys,point,effect);}
    HRESULT __stdcall DragLeave() override {DWORD none=0;return callback_(3,nullptr,0,POINTL{},&none);}
    HRESULT __stdcall Drop(IDataObject* data,DWORD keys,POINTL point,DWORD* effect) override {if(!effect)return E_POINTER;return callback_(4,data,keys,point,effect);}
};
extern "C" __declspec(dllexport) HRESULT __cdecl KY_CreateDropTarget(Callback callback,IDropTarget** result) noexcept {
    if(!result)return E_POINTER;
    *result=nullptr;
    if(!callback)return E_POINTER;
    *result=new(std::nothrow) Target(callback);
    return *result?S_OK:E_OUTOFMEMORY;
}
