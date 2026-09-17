# Cuebox installer

Pick the app folder and a separate data folder.

## Setup.exe

Build with Inno Setup 6:

```
powershell -File installer\build.ps1
```

That writes `installer\output\Cuebox.zip` and, if `iscc` is installed, `CueboxSetup.exe`.

## PowerShell

`install.ps1` asks for both folders, downloads the latest zip, writes `data.path`, and makes a desktop shortcut.

## Zip

Unpack `Cuebox.zip` anywhere. Optional: put a `data.path` file next to `Cuebox.exe` with one line, the data folder. Or create a `Cuebox.data` folder next to the exe for a portable layout.

Uninstall of the Inno package removes the app folder. Settings stay in the data folder unless you delete that yourself.
