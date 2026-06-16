using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Fragaria.Services;

/// <summary>
/// Per-process audio capture using Windows 10 2004+ Process Loopback API.
/// Each window strip maps HWND → PID and captures only that process tree.
/// </summary>
public sealed class ProcessAudioCapture : IWaveIn, IDisposable
{
    private readonly uint _processId;
    private WasapiLoopbackCapture? _fallback;
    private IWaveIn? _active;
    private bool _disposed;

    public ProcessAudioCapture(uint processId, string windowTitle)
    {
        _processId = processId;
        WindowTitle = windowTitle;

        if (ProcessLoopbackActivator.TryCreate(processId, out var device))
        {
            var cap = new WasapiCapture(device) { ShareMode = AudioClientShareMode.Shared };
            _active = cap;
            WaveFormat = cap.WaveFormat;
        }
        else
        {
            // Session title match for browser tabs / multi-session apps
            var session = AudioSessionMatcher.FindBestSession(processId, windowTitle);
            if (session != null)
            {
                _active = session;
                WaveFormat = session.WaveFormat;
            }
            else
            {
                _fallback = new WasapiLoopbackCapture();
                _active = _fallback;
                WaveFormat = _fallback.WaveFormat;
                ProcessFilterPid = processId;
            }
        }

        _active.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
    }

    public string WindowTitle { get; }
    public uint? ProcessFilterPid { get; }
    public WaveFormat WaveFormat { get; }

    public event EventHandler<WaveInEventArgs>? DataAvailable;
#pragma warning disable CS0067
    public event EventHandler<StoppedEventArgs>? RecordingStopped;
#pragma warning restore CS0067

    public void StartRecording() => _active?.StartRecording();
    public void StopRecording() => _active?.StopRecording();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _active?.Dispose();
        _fallback?.Dispose();
    }
}

internal static class ProcessLoopbackActivator
{
    public static bool TryCreate(uint processId, out MMDevice device)
    {
        device = null!;
        try
        {
            var hr = AudioLoopback.CreateProcessLoopbackDevice(processId, out var ptr);
            if (hr != 0 || ptr == IntPtr.Zero) return false;
            device = new MMDevice(ptr);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal static class AudioLoopback
{
    private const int AUDCLNT_E_DEVICE_INVALIDATED = unchecked((int)0x88890004);

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientActivationParams
    {
        public int ActivationType;
        public AudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientProcessLoopbackParams
    {
        public uint TargetProcessId;
        public uint ProcessLoopbackMode;
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-84353D8B5A0F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult(out int hr, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
    }

    private sealed class Handler : IActivateAudioInterfaceCompletionHandler
    {
        private readonly ManualResetEventSlim _evt = new(false);
        public int HResult { get; private set; }
        public object? Device { get; private set; }

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            operation.GetActivateResult(out var hr, out var obj);
            HResult = hr;
            Device = obj;
            _evt.Set();
        }

        public (int hr, object? device) Wait(int ms = 3000)
        {
            _evt.Wait(ms);
            return (HResult, Device);
        }
    }

    [DllImport("mmdevapi.dll", PreserveSig = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    private static readonly Guid IID_IAudioClient = new("1CB0ADFC-DBF8-4C32-B178-0472F5687B6B");

    public static int CreateProcessLoopbackDevice(uint pid, out IntPtr mmDevicePtr)
    {
        mmDevicePtr = IntPtr.Zero;
        var loopbackParams = new AudioClientProcessLoopbackParams
        {
            TargetProcessId = pid,
            ProcessLoopbackMode = 0 // INCLUDE_TARGET_PROCESS_TREE
        };

        var activation = new AudioClientActivationParams
        {
            ActivationType = 1, // PROCESS_LOOPBACK
            ProcessLoopbackParams = loopbackParams
        };

        var size = Marshal.SizeOf<AudioClientActivationParams>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(activation, ptr, false);
            var handler = new Handler();
            var hr = ActivateAudioInterfaceAsync(
                "VAD\\Process_Loopback",
                ref IID_IAudioClient,
                ptr,
                handler,
                out _);

            if (hr != 0) return hr;

            var (actHr, device) = handler.Wait();
            if (actHr != 0 || device == null) return actHr;

            // NAudio MMDevice from IMMDevice pointer
            mmDevicePtr = Marshal.GetIUnknownForObject(device);
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
