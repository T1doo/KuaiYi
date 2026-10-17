export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --no-build --logger 'trx;LogFileName=r04-provider-full-observation.trx' --results-directory artifacts/screenshot-lease-await-timeout
