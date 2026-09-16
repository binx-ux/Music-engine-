@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "$z=$env:TEMP+'\cuebox.zip'; [IO.File]::WriteAllBytes($z,(iwr -useb 'https://github.com/binx-ux/Music-engine-/releases/latest/download/Cuebox.zip').Content); $d=$env:LOCALAPPDATA+'\Cuebox'; if(Test-Path $d){ri $d -Recurse -Force}; Expand-Archive $z $d -Force; start (Join-Path $d 'Cuebox.exe')"
