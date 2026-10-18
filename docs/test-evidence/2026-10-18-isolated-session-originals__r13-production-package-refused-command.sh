export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll --prepare-isolated-session-package /workspace/KuaiYi/artifacts/isolated-session-data/app-production /workspace/KuaiYi/artifacts/isolated-session-data/r13-production-package-refused-no-output
