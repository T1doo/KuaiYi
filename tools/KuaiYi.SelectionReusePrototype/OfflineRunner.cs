using System.Reflection;
using KuaiYi.Core.Tests;
using Xunit;
using KuaiYi.SelectionReuseDiagnostic;

// Executes only the pure prototype tests. No VSTest, subprocess, UIA or target setup.
bool host=args is ["--host-tests"];if(args.Length!=0&&!host)return 2;
int passed=0,failed=0;
var type=host?typeof(SelectionReuseHostTests):typeof(SelectionReusePrototypeTests);
foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttribute<FactAttribute>()!=null).OrderBy(m=>m.Name))
{
    var rows=method.GetCustomAttributes<InlineDataAttribute>().Select(a=>a.GetData(method).Single()).ToArray();
    if(rows.Length==0)rows=[Array.Empty<object>()];
    foreach(var row in rows)
    {
        var result=await DiagnosticCaseRunner.ExecuteAsync(()=>{var value=method.Invoke(Activator.CreateInstance(type),row);return value as Task??Task.CompletedTask;},TimeSpan.FromSeconds(12));
        if(result.Passed)passed++;else{failed++;Console.WriteLine("FAIL "+method.Name);}
    }
}
Console.WriteLine(host?$"SELECTION_REUSE_HOST passed={passed} failed={failed}; real synthetic owned processes, UIA/COM NotRun":$"SELECTION_REUSE_OFFLINE passed={passed} failed={failed}; pure protocol/state assertions, Windows COM/process behavior NotRun");
return failed==0?0:1;
