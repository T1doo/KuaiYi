export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~HeldCaptureFinally|FullyQualifiedName~AChangedDisplayContextBeforeTheYieldContinuationNeverStartsNativeCapture' --logger 'trx;LogFileName=r02-held-context.trx' --results-directory artifacts/held-context-test-cleanup
