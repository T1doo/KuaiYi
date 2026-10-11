#pragma once
// Shared with executable portable contract tests. Every callback runs synchronously on the owning capture thread.
namespace kuaiyi {
template<class Flush,class Cancel,class Copy>
int ReadGdiPixels(bool drawingSucceeded,Flush flush,Cancel cancelled,Copy copy) {
    if (!flush()) return 5; // Even failed/partially queued drawing requires synchronization before CPU access.
    if (!drawingSucceeded) return 5;
    if (cancelled()) return 2;
    copy(); return 0;
}
template<class Flush,class Clear>
bool ClearGdiPixels(Flush flush,Clear clear) {
    if (!flush()) return false; // Never race CPU zeroing against unfinished GDI drawing.
    clear(); return true;
}
}
