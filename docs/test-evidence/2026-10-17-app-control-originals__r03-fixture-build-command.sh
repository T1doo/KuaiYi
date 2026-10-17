export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet build tools/KuaiYi.SettingsStoreFixture/KuaiYi.SettingsStoreFixture.csproj --no-restore -c Release
