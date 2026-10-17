export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~QueuedPreparedNotificationAfterClearCannotRestoreMetadataOrSend' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=r04-finally-red.trx' --results-directory artifacts/production-screenshot-translation --blame-hang-timeout 30s --blame-hang-dump-type none
