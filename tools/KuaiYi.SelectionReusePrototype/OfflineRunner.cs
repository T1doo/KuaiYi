using System.Reflection;
using KuaiYi.Core.Tests;
using Xunit;

// Executes only the pure prototype tests. No VSTest, subprocess, UIA or target setup.
int passed=0,failed=0;
foreach(var method in typeof(SelectionReusePrototypeTests).GetMethods().Where(m=>m.GetCustomAttribute<FactAttribute>()!=null).OrderBy(m=>m.Name))
{
    var rows=method.GetCustomAttributes<InlineDataAttribute>().Select(a=>a.GetData(method).Single()).ToArray();
    if(rows.Length==0)rows=[Array.Empty<object>()];
    foreach(var row in rows)
    {
        try{method.Invoke(new SelectionReusePrototypeTests(),row);passed++;}
        catch(Exception){failed++;Console.WriteLine("FAIL "+method.Name);}
    }
}
Console.WriteLine($"SELECTION_REUSE_OFFLINE passed={passed} failed={failed}; pure protocol/state assertions, Windows COM/process behavior NotRun");
return failed==0?0:1;
