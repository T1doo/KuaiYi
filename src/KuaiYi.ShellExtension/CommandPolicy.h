#pragma once
#include <string>
#include <string_view>
namespace kuaiyi {
inline std::wstring quoteArgument(std::wstring_view value) {
    std::wstring result = L"\""; size_t slashes = 0;
    for (wchar_t c : value) {
        if (c == L'\\') { ++slashes; continue; }
        if (c == L'"') { result.append(slashes * 2 + 1, L'\\'); result += c; }
        else { result.append(slashes, L'\\'); result += c; }
        slashes = 0;
    }
    result.append(slashes * 2, L'\\'); result += L'"'; return result;
}
inline bool supportedPath(std::wstring_view path) {
    if (path.size() < 3 || !((path[0] >= L'A' && path[0] <= L'Z') || (path[0] >= L'a' && path[0] <= L'z')) || path[1] != L':' || path[2] != L'\\' || path.find(L':', 2) != std::wstring_view::npos) return false;
    auto dot = path.find_last_of(L'.'); if (dot == std::wstring_view::npos) return false;
    std::wstring extension(path.substr(dot));
    for (auto& c : extension) if (c >= L'A' && c <= L'Z') c = static_cast<wchar_t>(c + L'a' - L'A');
    return extension == L".png" || extension == L".jpg" || extension == L".jpeg" || extension == L".webp" || extension == L".bmp" || extension == L".gif";
}
}
