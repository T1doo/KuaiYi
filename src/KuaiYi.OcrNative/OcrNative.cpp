#include <windows.h>
#include <appmodel.h>
#include <wincodec.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Globalization.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Media.Ocr.h>
#include <winrt/Windows.Storage.Streams.h>
#include <algorithm>
#include <cstdint>
#include <string>
#include <sstream>
#include <locale>
#include <limits>

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Globalization;
using namespace Windows::Graphics::Imaging;
using namespace Windows::Media::Ocr;
using namespace Windows::Storage::Streams;
namespace {
constexpr int inputLimit = 20 * 1024 * 1024, outputLimit = 1024 * 1024;
enum Code { Ok, IdentityMissing, LanguageUnavailable, InvalidImage, ImageTooLarge, OcrDimensionExceeded, Cancelled, NativeFailure };
struct Apartment { Apartment() { init_apartment(apartment_type::multi_threaded); } ~Apartment() { uninit_apartment(); } };
struct Failure { int code; };
bool stopped(HANDLE stop) { return stop && WaitForSingleObject(stop, 0) == WAIT_OBJECT_0; }
template<class T> auto wait(T const& operation, HANDLE stop) {
    while (operation.Status() == AsyncStatus::Started) {
        if (stopped(stop)) { operation.Cancel(); throw Failure{Cancelled}; }
        if (stop) { if (WaitForSingleObject(stop, 10) == WAIT_OBJECT_0) { operation.Cancel(); throw Failure{Cancelled}; } }
        else Sleep(10);
    }
    if (stopped(stop)) { operation.Cancel(); throw Failure{Cancelled}; }
    return operation.GetResults();
}
void requireIdentity() {
    UINT32 size = 0;
    LONG status = GetCurrentPackageFullName(&size, nullptr);
    if (status == APPMODEL_ERROR_NO_PACKAGE) throw Failure{IdentityMissing};
    if (status != ERROR_INSUFFICIENT_BUFFER || size == 0 || size > 4096) throw Failure{NativeFailure};
}
std::string jsonString(hstring const& value) {
    auto utf8 = to_string(value);
    std::string result = "\"";
    constexpr char hex[] = "0123456789abcdef";
    for (unsigned char c : utf8) {
        if (c == '"' || c == '\\') { result += '\\'; result += static_cast<char>(c); }
        else if (c < 32) { result += "\\u00"; result += hex[c >> 4]; result += hex[c & 15]; }
        else result += static_cast<char>(c);
    }
    result += '"'; return result;
}
void bounded(std::ostringstream& output) { if (output.tellp() > outputLimit) throw Failure{ImageTooLarge}; }
void copyOutput(std::string const& value, BYTE** bytes, int* length) {
    if (value.empty() || value.size() > outputLimit) throw Failure{ImageTooLarge};
    auto memory = static_cast<BYTE*>(CoTaskMemAlloc(value.size()));
    if (!memory) throw std::bad_alloc();
    memcpy(memory, value.data(), value.size()); *bytes = memory; *length = static_cast<int>(value.size());
}
uint32_t bigEndian(BYTE const* bytes) { return (uint32_t(bytes[0]) << 24) | (uint32_t(bytes[1]) << 16) | (uint32_t(bytes[2]) << 8) | bytes[3]; }
void validateHeader(BYTE const* bytes, int length) {
    constexpr BYTE signature[] = {137,80,78,71,13,10,26,10};
    if (!bytes || length < 33 || memcmp(bytes, signature, 8) || bigEndian(bytes + 8) != 13 || memcmp(bytes + 12, "IHDR", 4)) throw Failure{InvalidImage};
    if (length > inputLimit) throw Failure{ImageTooLarge};
    uint32_t width = bigEndian(bytes + 16), height = bigEndian(bytes + 20);
    if (!width || !height) throw Failure{InvalidImage};
    if (width > 16384 || height > 16384 || uint64_t(width) * height > 24000000) throw Failure{ImageTooLarge};
}
}
extern "C" __declspec(dllexport) void __cdecl KY_OcrFree(BYTE* bytes, int length) noexcept {
    if (bytes) { if (length > 0 && length <= outputLimit) SecureZeroMemory(bytes, static_cast<SIZE_T>(length)); CoTaskMemFree(bytes); }
}
extern "C" __declspec(dllexport) int __cdecl KY_OcrDiagnose(BYTE** bytes, int* length) noexcept {
    if (!bytes || !length) return NativeFailure;
    *bytes = nullptr; *length = 0;
    try {
        requireIdentity(); Apartment apartment;
        std::ostringstream output; output.imbue(std::locale::classic());
        output << "{\"maxImageDimension\":" << OcrEngine::MaxImageDimension() << ",\"languages\":[";
        bool first = true;
        for (auto const& language : OcrEngine::AvailableRecognizerLanguages()) {
            if (!first) output << ','; first = false; output << jsonString(language.LanguageTag()); bounded(output);
        }
        output << "]}"; copyOutput(output.str(), bytes, length); return Ok;
    } catch (Failure const& e) { return e.code; } catch (...) { return NativeFailure; }
}
extern "C" __declspec(dllexport) int __cdecl KY_OcrRecognize(BYTE const* input, int length, wchar_t const* languageTag, HANDLE stop, BYTE** bytes, int* outputLength) noexcept {
    if (!bytes || !outputLength) return NativeFailure;
    *bytes = nullptr; *outputLength = 0;
    try {
        if (stopped(stop)) return Cancelled;
        validateHeader(input, length); requireIdentity(); Apartment apartment;
        if (!languageTag || wcsnlen_s(languageTag, 65) > 64) return LanguageUnavailable;
        Language language(languageTag);
        if (!OcrEngine::IsLanguageSupported(language)) return LanguageUnavailable;
        auto engine = OcrEngine::TryCreateFromLanguage(language);
        if (!engine) return LanguageUnavailable;
        InMemoryRandomAccessStream stream;
        DataWriter writer(stream);
        writer.WriteBytes(array_view<uint8_t const>(input, input + length));
        wait(writer.StoreAsync(), stop); wait(writer.FlushAsync(), stop); writer.DetachStream(); stream.Seek(0);
        auto decoder = wait(BitmapDecoder::CreateAsync(stream), stop);
        uint32_t width = decoder.PixelWidth(), height = decoder.PixelHeight();
        if (!width || !height) return InvalidImage;
        if (width > 16384 || height > 16384 || uint64_t(width) * height > 24000000) return ImageTooLarge;
        if (width != bigEndian(input + 16) || height != bigEndian(input + 20)) return InvalidImage;
        if (width > OcrEngine::MaxImageDimension() || height > OcrEngine::MaxImageDimension()) return OcrDimensionExceeded;
        // P0 PNG minimum. Complete EXIF/alpha/resampling and SkiaSharp format baseline are still P6 work.
        auto bitmap = wait(decoder.GetSoftwareBitmapAsync(BitmapPixelFormat::Bgra8, BitmapAlphaMode::Ignore), stop);
        auto result = wait(engine.RecognizeAsync(bitmap), stop);
        std::ostringstream output; output.imbue(std::locale::classic());
        output << "{\"width\":" << width << ",\"height\":" << height << ",\"language\":" << jsonString(language.LanguageTag())
            << ",\"text\":" << jsonString(result.Text()) << ",\"lines\":[";
        bool firstLine = true;
        for (auto const& line : result.Lines()) {
            if (!firstLine) output << ','; firstLine = false;
            output << "{\"text\":" << jsonString(line.Text()) << ",\"words\":[";
            bool firstWord = true;
            for (auto const& word : line.Words()) {
                if (!firstWord) output << ','; firstWord = false;
                Rect rect = word.BoundingRect();
                output << "{\"text\":" << jsonString(word.Text()) << ",\"x\":" << rect.X << ",\"y\":" << rect.Y
                    << ",\"width\":" << rect.Width << ",\"height\":" << rect.Height << '}'; bounded(output);
            }
            output << "]}"; bounded(output);
        }
        output << "]}"; bitmap.Close(); stream.Close();
        if (stopped(stop)) return Cancelled;
        copyOutput(output.str(), bytes, outputLength); return Ok;
    } catch (Failure const& e) { return e.code; }
      catch (hresult_canceled const&) { return Cancelled; }
      catch (hresult_error const& e) { return e.code() == WINCODEC_ERR_BADIMAGE || e.code() == WINCODEC_ERR_BADHEADER || e.code() == WINCODEC_ERR_UNKNOWNIMAGEFORMAT ? InvalidImage : NativeFailure; }
      catch (...) { return NativeFailure; }
}
