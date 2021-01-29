# Building:
- dotnet publish --runtime win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=None -p:DebugSymbols=false --self-contained true --configuration Release --output .