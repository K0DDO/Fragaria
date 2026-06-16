using System.Runtime.InteropServices;

namespace Fragaria.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct AudioClientActivationParams
{
    public AudioClientActivationType ActivationType;
    public AudioClientProcessLoopbackParams ProcessLoopbackParams;
}

internal enum AudioClientActivationType : uint
{
    Default = 0,
    ProcessLoopback = 1
}

internal enum ProcessLoopbackMode : uint
{
    IncludeTargetProcessTree = 0,
    ExcludeTargetProcessTree = 1
}

[StructLayout(LayoutKind.Sequential)]
internal struct AudioClientProcessLoopbackParams
{
    public uint TargetProcessId;
    public ProcessLoopbackMode ProcessLoopbackMode;
}

[ComImport]
[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActivateAudioInterfaceCompletionHandler
{
    void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
}

[ComImport]
[Guid("72A22D78-CDE4-431D-B8CC-84353D8B5A0F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActivateAudioInterfaceAsyncOperation
{
    void GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
}

internal static class AudioActivation
{
    [DllImport("mmdevapi.dll", ExactSpelling = true, PreserveSig = true)]
    internal static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        nint activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    internal static readonly Guid IID_IAudioClient = new("1CB0ADFC-DBF8-4C32-B178-0472F5687B6B");
    internal static readonly Guid VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK =
        new("VAD\\Process_Loopback");
}
