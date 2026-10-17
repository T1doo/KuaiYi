export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
python3 - <<'PY'
import pathlib,json,datetime
pathlib.Path('artifacts/diagnostic-dispose-settlement/r07-provider-full-start.json').write_text(json.dumps({'startUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2)+'\n')
PY
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=r07-provider-full.trx' --results-directory artifacts/diagnostic-dispose-settlement > artifacts/diagnostic-dispose-settlement/r07-provider-full.log 2>&1
run_exit=$?
python3 - "$run_exit" <<'PY'
import pathlib,json,datetime,hashlib,sys
files=['tests/KuaiYi.Provider.Tests/bin/Release/net10.0/KuaiYi.Provider.Tests.dll', 'tests/KuaiYi.Provider.Tests/bin/Release/net10.0/KuaiYi.Providers.dll', 'tests/KuaiYi.Provider.Tests/bin/Release/net10.0/KuaiYi.Core.dll']
pathlib.Path('artifacts/diagnostic-dispose-settlement/r07-provider-full-executed-binding.json').write_text(json.dumps({'endUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exit':int(sys.argv[1]),'sha256':{f:hashlib.sha256(pathlib.Path(f).read_bytes()).hexdigest() for f in files if pathlib.Path(f).is_file()}},indent=2)+'\n')
PY
exit "$run_exit"
