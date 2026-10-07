param(
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$engine = Split-Path $PSScriptRoot -Parent
$sdkSources = Join-Path $engine 'UnityProject/Assets/GameScripts/GameSDK'
$testOutput = Join-Path (Split-Path $engine -Parent) 'output/sdk-validation'
New-Item -ItemType Directory -Force $testOutput | Out-Null
$testExe = Join-Path $testOutput 'SDKTests.exe'
$arguments = @('/nologo', '/target:exe', "/out:$testExe")
foreach ($name in @('mscorlib', 'System', 'System.Core')) {
    $arguments += "/r:$UnityData/MonoBleedingEdge/lib/mono/4.5/$name.dll"
}
$arguments += Join-Path $PSScriptRoot 'StartupTelemetryTests.cs'
foreach ($name in @('SdkSession', 'StartupTelemetry', 'ISdk', 'IRewardedVideoAd')) {
    $arguments += Join-Path $sdkSources "$name.cs"
}
& "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" @arguments
if ($LASTEXITCODE -ne 0) { throw 'SDK test compilation failed.' }
& "$UnityData/MonoBleedingEdge/bin/mono.exe" $testExe
if ($LASTEXITCODE -ne 0) { throw 'SDK tests failed.' }
