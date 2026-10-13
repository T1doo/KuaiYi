#pragma once
#include <array>
#include <cmath>
#include <cstdint>
#include <span>
#include <string_view>
#include <string>
#include <vector>
namespace kuaiyi::policy {
constexpr int soft_ms=250, text_units=100002, text_scalars=50000, candidate_limit=64, range_limit=64, rectangle_limit=256;
inline bool current(bool window,bool foreground,std::uint32_t expected,std::uint32_t actual,bool process_active,std::int64_t expected_start,std::int64_t actual_start) {
    return window&&foreground&&expected!=0&&expected==actual&&process_active&&expected_start>0&&expected_start==actual_start;
}
inline bool elevated(std::uint32_t source,std::uint32_t own) {return source>own;}
template<class C> bool excluded_name(std::basic_string_view<C> name) {
    std::string ascii;ascii.reserve(name.size());
    for(C c:name){auto value=static_cast<std::uint32_t>(c);if(value>127){ascii+='\x7f';continue;}char letter=static_cast<char>(value);ascii+=letter>='A'&&letter<='Z'?static_cast<char>(letter+32):letter;}
    if(ascii.ends_with(".exe"))ascii.resize(ascii.size()-4);
    return ascii.starts_with("kuaiyi")||ascii=="cmd"||ascii=="powershell"||ascii=="pwsh"||ascii=="windowsterminal"||ascii=="conhost";
}
inline bool soft_expired(std::int64_t elapsed) {return elapsed>=soft_ms;}
struct soft_observation {
    int first_stage=0;
    bool observe(std::int64_t elapsed,int stage) {
        if(!soft_expired(elapsed))return false;
        if(first_stage==0)first_stage=stage;
        return true;
    }
    int stage_or(int fallback) const {return first_stage==0?fallback:first_stage;}
};
inline bool whitespace(std::uint32_t c) {return (c>=9&&c<=13)||c==32||c==0x85||c==0xa0||c==0x1680||(c>=0x2000&&c<=0x200a)||c==0x2028||c==0x2029||c==0x202f||c==0x205f||c==0x3000;}
template<class C> bool blank(std::basic_string_view<C> text) {for(C c:text)if(!whitespace(static_cast<std::uint32_t>(c)))return false;return true;}
template<class C> bool valid_text(std::basic_string_view<C> text) {
    if(text.size()>=text_units)return false;
    int scalars=0;
    for(std::size_t i=0;i<text.size();++i) {
        auto c=static_cast<std::uint32_t>(text[i]);
        if(c>=0xd800&&c<=0xdbff) {if(++i==text.size())return false;auto low=static_cast<std::uint32_t>(text[i]);if(low<0xdc00||low>0xdfff)return false;}
        else if(c>=0xdc00&&c<=0xdfff)return false;
        if(++scalars>text_scalars)return false;
    }return true;
}
// Mirror managed WPF conversion FIRST (nonpositive dimensions -> Rect.Empty), then the existing aggregate policy.
inline bool append_rectangles(std::span<const double> raw,std::vector<double>& destination,int& observed) {
    if(raw.size()%4!=0||destination.size()%4!=0||destination.size()>1024||observed<0||observed>rectangle_limit)return false;
    int count=observed;std::vector<double> staged;
    for(std::size_t i=0;i<raw.size();i+=4) {
        if(++count>rectangle_limit)return false;
        if(raw[i+2]<=0||raw[i+3]<=0)continue;
        for(int k=0;k<4;++k)if(!std::isfinite(raw[i+static_cast<std::size_t>(k)]))return false;
        if(destination.size()+staged.size()>1020)return false;
        staged.insert(staged.end(),raw.begin()+static_cast<std::ptrdiff_t>(i),raw.begin()+static_cast<std::ptrdiff_t>(i+4));
    }destination.insert(destination.end(),staged.begin(),staged.end());observed=count;return true;
}
inline bool contains(const std::array<double,4>& rect,int x,int y) {return x>=rect[0]&&y>=rect[1]&&x<=rect[0]+rect[2]&&y<=rect[1]+rect[3];}
inline bool associated(std::span<const double> rects,int down_x,int down_y,int up_x,int up_y) {
    for(std::size_t i=0;i+3<rects.size();i+=4)if(contains({rects[i],rects[i+1],rects[i+2],rects[i+3]},down_x,down_y)||contains({rects[i],rects[i+1],rects[i+2],rects[i+3]},up_x,up_y))return true;
    return false;
}
}
