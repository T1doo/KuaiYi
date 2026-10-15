using KuaiYi.Core;
namespace System.Windows.Threading
{
    // The Linux test port models deferred dispatch only; it does not execute WPF layout or routed input.
    internal enum DispatcherPriority { Loaded }
}
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        private sealed class BodyPort
        {
            public double Offset;
            public int ScrollCalls;
            public void ScrollToEnd(){Offset=1000;ScrollCalls++;}
            public void ScrollToVerticalOffset(double value){Offset=value;ScrollCalls++;}
        }
        private sealed class DispatcherPort
        {
            private readonly List<Action> callbacks=[];
            public void BeginInvoke(System.Windows.Threading.DispatcherPriority priority,Action action)=>callbacks.Add(action);
            public void Run(int index){var action=callbacks[index];callbacks.RemoveAt(index);action();}
        }
        private sealed class LifecyclePort { public RuntimeGate Gate {get;}=new(); }
        private readonly BodyPort body=new();
        private readonly LifecyclePort lifecycle=new();
        private readonly DispatcherPort Dispatcher=new();
        private long resultRevision=1;
        private bool exiting,follow=true;
        internal double ScrollOffset=>body.Offset;
        internal int ScrollCalls=>body.ScrollCalls;
        internal void QueueScroll(bool bottom,double offset)=>ScheduleBodyScrollRestore(bottom,offset);
        internal void ReaderScroll(double offset){InvalidateBodyScrollRestore();follow=false;body.Offset=offset;}
        internal void ReaderFollowEnd()=>FollowBodyEnd();
        internal void HideOrInvalidate()=>InvalidateBodyScrollRestore();
        internal void ReplaceResult()=>resultRevision++;
        internal void Lock()=>lifecycle.Gate.SetSessionLocked(true);
        internal void Exit()=>exiting=true;
        internal void Deliver(int index=0)=>Dispatcher.Run(index);
    }
}
