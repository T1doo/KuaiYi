export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet build src/KuaiYi.ImageWorker/KuaiYi.ImageWorker.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
