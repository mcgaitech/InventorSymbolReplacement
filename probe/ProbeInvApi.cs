using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;

class ProbeInvApi
{
    static void Main(string[] args)
    {
        bool liveMode = args.Length > 0 && args[0].ToLower() == "live";
        if (!liveMode) { RunTypeMetadata(); return; }

        Console.WriteLine("=== LIVE MODE ===\n");

        dynamic app;
        try { app = Marshal.GetActiveObject("Inventor.Application"); }
        catch (Exception ex) { Console.WriteLine($"Không kết nối Inventor: {ex.Message}"); return; }
        Console.WriteLine($"✓ {app.Caption}");

        dynamic doc;
        try { doc = app.ActiveDocument; }
        catch { Console.WriteLine("Không lấy được ActiveDocument."); return; }
        if ((int)doc.DocumentType != 12292) { Console.WriteLine("Không phải DrawingDocument."); return; }

        dynamic sheet = doc.ActiveSheet;
        Console.WriteLine($"✓ Sheet: '{sheet.Name}'\n");

        // ── Tìm symbol nguồn có leader ────────────────────────────────────────
        dynamic srcSym = null; dynamic srcLeaf = null;
        foreach (dynamic s in sheet.SketchedSymbols)
        {
            try
            {
                if (!(bool)s.Leader.HasRootNode) continue;
                foreach (dynamic leaf in s.Leader.AllLeafNodes)
                {
                    dynamic gi = null;
                    try { gi = leaf.AttachedEntity; } catch { }
                    if (gi != null) { srcSym = s; srcLeaf = leaf; break; }
                }
            }
            catch { }
            if (srcSym != null) break;
        }
        if (srcSym == null) { Console.WriteLine("Không tìm được symbol nguồn."); return; }

        Console.WriteLine($"Nguồn: '{srcSym.Name}'  pos=({(double)srcSym.Position.X:F4},{(double)srcSym.Position.Y:F4})");
        Console.WriteLine($"  Leaf: ({(double)srcLeaf.Position.X:F4},{(double)srcLeaf.Position.Y:F4})");

        dynamic srcGeo = null; dynamic srcIntent = null;
        try { srcGeo    = srcLeaf.AttachedEntity.Geometry; }  catch { }
        try { srcIntent = srcLeaf.AttachedEntity.Intent; }    catch { }

        object promptStrings = BuildPromptStrings(srcSym.Definition);

        // ── 4 tests trong cùng 1 transaction (abort ở cuối) ──────────────────
        dynamic tx = null;
        try { tx = app.TransactionManager.StartTransaction(doc, "ProbeLeaderTest"); Console.WriteLine("✓ Transaction started\n"); }
        catch (Exception ex) { Console.WriteLine($"✗ Transaction: {ex.Message}"); }

        try
        {
            // Tạo freshIntent BÊN TRONG transaction
            dynamic freshIntent = null;
            try
            {
                freshIntent = sheet.CreateGeometryIntent(srcGeo, srcIntent);
                Console.WriteLine($"✓ CreateGeometryIntent() BÊN TRONG transaction → THÀNH CÔNG");
            }
            catch (Exception ex) { Console.WriteLine($"✗ CreateGeometryIntent() inside tx: {ex.Message}"); }

            // Add symbol mới
            double testX = (double)srcSym.Position.X + 5.0;
            double testY = (double)srcSym.Position.Y;
            dynamic newSym = sheet.SketchedSymbols.Add(
                srcSym.Definition,
                app.TransientGeometry.CreatePoint2d(testX, testY),
                (double)srcSym.Rotation, (double)srcSym.Scale,
                promptStrings);
            Console.WriteLine($"✓ Add() THÀNH CÔNG: '{newSym.Name}'");

            bool hasRoot = false;
            try { hasRoot = (bool)newSym.Leader.HasRootNode; } catch { }
            Console.WriteLine($"  Leader.HasRootNode = {hasRoot}\n");

            // ── Test 1: _AttachedEntity setter ────────────────────────────────
            Console.WriteLine("=== Test 1: newSym._AttachedEntity = freshIntent ===");
            if (freshIntent != null)
            {
                try
                {
                    newSym._AttachedEntity = freshIntent;
                    bool hasAtt = false;
                    try { hasAtt = !IsNull(newSym._AttachedEntity); } catch { }
                    bool nowHasRoot = false;
                    try { nowHasRoot = (bool)newSym.Leader.HasRootNode; } catch { }
                    Console.WriteLine($"  ✓ _AttachedEntity SET → hasAtt={hasAtt}  HasRootNode={nowHasRoot}");
                }
                catch (Exception ex) { Console.WriteLine($"  ✗ _AttachedEntity: {ex.Message}"); }
            }

            // ── Test 2: AddLeader([Point2d, GeometryIntent]) ──────────────────
            Console.WriteLine("\n=== Test 2: AddLeader([Point2d, GeometryIntent]) ===");
            if (freshIntent != null)
            {
                try
                {
                    // Tạo freshIntent2 riêng cho test này
                    dynamic gi2 = sheet.CreateGeometryIntent(srcGeo, srcIntent);
                    var pts2 = app.TransientObjects.CreateObjectCollection();
                    pts2.Add(app.TransientGeometry.CreatePoint2d(
                        (double)srcLeaf.Position.X, (double)srcLeaf.Position.Y));
                    pts2.Add(gi2);
                    newSym.Leader.AddLeader(pts2);
                    Console.WriteLine($"  ✓ AddLeader([Point2d, GeometryIntent]) THÀNH CÔNG");
                    Console.WriteLine($"    HasRootNode sau: {(bool)newSym.Leader.HasRootNode}");
                }
                catch (Exception ex) { Console.WriteLine($"  ✗ [Point2d, GeometryIntent]: {ex.Message}"); }
            }

            // ── Test 3: AddLeader([GeometryIntent]) — sau khi đã set _AttachedEntity ──
            Console.WriteLine("\n=== Test 3: AddLeader([GeometryIntent]) (fresh symbol, fresh intent) ===");
            dynamic newSym2 = sheet.SketchedSymbols.Add(
                srcSym.Definition,
                app.TransientGeometry.CreatePoint2d(testX + 5.0, testY),
                (double)srcSym.Rotation, (double)srcSym.Scale,
                BuildPromptStrings(srcSym.Definition));
            Console.WriteLine($"  Add() symbol2: '{newSym2.Name}'  HasRootNode={(bool)newSym2.Leader.HasRootNode}");
            try
            {
                dynamic gi3 = sheet.CreateGeometryIntent(srcGeo, srcIntent);
                var pts3 = app.TransientObjects.CreateObjectCollection();
                pts3.Add(gi3);
                newSym2.Leader.AddLeader(pts3);
                Console.WriteLine($"  ✓ AddLeader([GeometryIntent]) THÀNH CÔNG  HasRootNode={(bool)newSym2.Leader.HasRootNode}");
            }
            catch (Exception ex) { Console.WriteLine($"  ✗ AddLeader([GeometryIntent]): {ex.Message}"); }

            // ── Test 4: Gán leaf.AttachedEntity trực tiếp bằng srcLeaf.AttachedEntity ──
            Console.WriteLine("\n=== Test 4: leaf.AttachedEntity = srcLeaf.AttachedEntity (nếu HasRootNode=True) ===");
            try
            {
                bool hr = (bool)newSym.Leader.HasRootNode;
                Console.WriteLine($"  newSym.Leader.HasRootNode = {hr}");
                if (hr)
                {
                    foreach (dynamic leaf in newSym.Leader.AllLeafNodes)
                    {
                        Console.WriteLine($"  Leaf before: pos=({(double)leaf.Position.X:F4},{(double)leaf.Position.Y:F4})  att={(IsNull(leaf.AttachedEntity) ? "null" : "CÓ")}");
                        try
                        {
                            dynamic gi4 = sheet.CreateGeometryIntent(srcGeo, srcIntent);
                            leaf.AttachedEntity = gi4;
                            Console.WriteLine($"  ✓ leaf.AttachedEntity SET  att={(IsNull(leaf.AttachedEntity) ? "null" : "CÓ")}");
                        }
                        catch (Exception ex) { Console.WriteLine($"  ✗ leaf.AttachedEntity: {ex.Message}"); }
                        try
                        {
                            leaf.Position = app.TransientGeometry.CreatePoint2d(
                                (double)srcLeaf.Position.X, (double)srcLeaf.Position.Y);
                            Console.WriteLine($"  ✓ leaf.Position SET → ({(double)leaf.Position.X:F4},{(double)leaf.Position.Y:F4})");
                        }
                        catch (Exception ex) { Console.WriteLine($"  ✗ leaf.Position: {ex.Message}"); }
                        break;
                    }
                }
                else Console.WriteLine("  HasRootNode=False, bỏ qua test 4.");
            }
            catch (Exception ex) { Console.WriteLine($"  ✗ Test 4: {ex.Message}"); }
        }
        catch (Exception ex) { Console.WriteLine($"\n✗ Outer test THẤT BẠI: {ex.Message}"); }
        finally
        {
            if (tx != null)
            {
                try { tx.Abort(); Console.WriteLine("\n✓ Transaction ABORTED — undo mọi thay đổi"); }
                catch (Exception ex) { Console.WriteLine($"✗ Abort: {ex.Message}"); }
            }
        }
    }

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

    static bool IsNull(object o)
    {
        if (o == null) return true;
        try { return o is System.Reflection.Missing; } catch { return false; }
    }

    static void RunTypeMetadata()
    {
        var asm = Assembly.LoadFrom(
            @"C:\Program Files\Autodesk\Inventor 2023\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll");
        Console.WriteLine("=== SketchedSymbol members ===");
        foreach (var m in asm.GetType("Inventor.SketchedSymbol")
                              .GetMembers(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name))
        {
            if (m is PropertyInfo pi) Console.WriteLine($"  PROP   {pi.PropertyType.Name,-30} {pi.Name}");
            else if (m is MethodInfo mi && !mi.IsSpecialName)
                Console.WriteLine($"  METHOD {mi.ReturnType.Name,-30} {mi.Name}({string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }
    }
}
