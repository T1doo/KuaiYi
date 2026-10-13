#pragma once
#include <string>
#include <memory>
#include <stdexcept>
#include <algorithm>
namespace kuaiyi::reuse {
inline void erase(void* data,std::size_t bytes)noexcept{auto p=static_cast<volatile unsigned char*>(data);while(bytes--)*p++=0;}
struct ledger {int owners=0,texts=0,buffers=0;bool clean=true;bool settled()const noexcept{return clean&&owners==0&&texts==0&&buffers==0;}};
inline thread_local ledger* current=nullptr;
inline thread_local void(*erased_for_test)(const void*,std::size_t)=nullptr;
struct request_scope {
    ledger state;
    request_scope(){if(current)throw std::logic_error("Overlapping request.");current=&state;}
    ~request_scope(){current=nullptr;}
    request_scope(const request_scope&)=delete;
};
struct lease {
    ledger* state=current;int ledger::*field;
    explicit lease(int ledger::*value):field(value){if(state)++(state->*field);}
    ~lease(){if(state)--(state->*field);}
    lease(const lease&)=delete;
};
template<class C> struct secure_allocator:std::allocator<C> {
    using value_type=C;
    secure_allocator()=default;
    template<class U> secure_allocator(const secure_allocator<U>&)noexcept{}
    C* allocate(std::size_t n){C* p=std::allocator<C>{}.allocate(n);if(current)++current->buffers;return p;}
    void deallocate(C* p,std::size_t n)noexcept{erase(p,n*sizeof(C));if(erased_for_test)erased_for_test(p,n*sizeof(C));if(current)--current->buffers;std::allocator<C>{}.deallocate(p,n);}
    template<class U> bool operator==(const secure_allocator<U>&)const noexcept{return true;}
};
template<class C> class request_string:public std::basic_string<C,std::char_traits<C>,secure_allocator<C>> {
    using base=std::basic_string<C,std::char_traits<C>,secure_allocator<C>>;
    lease owned{&ledger::texts};
    void wipe()noexcept{try{this->resize(this->capacity());erase(this->data(),this->size()*sizeof(C));this->clear();}catch(...){erase(this->data(),this->size()*sizeof(C));if(owned.state)owned.state->clean=false;}}
public:
    // Reserve heap storage before any text assignment; avoid secret remnants in an SSO union.
    request_string(){base::reserve(64);}
    request_string(const C* p):request_string(){base::assign(p);}
    request_string(const C* p,std::size_t n):request_string(){base::assign(p,n);}
    request_string(std::size_t n,C c):request_string(){base::assign(n,c);}
    request_string(const request_string& other):request_string(){base::assign(other.data(),other.size());}
    request_string(request_string&& other)noexcept:base(std::move(other)){other.wipe();}
    request_string& operator=(const request_string& other){if(this!=&other){wipe();base::operator=(other);}return *this;}
    request_string& operator=(request_string&& other)noexcept{if(this!=&other){wipe();base::operator=(std::move(other));other.wipe();}return *this;}
    request_string& operator=(const C* p){if(this->capacity()<64)base::reserve(64);base::assign(p);return *this;}
    request_string& assign(const C* p,std::size_t n){if(this->capacity()<64)base::reserve(64);base::assign(p,n);return *this;}
    ~request_string(){wipe();}
    request_string substr(std::size_t pos=0,std::size_t n=base::npos)const{if(pos>this->size())throw std::out_of_range("Substring.");return request_string(this->data()+pos,std::min(n,this->size()-pos));}
};
using request_wstring=request_string<wchar_t>;
using request_bytes=request_string<char>;
// Counts a request-local owner, then releases its actual interface before the owner lease ends.
template<class T> class request_com {
    lease owned{&ledger::owners};T* value=nullptr;
public:
    request_com()=default;
    explicit request_com(T* p):value(p){if(value)value->AddRef();}
    request_com(const request_com& other):value(other.value){if(value)value->AddRef();}
    request_com(request_com&& other)noexcept:value(other.value){other.value=nullptr;}
    request_com& operator=(const request_com& other){if(this!=&other){Reset();value=other.value;if(value)value->AddRef();}return *this;}
    request_com& operator=(request_com&& other)noexcept{if(this!=&other){Reset();value=other.value;other.value=nullptr;}return *this;}
    ~request_com(){Reset();}
    void Reset()noexcept{if(value){auto p=value;value=nullptr;p->Release();}}
    T* Get()const noexcept{return value;}
    T** GetAddressOf(){if(value)throw std::logic_error("Nonempty output owner.");return &value;}
    T* operator->()const noexcept{return value;}
    explicit operator bool()const noexcept{return value!=nullptr;}
};
struct owned_buffer {
    lease owned{&ledger::buffers};void* data;std::size_t bytes;
    owned_buffer(void* p,std::size_t n):data(p),bytes(n){}
    ~owned_buffer(){erase(data,bytes);if(erased_for_test)erased_for_test(data,bytes);}
};
}
