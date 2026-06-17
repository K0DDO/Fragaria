using System.Runtime.InteropServices;
using Fragaria.Native;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Fragaria.Services;

/// <summary>Per-process WASAPI loopback (Windows 10 2004+). Falls back to system loopback.</summary>
public sealed class ProcessLoopbackCapture : IDisposable
{
    private readonly WasapiLoopbackCapture _fallback;
    private bool _disposed;
    private bool _useFallback = true;

    public ProcessLoopbackCapture(uint processId, string windowTitle)
    {
        ProcessId = processId;
        WindowTitle = windowTitle;
        _fallback = new WasapiLoopbackCapture();
        WaveFormat = _fallback.WaveFormat;

        if (processId > 0 && TryEnableProcessLoopback(processId))
            _useFallback = false;

        _fallback.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
        AppLogger.Info(_useFallback
            ? $"Strip '{windowTitle}': system loopback (PID {processId})"
            : $"Strip '{windowTitle}': per-process loopback PID {processId}");
    }

    public uint ProcessId { get; }
    public string WindowTitle { get; }
    public WaveFormat WaveFormat { get; }
    public bool IsProcessIsolated => !_useFallback;

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public void StartRecording() => _fallback.StartRecording();
    public void StopRecording() => _fallback.StopRecording();

    private static bool TryEnableProcessLoopback(uint processId)
    {
        try
        {
            var os = Environment.OSVersion.Version;
            if (os.Build < 19041) return false;

            var handler = new ActivateHandler();
            var param = new AudioClientActivationParams
            {
                ActivationType = AudioClientActivationType.ProcessLoopback,
                ProcessLoopbackParams = new AudioClientProcessLoopbackParams
                {
                    TargetProcessId = processId,
                    ProcessLoopbackMode = ProcessLoopbackMode.IncludeTargetProcessTree
                }
            };

            var size = Marshal.SizeOf<AudioClientActivationParams>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(param, ptr, false);
                var iid = AudioActivation.IID_IAudioClient;
                var hr = AudioActivation.ActivateAudioInterfaceAsync(
                    "VAD\\Process_Loopback",
                    ref iid,
                    ptr,
                    handler,
                    out _);
                if (hr != 0) return false;
                handler.WaitHandle.WaitOne(5000);
                return handler.ActivateHr == 0 && handler.ActivatedInterface != null;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Process loopback activation failed", ex);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fallback.Dispose();
    }

    private sealed class ActivateHandler : IActivateAudioInterfaceCompletionHandler
    {
        private readonly ManualResetEventSlim _evt = new(false);

        public int ActivateHr { get; private set; }
        public object? ActivatedInterface { get; private set; }
        public WaitHandle WaitHandle => _evt.WaitHandle;

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            try
            {
                operation.GetActivateResult(out var hr, out var iface);
                ActivateHr = hr;
                ActivatedInterface = iface;
            }
            catch (Exception ex)
            {
                AppLogger.Error("ActivateCompleted failed", ex);
                ActivateHr = -1;
            }
            finally
            {
                _evt.Set();
            }
        }
    }
}
