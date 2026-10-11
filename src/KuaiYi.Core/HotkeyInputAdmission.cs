namespace KuaiYi.Core;
public readonly record struct HotkeyInputIdentity(Guid Gate,long Revision,long Epoch);
public sealed record HotkeyInputDispatch(HotkeyDispatch Binding,HotkeyInputIdentity Input);
/// <summary>Production admission before the native HWND queue is handed to the UI queue.</summary>
public static class HotkeyInputAdmission
{
    public static HotkeyInputDispatch? Resolve(HotkeyRegistry registry,RuntimeGate gate,int id,HotkeyChord chord,uint messageTick)
    {
        if(!gate.TryAdmitHotkeyInput(messageTick,out var input))return null;
        var binding=registry.Resolve(id,chord,messageTick);
        return binding==null||!gate.IsHotkeyInputCurrent(input)?null:new(binding,input);
    }
    public static bool Current(HotkeyRegistry registry,RuntimeGate gate,HotkeyInputDispatch dispatch)
        =>registry.Current(dispatch.Binding)&&gate.IsHotkeyInputCurrent(dispatch.Input);
}
public sealed partial class RuntimeGate
{
    private readonly Func<uint> hotkeyUptime;
    private readonly Guid hotkeyGate=Guid.NewGuid();private long hotkeyRevision;private uint hotkeyFence;private bool hotkeyInputFaulted;
    private void AdvanceHotkeyInputLocked(){hotkeyRevision++;try{hotkeyFence=hotkeyUptime();hotkeyInputFaulted=false;}catch{hotkeyInputFaulted=true;throw;}}
    public bool TryAdmitHotkeyInput(uint messageTick,out HotkeyInputIdentity input)
    {
        lock(sync)
        {
            input=default;if(hotkeyInputFaulted||unchecked((int)(messageTick-hotkeyFence))<=0)return false;
            input=new(hotkeyGate,hotkeyRevision,epoch);return true;
        }
    }
    public bool TryBeginHotkeyPassiveCapture(HotkeyInputIdentity input,out CaptureTicket ticket)
    {lock(sync){ticket=default;return IsHotkeyInputCurrent(input)&&TryBeginPassiveCapture(mode,out ticket);}}
    public bool IsHotkeyInputCurrent(HotkeyInputIdentity input)
    {lock(sync)return !hotkeyInputFaulted&&input.Gate==hotkeyGate&&input.Revision==hotkeyRevision&&input.Epoch==epoch;}
}
