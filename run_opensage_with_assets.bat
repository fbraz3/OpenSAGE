@echo off
set "CNC_GENERALS_PATH=C:\Program Files (x86)\Steam\steamapps\common\Command and Conquer Generals"
pushd "c:\Users\fbraz\Projects\OpenSAGE\src"
dotnet run --project OpenSage.Launcher\OpenSage.Launcher.csproj
popd
