#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <memory>
#include <atomic>
#include <cstring>
#include "GdiReadContract.hpp"
using namespace winrt;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;
namespace {
struct EventState {
    HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    std::atomic<bool> closed = false;
    ~EventState() { if (ready) CloseHandle(ready); }
};
struct Apartment { Apartment() { init_apartment(apartment_type::multi_threaded); } ~Apartment() { uninit_apartment(); } };
bool valid(int width, int height, int length, const void* bytes) {
    return bytes && width > 0 && height > 0 && width <= 16384 && height <= 16384 &&
        static_cast<long long>(width) * height <= 32000000 && static_cast<long long>(width) * height * 4 == length;
}
struct Dc { HDC value; HWND window; bool owned; bool& failed; ~Dc() { if (value) { if (owned) { if (!DeleteDC(value)) failed=true; } else if (!ReleaseDC(window,value)) failed=true; } } };
struct Dib {
    HDC dc; HBITMAP bitmap; HGDIOBJ previous; void* bits; SIZE_T bytes; bool& failed;
    ~Dib() {
        if (bits && !kuaiyi::ClearGdiPixels([] { return GdiFlush()!=FALSE; },[&] { SecureZeroMemory(bits,bytes); })) failed=true;
        if (previous) { auto restored=SelectObject(dc,previous); if (!restored || restored==HGDI_ERROR) failed=true; }
        if (bitmap && !DeleteObject(bitmap)) failed=true;
    }
};
}
// Caller-owned top-down BGRA output, fixed physical size. 1 unavailable / 2 cancelled / 3 timeout / 4 changed / 5 failure.
extern "C" __declspec(dllexport) int __cdecl KY_CaptureMonitor(HMONITOR monitor,int width,int height,HANDLE stop,unsigned char* output,int length) noexcept {
    if (!valid(width,height,length,output) || !stop) return 5;
    try {
        Apartment apartment;
        bool cleanupFailed=false;int outcome=[&]() -> int {
        if (!GraphicsCaptureSession::IsSupported()) return 1;
        if (WaitForSingleObject(stop,0) == WAIT_OBJECT_0) return 2;
        auto factory = get_activation_factory<GraphicsCaptureItem,IGraphicsCaptureItemInterop>();
        GraphicsCaptureItem item{nullptr};
        check_hresult(factory->CreateForMonitor(monitor,guid_of<GraphicsCaptureItem>(),put_abi(item)));
        auto size = item.Size(); if (size.Width != width || size.Height != height) return 4;
        com_ptr<ID3D11Device> device; com_ptr<ID3D11DeviceContext> context;
        check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,device.put(),nullptr,context.put()));
        auto dxgi = device.as<IDXGIDevice>(); com_ptr<IInspectable> inspectable;
        check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(),inspectable.put()));
        auto direct = inspectable.as<IDirect3DDevice>();
        auto state = std::make_shared<EventState>(); if (!state->ready) return 5;
        auto pool = Direct3D11CaptureFramePool::CreateFreeThreaded(direct,DirectXPixelFormat::B8G8R8A8UIntNormalized,1,size);
        auto arrived = pool.FrameArrived(auto_revoke,[state](auto const&,auto const&) { SetEvent(state->ready); });
        auto closed = item.Closed(auto_revoke,[state](auto const&,auto const&) { state->closed = true; SetEvent(state->ready); });
        auto session = pool.CreateCaptureSession(item);
        struct Close { GraphicsCaptureSession session; Direct3D11CaptureFramePool pool; bool& failed;
            ~Close() noexcept { try { session.Close(); } catch (...) { failed=true; } try { pool.Close(); } catch (...) { failed=true; } }
        } close{session,pool,cleanupFailed};
        session.IsCursorCaptureEnabled(false); session.StartCapture();
        HANDLE waits[]{stop,state->ready}; DWORD result = WaitForMultipleObjects(2,waits,FALSE,2000);
        if (result == WAIT_OBJECT_0) return 2;
        if (result == WAIT_TIMEOUT) return 3;
        if (result != WAIT_OBJECT_0+1 || state->closed) return 4;
        auto frame = pool.TryGetNextFrame(); if (!frame) return 5;
        struct FrameClose { Direct3D11CaptureFrame frame; bool& failed; ~FrameClose() noexcept { try { frame.Close(); } catch (...) { failed=true; } } } frameClose{frame,cleanupFailed};
        auto actual = frame.ContentSize(); if (actual.Width != width || actual.Height != height) return 4;
        auto access = frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
        com_ptr<ID3D11Texture2D> texture; check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D),texture.put_void()));
        D3D11_TEXTURE2D_DESC desc{}; texture->GetDesc(&desc);
        if (desc.Width != static_cast<UINT>(width) || desc.Height != static_cast<UINT>(height) || desc.Format != DXGI_FORMAT_B8G8R8A8_UNORM) return 4;
        desc.BindFlags = 0; desc.MiscFlags = 0; desc.Usage = D3D11_USAGE_STAGING; desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        com_ptr<ID3D11Texture2D> staging; check_hresult(device->CreateTexture2D(&desc,nullptr,staging.put())); context->CopyResource(staging.get(),texture.get());
        D3D11_MAPPED_SUBRESOURCE mapped{}; check_hresult(context->Map(staging.get(),0,D3D11_MAP_READ,0,&mapped));
        struct Unmap { ID3D11DeviceContext* context; ID3D11Texture2D* texture; ~Unmap() { context->Unmap(texture,0); } } unmap{context.get(),staging.get()};
        for (int y=0;y<height;y++) {
            if (WaitForSingleObject(stop,0) == WAIT_OBJECT_0) { SecureZeroMemory(output,static_cast<SIZE_T>(length)); return 2; }
            std::memcpy(output+static_cast<SIZE_T>(y)*width*4,static_cast<unsigned char*>(mapped.pData)+static_cast<SIZE_T>(y)*mapped.RowPitch,static_cast<SIZE_T>(width)*4);
        }
        return 0;
        }();
        if (cleanupFailed || outcome!=0) SecureZeroMemory(output,static_cast<SIZE_T>(length));
        return cleanupFailed?5:outcome;
    } catch (...) { SecureZeroMemory(output,static_cast<SIZE_T>(length)); return 5; }
}
// Explicitly selected compatibility backend; never an automatic retry after a protected/black WGC frame.
extern "C" __declspec(dllexport) int __cdecl KY_CaptureGdi(int left,int top,int width,int height,HANDLE stop,unsigned char* output,int length) noexcept {
    if (!valid(width,height,length,output) || !stop) return 5;
    if (WaitForSingleObject(stop,0) == WAIT_OBJECT_0) { SecureZeroMemory(output,static_cast<SIZE_T>(length)); return 2; }
    bool cleanupFailed=false;int outcome=[&]() -> int {
    Dc screen{GetDC(nullptr),nullptr,false,cleanupFailed}; if (!screen.value) return 5;
    Dc memory{CreateCompatibleDC(screen.value),nullptr,true,cleanupFailed}; if (!memory.value) return 5;
    BITMAPINFO info{}; info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);info.bmiHeader.biWidth=width;info.bmiHeader.biHeight=-height;info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;info.bmiHeader.biCompression=BI_RGB;
    void* bits=nullptr; HBITMAP bitmap=CreateDIBSection(screen.value,&info,DIB_RGB_COLORS,&bits,nullptr,0); if (!bitmap) return 5;
    Dib dib{memory.value,bitmap,nullptr,bits,static_cast<SIZE_T>(length),cleanupFailed}; dib.previous=SelectObject(memory.value,bitmap);
    if (!dib.previous || dib.previous==HGDI_ERROR) { dib.previous=nullptr; return 5; }
    bool drawn=BitBlt(memory.value,0,0,width,height,screen.value,left,top,SRCCOPY|CAPTUREBLT)!=FALSE;
    return kuaiyi::ReadGdiPixels(drawn,[] { return GdiFlush()!=FALSE; },
        [&] { return WaitForSingleObject(stop,0)==WAIT_OBJECT_0; },
        [&] { std::memcpy(output,bits,static_cast<SIZE_T>(length)); });
    }();
    if (cleanupFailed || outcome!=0) SecureZeroMemory(output,static_cast<SIZE_T>(length));
    return cleanupFailed?5:outcome;
}
