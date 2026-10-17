export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore --no-build -c Release --filter 'FullyQualifiedName~OleReceiverTests' --logger 'trx;LogFileName=r21-ole-same-dll.trx' --results-directory artifacts/app-installation-control
