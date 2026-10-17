export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~QueuedPreparedNotificationAfterClearCannotRestoreMetadataOrSend' --logger 'trx;LogFileName=r05-finally-green.trx' --results-directory artifacts/production-screenshot-translation
