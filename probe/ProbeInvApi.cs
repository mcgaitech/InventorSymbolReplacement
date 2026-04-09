using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;

class ProbeInvApi
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== So sánh Native vs API symbol properties ===\n");

        dynamic app;
        try { app = Marshal.GetActiveObject("Inventor.Application"); }
        catch { Console.WriteLine("Không kết nối Inventor."); return; }

        dynamic doc = app.ActiveDocument;
        dynamic sheet = doc.ActiveSheet;

        // ── Dump TẤT CẢ properties của mỗi symbol ───────────────────────────
        int idx = 0;
        foreach (dynamic sym in sheet.SketchedSymbols)
        {
            idx++;
            if (idx > 5) break;  // chỉ dump 5 symbol đầu

            Console.WriteLine($"--- Symbol #{idx}: '{sym.Name}' (def='{sym.Definition.Name}') ---");

            // Properties cơ bản
            Try(() => Console.WriteLine($"  Position     : ({(double)sym.Position.X:F4},{(double)sym.Position.Y:F4})"));
            Try(() => Console.WriteLine($"  Rotation     : {(double)sym.Rotation:F4}"));
            Try(() => Console.WriteLine($"  Scale        : {(double)sym.Scale:F4}"));
            Try(() => Console.WriteLine($"  Static       : {(bool)sym.Static}"));

            // Leader properties — ĐÂY LÀ KEY
            Try(() => Console.WriteLine($"  Callout      : {(bool)sym.Callout}"));
            Try(() => Console.WriteLine($"  LeaderVisible: {(bool)sym.LeaderVisible}"));
            Try(() => Console.WriteLine($"  LeaderClipping: {(bool)sym.LeaderClipping}"));
            Try(() => Console.WriteLine($"  SymbolClipping: {(bool)sym.SymbolClipping}"));
            Try(() => Console.WriteLine($"  LeaderStyle  : {sym.LeaderStyle}"));

            // _AttachedEntity
            Try(() => {
                var ae = sym._AttachedEntity;
                Console.WriteLine($"  _AttachedEntity: {(ae != null ? "CÓ" : "null")}");
            });

            // Leader structure
            Try(() => {
                bool hasRoot = sym.Leader.HasRootNode;
                Console.WriteLine($"  Leader.HasRootNode: {hasRoot}");
                if (hasRoot)
                {
                    Try(() => Console.WriteLine($"  Leader.ArrowheadType: {sym.Leader.ArrowheadType}"));
                    int leafIdx = 0;
                    foreach (dynamic leaf in sym.Leader.AllLeafNodes)
                    {
                        double lx = leaf.Position.X, ly = leaf.Position.Y;
                        bool hasAtt = false;
                        Try(() => { hasAtt = leaf.AttachedEntity != null; });
                        Console.WriteLine($"  Leaf[{leafIdx}]: ({lx:F4},{ly:F4}) att={hasAtt}");
                        leafIdx++;
                    }
                }
            });

            // LineType, LineWeight, Color
            Try(() => Console.WriteLine($"  LineType     : {sym.LineType}"));
            Try(() => Console.WriteLine($"  LineWeight   : {(double)sym.LineWeight:F4}"));

            Console.WriteLine();
        }

        // ── Tạo API symbol để so sánh ────────────────────────────────────────
        Console.WriteLine("=== Tạo API symbol để so sánh ===\n");

        // Tìm definition + geometry
        dynamic testDef = null;
        foreach (dynamic def in doc.SketchedSymbolDefinitions) { testDef = def; break; }
        if (testDef == null) { Console.WriteLine("Không có definition."); return; }

        dynamic drawCurve = null;
        foreach (dynamic view in sheet.DrawingViews)
        {
            foreach (dynamic c in view.DrawingCurves) { drawCurve = c; break; }
            break;
        }

        object prompts = BuildPromptStrings(testDef);

        dynamic tx = app.TransactionManager.StartTransaction(doc, "ProbeCompare");
        try
        {
            dynamic pos = app.TransientGeometry.CreatePoint2d(5.0, 5.0);
            dynamic newSym = sheet.SketchedSymbols.Add(testDef, pos, 0.0, 1.0, prompts);
            Console.WriteLine($"API Symbol: '{newSym.Name}'");

            // Dump TRƯỚC AddLeader
            Console.WriteLine("\n  TRƯỚC AddLeader:");
            Try(() => Console.WriteLine($"    Callout      : {(bool)newSym.Callout}"));
            Try(() => Console.WriteLine($"    LeaderVisible: {(bool)newSym.LeaderVisible}"));
            Try(() => Console.WriteLine($"    HasRootNode  : {(bool)newSym.Leader.HasRootNode}"));

            // AddLeader
            if (drawCurve != null)
            {
                dynamic gi = sheet.CreateGeometryIntent(drawCurve, pos);
                var pts = app.TransientObjects.CreateObjectCollection();
                pts.Add(pos);
                pts.Add(gi);
                newSym.Leader.AddLeader(pts);

                Console.WriteLine("\n  SAU AddLeader:");
                Try(() => Console.WriteLine($"    Callout      : {(bool)newSym.Callout}"));
                Try(() => Console.WriteLine($"    LeaderVisible: {(bool)newSym.LeaderVisible}"));
                Try(() => Console.WriteLine($"    HasRootNode  : {(bool)newSym.Leader.HasRootNode}"));
                Try(() => Console.WriteLine($"    ArrowheadType: {newSym.Leader.ArrowheadType}"));

                // Thử set LeaderVisible = true
                Try(() => { newSym.LeaderVisible = true; Console.WriteLine($"    Set LeaderVisible=true → OK"); });
                Try(() => Console.WriteLine($"    LeaderVisible sau set: {(bool)newSym.LeaderVisible}"));

                // Thử set Callout (read-only?)
                Try(() => { newSym.Callout = true; Console.WriteLine($"    Set Callout=true → OK"); });
            }
        }
        catch (Exception ex) { Console.WriteLine($"LỖI: {ex.Message}"); }
        finally { Try(() => tx.Abort()); Console.WriteLine("\n✓ Transaction ABORTED."); }
    }

    static void Try(Action a) { try { a(); } catch (Exception ex) { Console.WriteLine($"    ERR: {ex.Message}"); } }

    static object BuildPromptStrings(dynamic def)
    {
        try
        {
            var values = new List<string>();
            foreach (dynamic tb in def.Sketch.TextBoxes)
            {
                try
                {
                    string fmt = ""; try { fmt = tb.FormattedText ?? ""; } catch { }
                    if (fmt.IndexOf("ReadOnlyUniqueID", StringComparison.OrdinalIgnoreCase) >= 0)
                    { string v = ""; try { v = tb.Text ?? ""; } catch { } values.Add(v); }
                }
                catch { }
            }
            return values.Count == 0 ? (object)Type.Missing : values.ToArray();
        }
        catch { return Type.Missing; }
    }
}
