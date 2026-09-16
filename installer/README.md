# Cuebox installer

The public install path is a Command Prompt one-liner that writes the zip bytes to disk. See the root README.

To build a local copy:

```
dotnet publish ..\src\UI\Mixline.App.csproj -c Release -r win-x64 --self-contained true
```

Optional: compile `Cuebox.iss` with Inno Setup 6.

Uninstall of the Inno package removes the app folder and `%AppData%\Cuebox`.
The optional HKCU Run value is removed when the user turns off Start with Windows.
