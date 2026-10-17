export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet build src/KuaiYi.App/KuaiYi.App.csproj -c Release --no-restore -p:EnableWindowsTargeting=true > artifacts/integration-status-page/r03-app-build.log 2>&1
