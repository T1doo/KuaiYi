#include "../../tools/KuaiYi.ComCaptureProbe/SelectionPolicy.h"
#include <cstdlib>
#include <iostream>
#include <limits>
#include <string>
namespace p=kuaiyi::policy;
int checks=0;
void require(bool value) {if(!value)std::abort();++checks;}
int main() {
    require(p::soft_ms==250&&p::text_units==100002&&p::text_scalars==50000&&p::candidate_limit==64&&p::range_limit==64&&p::rectangle_limit==256);
    require(p::current(true,true,3,3,true,7,7));require(!p::current(true,true,3,4,true,7,7));require(!p::current(true,false,3,3,true,7,7));require(!p::current(true,true,3,3,true,7,8));require(!p::current(true,true,3,3,false,7,7));
    require(p::excluded_name(std::u16string_view(u"KUAIYI.Tools.EXE")));require(p::excluded_name(std::u16string_view(u"CMD.EXE")));require(p::excluded_name(std::u16string_view(u"pwsh")));require(p::excluded_name(std::u16string_view(u"conhost.exe")));require(!p::excluded_name(std::u16string_view(u"chrome.exe")));require(p::excluded_name(std::u16string_view(u"KuaiYi_\u4e2d\u6587.exe")));require(!p::excluded_name(std::u16string_view(u"cmd_\u4e2d.exe")));require(p::elevated(0x3000,0x2000));require(!p::elevated(0x2000,0x2000));require(!p::elevated(0x1000,0x2000));
    require(!p::soft_expired(249));require(p::soft_expired(250));require(p::soft_expired(251));
    require(p::valid_text(std::u16string_view(u"translation")));require(p::valid_text(std::u16string_view(u"a\U0001f600")));require(!p::valid_text(std::u16string_view(std::u16string(1,static_cast<char16_t>(0xd800)))));require(!p::valid_text(std::u16string_view(std::u16string(1,static_cast<char16_t>(0xdc00)))));
    std::u16string fifty(50000,u'a');require(p::valid_text(std::u16string_view(fifty)));fifty+=u'a';require(!p::valid_text(std::u16string_view(fifty)));std::u16string units(100002,u'a');require(!p::valid_text(std::u16string_view(units)));
    require(p::blank(std::u16string_view(u" \t\r\n\u3000")));require(!p::blank(std::u16string_view(u"\u200b")));require(!p::blank(std::u16string_view(u"text")));
    int observed=0;std::vector<double> dest;require(p::append_rectangles(std::array<double,8>{0,0,0,0,1,2,3,4},dest,observed));require(observed==2&&dest==std::vector<double>({1,2,3,4}));
    auto before=dest;int count=observed;require(!p::append_rectangles(std::array<double,4>{0,0,std::numeric_limits<double>::quiet_NaN(),1},dest,observed));require(dest==before&&observed==count);
    std::vector<double> empty(255*4,0);dest.clear();observed=0;require(p::append_rectangles(empty,dest,observed));require(observed==255&&dest.empty());require(!p::append_rectangles(std::array<double,8>{0,0,0,0,0,0,0,0},dest,observed));require(observed==255&&dest.empty());
    require(p::associated(std::array<double,4>{0,0,10,10},0,0,10,10));require(!p::associated(std::array<double,4>{0,0,10,10},20,20,21,21));require(!p::associated({},0,0,1,1));
    std::cout<<"COM_SELECTION_POLICY passed="<<checks<<" failed=0; no Windows COM proof\n";
}
