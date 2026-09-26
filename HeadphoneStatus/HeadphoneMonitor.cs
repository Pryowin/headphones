using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HeadphoneStatus;

public enum HeadphoneState
{
    NotConnected,
    Off,
    On,
    Unknown
}

/// <summary>
/// Talks to the Logitech G535 LIGHTSPEED receiver over its raw HID++ 2.0 vendor
/// channel (VID 046D / PID 0AC4, usage page 0xFF43, usage 0x0202). Independent of
/// G HUB: it just opens the HID device and exchanges reports directly, the same way
/// G HUB itself would, so it works whether G HUB is installed/running or not.
///
/// The receiver answers a request to feature index 0x05 / function 0 differently
/// depending on whether the paired headset is reachable over its 2.4GHz link:
///   - headset on:  11 FF 05 0B 10 xx 01 00 ... (feature 0x05 responds normally)
///   - headset off: 11 FF FF 05 0B 02 00 00 ... (HID++ error frame: feature/function
///                  echoed back with error code 0x02, because the receiver couldn't
///                  reach the headset)
/// This was verified empirically against the actual hardware by toggling the
/// headset's power switch and observing the byte patterns above.
/// </summary>
public sealed class HeadphoneMonitor
{
    private const ushort VendorId = 0x046D;
    private const ushort ProductId = 0x0AC4;
    private const ushort ExpectedUsagePage = 0xFF43;
    private const ushort ExpectedUsage = 0x0202;
    private const int ReportLength = 20;
    private const int TimeoutMs = 800;

    public HeadphoneState Poll()
    {
        var paths = FindCandidatePaths();
        if (paths.Count == 0) return HeadphoneState.NotConnected;

        bool sawOff = false;
        bool sawAnyResponse = false;

        foreach (var path in paths)
        {
            var reply = QueryOne(path);
            if (reply is null) continue; // this path never answered; likely a stale duplicate

            sawAnyResponse = true;
            if (IsOnResponse(reply)) return HeadphoneState.On;
            if (IsOffErrorResponse(reply)) sawOff = true;
        }

        if (sawOff) return HeadphoneState.Off;
        return sawAnyResponse ? HeadphoneState.Unknown : HeadphoneState.NotConnected;
    }

    private static bool IsOnResponse(byte[] r) =>
        r.Length >= 4 && r[1] == 0xFF && r[2] == 0x05 && r[3] == 0x0B;

    private static bool IsOffErrorResponse(byte[] r) =>
        r.Length >= 6 && r[1] == 0xFF && r[2] == 0xFF && r[3] == 0x05 && r[4] == 0x0B;

    private static byte[]? QueryOne(string path)
    {
        using var handle = HidNative.CreateFile(
            path,
            HidNative.GENERIC_READ | HidNative.GENERIC_WRITE,
            HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
            IntPtr.Zero, HidNative.OPEN_EXISTING, HidNative.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (handle.IsInvalid) return null;

        var request = new byte[ReportLength];
        request[0] = 0x11; // HID++ 2.0 long report
        request[1] = 0xFF; // device index (this receiver has a single paired device)
        request[2] = 0x05; // feature index (empirically: wireless/battery status)
        request[3] = 0x0B; // function 0, arbitrary software id

        if (!OverlappedWrite(handle, request, TimeoutMs)) return null;

        var response = new byte[ReportLength];
        return OverlappedRead(handle, response, TimeoutMs) ? response : null;
    }

    private static bool OverlappedWrite(SafeFileHandle handle, byte[] buffer, int timeoutMs)
    {
        var overlapped = new HidNative.OVERLAPPED { hEvent = HidNative.CreateEvent(IntPtr.Zero, true, false, null) };
        try
        {
            bool ok = HidNative.WriteFile(handle, buffer, (uint)buffer.Length, out _, ref overlapped);
            if (ok) return true;
            if (Marshal.GetLastWin32Error() != HidNative.ERROR_IO_PENDING) return false;
            return WaitForOverlapped(handle, ref overlapped, timeoutMs);
        }
        finally
        {
            if (overlapped.hEvent != IntPtr.Zero) HidNative.CloseHandle(overlapped.hEvent);
        }
    }

    private static bool OverlappedRead(SafeFileHandle handle, byte[] buffer, int timeoutMs)
    {
        var overlapped = new HidNative.OVERLAPPED { hEvent = HidNative.CreateEvent(IntPtr.Zero, true, false, null) };
        try
        {
            bool ok = HidNative.ReadFile(handle, buffer, (uint)buffer.Length, out _, ref overlapped);
            if (ok) return true;
            if (Marshal.GetLastWin32Error() != HidNative.ERROR_IO_PENDING) return false;
            return WaitForOverlapped(handle, ref overlapped, timeoutMs);
        }
        finally
        {
            if (overlapped.hEvent != IntPtr.Zero) HidNative.CloseHandle(overlapped.hEvent);
        }
    }

    private static bool WaitForOverlapped(SafeFileHandle handle, ref HidNative.OVERLAPPED overlapped, int timeoutMs)
    {
        uint wait = HidNative.WaitForSingleObject(overlapped.hEvent, (uint)timeoutMs);
        if (wait == HidNative.WAIT_OBJECT_0)
        {
            return HidNative.GetOverlappedResult(handle, ref overlapped, out _, false);
        }

        HidNative.CancelIoEx(handle, ref overlapped);
        return false;
    }

    private static List<string> FindCandidatePaths()
    {
        var result = new List<string>();
        foreach (var path in HidNative.EnumerateDevicePaths())
        {
            using var handle = HidNative.CreateFile(path, HidNative.GENERIC_READ,
                HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE, IntPtr.Zero, HidNative.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid) continue;

            if (!HidNative.HidD_GetAttributes(handle, out var attrs)) continue;
            if (attrs.VendorID != VendorId || attrs.ProductID != ProductId) continue;

            if (!HidNative.HidD_GetPreparsedData(handle, out var preparsed)) continue;
            try
            {
                if (HidNative.HidP_GetCaps(preparsed, out var caps) != HidNative.HIDP_STATUS_SUCCESS) continue;
                if (caps.UsagePage != ExpectedUsagePage || caps.Usage != ExpectedUsage) continue;
                if (caps.OutputReportByteLength != ReportLength) continue;
            }
            finally
            {
                HidNative.HidD_FreePreparsedData(preparsed);
            }

            result.Add(path);
        }
        return result;
    }
}
