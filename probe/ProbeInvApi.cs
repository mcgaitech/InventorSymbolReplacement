using System;
using System.Reflection;
using System.Linq;

class ProbeInvApi
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(
            @"C:\Program Files\Autodesk\Inventor 2023\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll");

        Console.WriteLine("=== SketchedSymbol all members ===");
        var tSym = asm.GetType("Inventor.SketchedSymbol");
        foreach (var m in tSym.GetMembers().OrderBy(x => x.Name))
            Console.WriteLine($"  {m.MemberType,-12} {m.Name}");

        Console.WriteLine("\n=== DrawingView members containing Symbol|Annot|Sketch ===");
        var tDV = asm.GetType("Inventor.DrawingView");
        foreach (var m in tDV.GetMembers()
            .Where(x => x.Name.IndexOf("Symbol", StringComparison.OrdinalIgnoreCase) >= 0
                     || x.Name.IndexOf("Annot",  StringComparison.OrdinalIgnoreCase) >= 0
                     || x.Name.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => x.Name))
            Console.WriteLine($"  {m.MemberType,-12} {m.Name}");
    }
}
