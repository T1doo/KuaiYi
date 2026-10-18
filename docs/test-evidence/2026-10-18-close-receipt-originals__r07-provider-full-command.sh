export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=r07-provider-full.trx' --results-directory artifacts/session-resume-review
