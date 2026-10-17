export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --no-build --filter 'FullyQualifiedName~IdleDeadlineKeepsPartialTextAndIgnoresNoContentActivity' --logger 'trx;LogFileName=r03-old-idle-observation.trx' --results-directory artifacts/screenshot-lease-await-timeout
