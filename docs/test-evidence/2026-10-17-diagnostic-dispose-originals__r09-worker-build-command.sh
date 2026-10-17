export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
python3 - <<'PY'
import pathlib,json,datetime
pathlib.Path('artifacts/diagnostic-dispose-settlement/r09-worker-build-start.json').write_text(json.dumps({'startUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2)+'\n')
PY
dotnet build src/KuaiYi.ImageWorker/KuaiYi.ImageWorker.csproj -c Release --no-restore -p:EnableWindowsTargeting=true > artifacts/diagnostic-dispose-settlement/r09-worker-build.log 2>&1
run_exit=$?
python3 - "$run_exit" <<'PY'
import pathlib,json,datetime,hashlib,sys
files=['src/KuaiYi.ImageWorker/bin/Release/net10.0/win-x64/KuaiYi.ImageWorker.dll', 'src/KuaiYi.ImageWorker/bin/Release/net10.0/win-x64/KuaiYi.Core.dll']
pathlib.Path('artifacts/diagnostic-dispose-settlement/r09-worker-build-executed-binding.json').write_text(json.dumps({'endUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exit':int(sys.argv[1]),'sha256':{f:hashlib.sha256(pathlib.Path(f).read_bytes()).hexdigest() for f in files if pathlib.Path(f).is_file()}},indent=2)+'\n')
PY
exit "$run_exit"
