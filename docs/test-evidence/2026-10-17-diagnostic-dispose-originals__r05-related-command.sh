export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
python3 - <<'PY'
import pathlib,json,datetime
pathlib.Path('artifacts/diagnostic-dispose-settlement/r05-related-start.json').write_text(json.dumps({'startUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2)+'\n')
PY
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~DiagnosticDisposeSettlementTests|FullyQualifiedName~SelectionReuseHostTests|FullyQualifiedName~HostTerminationTests' --logger 'trx;LogFileName=r05-related.trx' --results-directory artifacts/diagnostic-dispose-settlement > artifacts/diagnostic-dispose-settlement/r05-related.log 2>&1
run_exit=$?
python3 - "$run_exit" <<'PY'
import pathlib,json,datetime,hashlib,sys
files=['tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.Tests.dll', 'tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.SelectionReuseDiagnostic.dll', 'tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.dll', 'tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.NativeHost.dll', 'tools/KuaiYi.SelectionReuseDiagnostic/bin/Release/net10.0/KuaiYi.SelectionReuseDiagnostic.dll']
pathlib.Path('artifacts/diagnostic-dispose-settlement/r05-related-executed-binding.json').write_text(json.dumps({'endUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exit':int(sys.argv[1]),'sha256':{f:hashlib.sha256(pathlib.Path(f).read_bytes()).hexdigest() for f in files if pathlib.Path(f).is_file()}},indent=2)+'\n')
PY
exit "$run_exit"
