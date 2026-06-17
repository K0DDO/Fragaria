using NAudio.CoreAudioApi;

namespace Fragaria.Services;

public sealed record AudioDeviceInfo(string Id, string Name, DataFlow Flow);

public static class AudioDeviceService
{
    public static IReadOnlyList<AudioDeviceInfo> ListDevices(DataFlow flow)
    {
        var list = new List<AudioDeviceInfo>();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var d in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                list.Add(new AudioDeviceInfo(d.ID, d.FriendlyName, flow));
        }
        catch (Exception ex)
        {
            AppLogger.Error("ListDevices failed", ex);
        }
        return list;
    }

    public static string? DefaultDeviceId(DataFlow flow)
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var role = flow == DataFlow.Capture ? Role.Communications : Role.Multimedia;
            return enumerator.GetDefaultAudioEndpoint(flow, role).ID;
        }
        catch { return null; }
    }
}
