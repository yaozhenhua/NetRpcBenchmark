@rem Generate code from *.protos

setlocal

cd /d %~dp0

@rem path %path%;%USERPROFILE%\go\bin

protoc -I ../NetRpcPerfApi --go_out=plugins=grpc:./EchoServer ../NetRpcPerfApi/EchoProto.proto

endlocal
