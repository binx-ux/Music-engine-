using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Mixline.Audio.Devices;

internal static class WindowsDefaultEndpoint
{
    public static bool SetDefaultCapture(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"));
            if (type is null)
                return false;
            var client = Activator.CreateInstance(type);
            if (client is null)
                return false;
            try
            {
                if (client is IPolicyConfig cfg)
                    return SetRoles(cfg.SetDefaultEndpoint, deviceId);
                if (client is IPolicyConfigVista vista)
                    return SetRoles(vista.SetDefaultEndpoint, deviceId);
            }
            finally
            {
                Marshal.ReleaseComObject(client);
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool SetRoles(Func<string, Role, int> set, string deviceId)
    {
        var a = set(deviceId, Role.Console);
        var b = set(deviceId, Role.Multimedia);
        var c = set(deviceId, Role.Communications);
        return a == 0 || b == 0 || c == 0;
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint ppFormat);
        int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, nint ppFormat);
        int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pEndpointFormat, nint pMixFormat);
        int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, nint pmftDefaultPeriod, nint pmftMinimumPeriod);
        int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pmftPeriod);
        int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pMode);
        int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint mode);
        int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, nint key, nint pv);
        int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, nint key, nint pv);
        [PreserveSig]
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, Role eRole);
        int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }

    [ComImport]
    [Guid("568B9108-44BF-40B4-9006-86AFE5B5A620")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfigVista
    {
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint ppFormat);
        int Unused1();
        int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pEndpointFormat, nint pMixFormat);
        int Unused2();
        int Unused3();
        int Unused4();
        int Unused5();
        int Unused6();
        int Unused7();
        int Unused8();
        [PreserveSig]
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, Role eRole);
        int Unused9();
    }
}
