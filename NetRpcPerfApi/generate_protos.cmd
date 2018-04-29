@rem Generate the C# code for *.proto files

setlocal

cd /d %~dp0

set TOOLS_PATH=%HOMEPATH%\.nuget\packages\Grpc.Tools\1.11.0\tools\windows_x64

%TOOLS_PATH%\protoc.exe -I. --csharp_out . ./EchoProto.proto --grpc_out . --plugin=protoc-gen-grpc=%TOOLS_PATH%\grpc_csharp_plugin.exe

endlocal
