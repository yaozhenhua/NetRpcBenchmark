@rem Generate the C# code for *.bond files

setlocal

cd /d %~dp0

%HOMEPATH%\.nuget\packages\bond.compiler.csharp\7.0.1\tools\gbc.exe c# EchoBond.bond --grpc

endlocal
