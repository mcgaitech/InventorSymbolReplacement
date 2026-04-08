using System;
using System.Reflection;
using System.Linq;

class ProbeInvApi
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(
            @"C:\Program Files\Autodesk\Inventor 2023\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll");

        Console.WriteLine("=== SketchedSymbols.Add — all overloads, full parameter detail ===");
        var tColl = asm.GetType("Inventor.SketchedSymbols");
        foreach (var m in tColl.GetMethods().Where(x => x.Name == "Add" || x.Name == "AddWithOptions"))
        {
            Console.WriteLine($"\n  {m.Name}:");
            foreach (var p in m.GetParameters())
                Console.WriteLine($"    [{p.Position}] {p.ParameterType.FullName ?? p.ParameterType.Name}  {p.Name}" +
                                  $"  optional={p.IsOptional}  default={p.DefaultValue}");
            Console.WriteLine($"    → returns {m.ReturnType.Name}");
        }

        Console.WriteLine("\n=== NameValueMap.Add — parameter detail ===");
        var tNVM = asm.GetType("Inventor.NameValueMap");
        foreach (var m in tNVM.GetMethods().Where(x => x.Name == "Add" || x.Name == "Insert"))
        {
            Console.WriteLine($"\n  {m.Name}:");
            foreach (var p in m.GetParameters())
                Console.WriteLine($"    [{p.Position}] {p.ParameterType.FullName ?? p.ParameterType.Name}  {p.Name}");
        }
    }
}
