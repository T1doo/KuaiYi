export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet build src/KuaiYi.ImageWorker/KuaiYi.ImageWorker.csproj -c Release --no-restore -p:EnableWindowsTargeting=true
