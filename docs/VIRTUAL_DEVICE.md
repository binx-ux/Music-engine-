# Virtual microphone

Windows has no supported user-mode API that creates a new capture endpoint. A device that shows up in Roblox or Discord as a microphone is a Kernel Streaming audio endpoint. That means a signed driver.

## What Cuebox does today

Cuebox renders the mixed bus to a WASAPI **playback** device that belongs to a virtual cable. The matching **capture** device is what other apps select.

Common pairing:

| Cuebox Virtual Output | App microphone setting |
| --- | --- |
| CABLE Input (VB-Audio) | CABLE Output |
| VoiceMeeter Input | VoiceMeeter Output |

Cuebox detects names such as VB-Audio, CABLE, VoiceMeeter, and VAIO. If none are installed, the Devices page explains that a virtual cable is required. The rest of the app still works for local monitoring.

This is the same class of routing used by legitimate voice tools. It does not hook Roblox, Discord, or any game.

## Shipping Cuebox's own device

Microsoft's SysVAD sample is the reference for a virtual render/capture driver:

https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad

To ship a "Cuebox Virtual Microphone" endpoint you need:

1. A WDK driver package (INF + SYS, componentized since Windows 10 1809)
2. An EV code-signing certificate
3. Attestation signing through the Hardware Partner Center
https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-attestation
4. An installer that uses `pnputil` / `devcon` and removes the package on uninstall

Unsigned kernel drivers will not load on normal Windows 11 systems. Test signing is only for development machines.

The installer in `installer/` copies the app and documents this driver gap. It does not install an unsigned `.sys`.

## Registry

Cuebox writes:

| Key | Why |
| --- | --- |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Cuebox` | Optional start with Windows. Removed when the setting is off or the app is uninstalled. |

No other registry values are written. A future signed driver would add INF-defined device keys only through the driver installer, and those keys would be removed on driver uninstall.
