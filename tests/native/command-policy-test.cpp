#include "../../src/KuaiYi.ShellExtension/CommandPolicy.h"
#include <stdexcept>
void require(bool value) { if (!value) throw std::runtime_error("Command policy assertion failed"); }
int main() {
 using namespace kuaiyi;
 require(quoteArgument(L"")==L"\"\"");
 require(quoteArgument(L"C:\\Images\\a b.png")==L"\"C:\\Images\\a b.png\"");
 require(quoteArgument(L"a\"b")==L"\"a\\\"b\"");
 require(quoteArgument(L"a\\")==L"\"a\\\\\"");
 require(quoteArgument(L"a\\\"b")==L"\"a\\\\\\\"b\"");
 require(supportedPath(L"C:\\x.PNG"));require(supportedPath(L"D:\\image.webp"));require(supportedPath(L"C:\\x.gif"));
 require(!supportedPath(L"\\\\server\\x.png"));require(!supportedPath(L"C:relative.png"));require(!supportedPath(L"C:\\x.png:stream"));
 require(!supportedPath(L"C:\\x.tiff"));require(!supportedPath(L"C:\\x.svg"));require(!supportedPath(L"C:\\x.png.exe"));
}
