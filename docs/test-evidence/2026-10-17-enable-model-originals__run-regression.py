from pathlib import Path
import hashlib,json,subprocess,datetime
p=Path('artifacts/enable-model-disclosure');source=json.loads((p/'final-source-binding.json').read_text())
def hashes(paths):return {f:hashlib.sha256(Path(f).read_bytes()).hexdigest() for f in paths if Path(f).is_file()}
jobs=[('r06-core-full','dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=r06-core-full.trx" --results-directory artifacts/enable-model-disclosure','tests/KuaiYi.Core.Tests/bin/Release/net10.0'),('r07-provider-full','dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=r07-provider-full.trx" --results-directory artifacts/enable-model-disclosure','tests/KuaiYi.Provider.Tests/bin/Release/net10.0'),('r08-app-build','dotnet build src/KuaiYi.App/KuaiYi.App.csproj -c Release --no-restore -p:EnableWindowsTargeting=true','src/KuaiYi.App/bin/Release/net10.0-windows/win-x64'),('r09-worker-build','dotnet build src/KuaiYi.ImageWorker/KuaiYi.ImageWorker.csproj -c Release --no-restore -p:EnableWindowsTargeting=true','src/KuaiYi.ImageWorker/bin/Release/net10.0/win-x64')]
for name,command,binary in jobs:
 assert hashes(source)==source
 script='export DOTNET_GENERATE_ASPNET_CERTIFICATE=false\nsource /workspace/.kuaiyi-setup/activate.sh\nexport DOTNET_GENERATE_ASPNET_CERTIFICATE=false\n'+command+'\n';(p/(name+'-command.sh')).write_text(script)
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (p/(name+'.log')).open('wb') as log:r=subprocess.run(['bash',str(p/(name+'-command.sh'))],stdout=log,stderr=subprocess.STDOUT)
 (p/(name+'-execution.json')).write_text(json.dumps({'startUtc':start,'endUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exitCode':r.returncode,'source':source,'binaries':hashes(str(f) for f in Path(binary).glob('KuaiYi.*.dll'))},indent=2)+'\n');print(name+': '+str(r.returncode),flush=True)
 if r.returncode:break
