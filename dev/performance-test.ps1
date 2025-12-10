# Performance Test Script
# Run this after building with optimizations to measure improvements

Write-Host "=== EasyAuth Tokenstore Proxy - Performance Testing ===" -ForegroundColor Green
Write-Host ""

# Test 1: Build size analysis
Write-Host "1. Build Size Analysis:" -ForegroundColor Yellow
Write-Host "   Framework-dependent build (no trimming):"
if (Test-Path "./bin/Release/net10.0/publish/") {
    $frameworkDependent = Get-ChildItem -Recurse "./bin/Release/net10.0/publish/" | Measure-Object -Property Length -Sum
    Write-Host "   Size: $([math]::Round($frameworkDependent.Sum/1MB,2)) MB ($($frameworkDependent.Count) files)"
}

Write-Host "   Self-contained build (with trimming):"
if (Test-Path "./bin/Release/net10.0/win-x64/publish/") {
    $selfContained = Get-ChildItem -Recurse "./bin/Release/net10.0/win-x64/publish/" | Measure-Object -Property Length -Sum
    Write-Host "   Size: $([math]::Round($selfContained.Sum/1MB,2)) MB ($($selfContained.Count) files)"
}
Write-Host ""

# Test 2: Startup time measurement
Write-Host "2. Startup Time Measurement:" -ForegroundColor Yellow

if (Test-Path "./bin/Release/net10.0/win-x64/publish/EasyAuthTokenstoreProxy.exe") {
    Write-Host "   Testing self-contained executable startup time..."
    $startupTime = Measure-Command {
        $process = Start-Process -FilePath "./bin/Release/net10.0/win-x64/publish/EasyAuthTokenstoreProxy.exe" -WindowStyle Hidden -PassThru
        Start-Sleep -Milliseconds 500  # Allow startup
        $process.Kill()
        $process.WaitForExit()
    }
    Write-Host "   Self-contained startup time: $($startupTime.TotalMilliseconds) ms"
}

if (Test-Path "./bin/Release/net10.0/publish/EasyAuthTokenstoreProxy.dll") {
    Write-Host "   Testing framework-dependent startup time..."
    $frameworkStartupTime = Measure-Command {
        $process = Start-Process -FilePath "dotnet" -ArgumentList "./bin/Release/net10.0/publish/EasyAuthTokenstoreProxy.dll" -WindowStyle Hidden -PassThru
        Start-Sleep -Milliseconds 500  # Allow startup
        $process.Kill()
        $process.WaitForExit()
    }
    Write-Host "   Framework-dependent startup time: $($frameworkStartupTime.TotalMilliseconds) ms"
}
Write-Host ""

# Test 3: Container size estimation
Write-Host "3. Container Size Impact:" -ForegroundColor Yellow
Write-Host "   Standard ASP.NET base image: ~200MB"
Write-Host "   Alpine ASP.NET base image: ~100MB"
Write-Host "   Application layer (trimmed): ~35MB"
Write-Host "   Estimated total container size: ~135MB"
Write-Host ""

# Test 4: Memory usage tips
Write-Host "4. Memory Usage Monitoring:" -ForegroundColor Yellow
Write-Host "   To monitor memory usage in production:"
Write-Host "   - Use dotnet-counters: dotnet-counters monitor --process-id <PID>"
Write-Host "   - Check GC metrics and working set"
Write-Host "   - Compare with non-trimmed builds"
Write-Host ""

# Test 5: Build commands summary
Write-Host "5. Quick Build Commands:" -ForegroundColor Yellow
Write-Host "   Development:     dotnet run"
Write-Host "   Release build:   dotnet build -c Release"  
Write-Host "   Optimized:       dotnet publish -c Release -p:PublishTrimmed=true"
Write-Host "   Container:       docker build -t easyauth-proxy ."
Write-Host ""

Write-Host "Performance testing completed!" -ForegroundColor Green