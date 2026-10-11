#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <wrl/client.h>
#include <atomic>
#include <new>
#include <string>
#include <vector>
#include "CommandPolicy.h"
using Microsoft::WRL::ComPtr;
namespace {
const CLSID commandId = {0x86f7ab57,0x0f30,0x47a6,{0xb2,0x84,0x3c,0x7b,0xc8,0x2d,0xf8,0xe2}};
std::atomic<long> objects{0}, locks{0}; HMODULE module = nullptr;
std::wstring modulePath() {
    std::vector<wchar_t> buffer(32768);
    DWORD size = GetModuleFileNameW(module, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (!size || size >= buffer.size()) return {};
    return std::wstring(buffer.data(), size);
}
HRESULT selectedPath(IShellItemArray* items, std::wstring& path) {
    if (!items) return E_INVALIDARG;
    DWORD count = 0; HRESULT result = items->GetCount(&count);
    if (FAILED(result) || count != 1) return E_INVALIDARG;
    ComPtr<IShellItem> item; result = items->GetItemAt(0, item.GetAddressOf());
    if (FAILED(result)) return result;
    PWSTR name = nullptr; result = item->GetDisplayName(SIGDN_FILESYSPATH, &name);
    if (SUCCEEDED(result) && name) path = name;
    CoTaskMemFree(name);
    if (FAILED(result) || !kuaiyi::supportedPath(path)) return E_INVALIDARG;
    return S_OK;
}
class Command final : public IExplorerCommand {
    std::atomic<ULONG> refs{1};
public:
    Command() { ++objects; } ~Command() { --objects; }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** out) override {
        if (!out) return E_POINTER; *out = nullptr;
        if (id == IID_IUnknown || id == IID_IExplorerCommand) { *out = static_cast<IExplorerCommand*>(this); AddRef(); return S_OK; }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs; }
    ULONG STDMETHODCALLTYPE Release() override { ULONG count = --refs; if (!count) delete this; return count; }
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*, PWSTR* out) override { return out ? SHStrDupW(L"快译", out) : E_POINTER; }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*, PWSTR* out) override {
        if (!out) return E_POINTER; *out = nullptr;
        try { auto path = modulePath(); if (path.empty()) return E_FAIL; return SHStrDupW((path + L",-101").c_str(), out); } catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*, PWSTR* out) override { return out ? SHStrDupW(L"在快译中确认图片后处理", out) : E_POINTER; }
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* out) override { if (!out) return E_POINTER; *out = commandId; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* out) override {
        if (!out) return E_POINTER; *out = ECS_HIDDEN;
        // Menu construction only consults supplied Shell selection/path. No file open, decode, network or CLR.
        try { std::wstring path; if (SUCCEEDED(selectedPath(items,path))) *out = ECS_ENABLED; return S_OK; } catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray* items, IBindCtx*) override {
        try {
            std::wstring path; HRESULT result = selectedPath(items,path); if (FAILED(result)) return result;
            auto dll = modulePath(); auto separator = dll.find_last_of(L'\\'); if (separator == std::wstring::npos) return E_FAIL;
            auto root = dll.substr(0, separator); auto app = root + L"\\KuaiYi.App.exe";
            auto command = kuaiyi::quoteArgument(app) + L" --prepare-image " + kuaiyi::quoteArgument(path);
            STARTUPINFOW startup{sizeof(STARTUPINFOW)}; PROCESS_INFORMATION process{};
            // Fixed sibling EXE; argument escaping is Windows argv escaping, never cmd.exe. No image read before user confirmation.
            if (!CreateProcessW(app.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, root.c_str(), &startup, &process)) return HRESULT_FROM_WIN32(GetLastError());
            CloseHandle(process.hThread); CloseHandle(process.hProcess); return S_OK;
        } catch (...) { return E_OUTOFMEMORY; }
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* out) override { if (!out) return E_POINTER; *out = ECF_DEFAULT; return S_OK; }
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** out) override { if (!out) return E_POINTER; *out = nullptr; return E_NOTIMPL; }
};
class Factory final : public IClassFactory {
    std::atomic<ULONG> refs{1};
public:
    Factory() { ++objects; } ~Factory() { --objects; }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** out) override {
        if (!out) return E_POINTER; *out = nullptr;
        if (id == IID_IUnknown || id == IID_IClassFactory) { *out = static_cast<IClassFactory*>(this); AddRef(); return S_OK; } return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs; }
    ULONG STDMETHODCALLTYPE Release() override { ULONG count = --refs; if (!count) delete this; return count; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID id, void** out) override {
        if (!out) return E_POINTER; *out = nullptr; if (outer) return CLASS_E_NOAGGREGATION;
        auto instance = new(std::nothrow) Command(); if (!instance) return E_OUTOFMEMORY;
        HRESULT result = instance->QueryInterface(id,out); instance->Release(); return result;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override { if (lock) ++locks; else --locks; return S_OK; }
};
}
extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) { if (reason == DLL_PROCESS_ATTACH) { module = instance; DisableThreadLibraryCalls(instance); } return TRUE; }
extern "C" HRESULT __stdcall DllCanUnloadNow() { return objects.load() == 0 && locks.load() == 0 ? S_OK : S_FALSE; }
extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID iid, void** out) {
    if (!out) return E_POINTER; *out = nullptr;
    if (clsid != commandId) return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = new(std::nothrow) Factory(); if (!factory) return E_OUTOFMEMORY;
    HRESULT result = factory->QueryInterface(iid,out); factory->Release(); return result;
}
