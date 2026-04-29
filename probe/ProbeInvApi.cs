using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

class ProbeInvApi
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Dump SketchedSymbol API surface ===\n");

        dynamic app;
        try { app = Marshal.GetActiveObject("Inventor.Application"); }
        catch { Console.WriteLine("Không kết nối Inventor."); return; }

        dynamic doc;
        try { doc = app.ActiveDocument; }
        catch { Console.WriteLine("Không có document."); return; }

        dynamic sheet;
        try { sheet = doc.ActiveSheet; }
        catch { Console.WriteLine("Không có sheet."); return; }

        // Lấy 1 symbol rotation=180° để probe kỹ
        dynamic target = null;
        foreach (dynamic s in sheet.SketchedSymbols)
        {
            try
            {
                double rot = (double)s.Rotation;
                if (Math.Abs(rot - Math.PI) < 0.01)
                {
                    target = s;
                    Console.WriteLine($"Target: '{s.Name}' rotation={rot:F4} ({rot * 180 / Math.PI:F1}°)\n");
                    break;
                }
            }
            catch { }
        }
        if (target == null) { Console.WriteLine("Không tìm thấy symbol rotation=180°."); return; }

        // ── 1. Probe property candidates qua COM late-binding ──
        Console.WriteLine("--- Probe properties ---");
        string[] candidates = new[]
        {
            "Rotation", "Scale", "Position", "Static",
            "IsMirrored", "Mirror", "MirrorX", "MirrorY", "Flipped",
            "FlipHorizontal", "FlipVertical", "Reverse",
            "Transformation", "Transform", "GetTransform", "GetMatrix",
            "Orientation", "Direction",
            "HorizontalJustification", "VerticalJustification",
            "Layer", "LineType", "LineWeight", "SymbolClipping", "LeaderVisible",
            "HasFlip", "IsFlipped", "ReflectX", "ReflectY",
            "Callout",
            "_AttachedEntity", "AttachedEntity",
            "Definition", "Name", "Parent"
        };

        foreach (string name in candidates)
        {
            try
            {
                var result = target.GetType().InvokeMember(
                    name, BindingFlags.GetProperty | BindingFlags.GetField,
                    null, target, null);
                string val;
                if (result == null) val = "null";
                else if (result is bool) val = ((bool)result).ToString();
                else if (result is double) val = ((double)result).ToString("F4");
                else if (result is int) val = ((int)result).ToString();
                else if (result is string) val = "\"" + (string)result + "\"";
                else val = result.GetType().Name;
                Console.WriteLine($"  [OK]    {name,-30} = {val}");
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                if (msg.Contains("Unknown name") || msg.Contains("không xác định được"))
                {
                    // skip — property không tồn tại
                }
                else
                {
                    Console.WriteLine($"  [ERR]   {name,-30} {msg.Substring(0, Math.Min(msg.Length, 80))}");
                }
            }
        }

        // ── 2. Dispatch members via ITypeInfo ──
        Console.WriteLine("\n--- Dispatch members (all func/property names) ---");
        try
        {
            IntPtr pUnk = Marshal.GetIUnknownForObject(target);
            IntPtr pDisp;
            Marshal.QueryInterface(pUnk, ref IID_IDispatch, out pDisp);
            var disp = (IDispatch)Marshal.GetObjectForIUnknown(pDisp);

            disp.GetTypeInfoCount(out int count);
            if (count > 0)
            {
                disp.GetTypeInfo(0, 0, out IntPtr pTypeInfo);
                var ti = (ITypeInfo)Marshal.GetObjectForIUnknown(pTypeInfo);
                ti.GetTypeAttr(out IntPtr pAttr);
                var attr = Marshal.PtrToStructure<TYPEATTR>(pAttr);
                Console.WriteLine($"  cFuncs: {attr.cFuncs}");

                var names = new HashSet<string>();
                for (int i = 0; i < attr.cFuncs; i++)
                {
                    try
                    {
                        ti.GetFuncDesc(i, out IntPtr pFunc);
                        var fd = Marshal.PtrToStructure<FUNCDESC>(pFunc);
                        string[] nameArr = new string[1];
                        ti.GetNames(fd.memid, nameArr, 1, out int cNames);
                        string n = nameArr[0] ?? "?";
                        string kind;
                        if (fd.invkind == 1) kind = "method";
                        else if (fd.invkind == 2) kind = "get";
                        else if (fd.invkind == 4) kind = "put";
                        else if (fd.invkind == 8) kind = "putref";
                        else kind = fd.invkind.ToString();
                        string key = $"{kind,6} {n}";
                        if (names.Add(key))
                            Console.WriteLine($"    {key}");
                        ti.ReleaseFuncDesc(pFunc);
                    }
                    catch { }
                }
                ti.ReleaseTypeAttr(pAttr);
                Marshal.Release(pTypeInfo);
            }
            Marshal.Release(pDisp);
            Marshal.Release(pUnk);
        }
        catch (Exception ex) { Console.WriteLine($"  ERR Dispatch: {ex.Message}"); }

        // ── 3. Thử Transformation matrix ──
        Console.WriteLine("\n--- Transformation matrix ---");
        try
        {
            dynamic m = target.Transformation;
            if (m == null) { Console.WriteLine("  Transformation = null"); }
            else
            {
                Console.WriteLine($"  Type: {m.GetType().Name}");
                // Matrix2d có các method: GetMatrixData(double[16]), Determinant, Cell(row,col)
                try
                {
                    // Cell là method, không phải indexer — thử Cell(row, col)
                    for (int r = 1; r <= 4; r++)
                    {
                        string row = "  ";
                        for (int c = 1; c <= 4; c++)
                        {
                            try { double v = (double)m.Cell[r, c]; row += $"{v,8:F4} "; }
                            catch { row += "   ERR   "; }
                        }
                        Console.WriteLine(row);
                    }
                }
                catch (Exception ex) { Console.WriteLine($"  Cell ERR: {ex.Message}"); }

                try { double det = (double)m.Determinant; Console.WriteLine($"  Determinant: {det:F4}"); }
                catch (Exception ex) { Console.WriteLine($"  Determinant ERR: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Console.WriteLine($"  Transformation ERR: {ex.Message}"); }
    }

    static Guid IID_IDispatch = new Guid("00020400-0000-0000-C000-000000000046");

    [ComImport, Guid("00020400-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDispatch
    {
        void GetTypeInfoCount(out int pctinfo);
        void GetTypeInfo(int iTInfo, int lcid, out IntPtr ppTInfo);
    }

    [ComImport, Guid("00020401-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITypeInfo
    {
        void GetTypeAttr(out IntPtr ppTypeAttr);
        void _slot1();
        void GetFuncDesc(int index, out IntPtr ppFuncDesc);
        void _slot3();
        void GetNames(int memid, [Out, MarshalAs(UnmanagedType.LPArray)] string[] rgBstrNames, int cMaxNames, out int pcNames);
        void _slot5(); void _slot6(); void _slot7(); void _slot8(); void _slot9(); void _slot10(); void _slot11();
        void ReleaseTypeAttr(IntPtr pTypeAttr);
        void ReleaseFuncDesc(IntPtr pFuncDesc);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TYPEATTR
    {
        public Guid guid;
        public int lcid;
        public int dwReserved;
        public int memidConstructor;
        public int memidDestructor;
        public IntPtr lpstrSchema;
        public int cbSizeInstance;
        public short typekind;
        public short cFuncs;
        public short cVars;
        public short cImplTypes;
        public short cbSizeVft;
        public short cbAlignment;
        public short wTypeFlags;
        public short wMajorVerNum;
        public short wMinorVerNum;
        public int tdescAlias_vt;
        public IntPtr tdescAlias_lpValue;
        public int idldescType_dwReserved;
        public short idldescType_wIDLFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FUNCDESC
    {
        public int memid;
        public IntPtr lprgscode;
        public IntPtr lprgelemdescParam;
        public int funckind;
        public int invkind;
        public int callconv;
        public short cParams;
        public short cParamsOpt;
        public short oVft;
        public short cScodes;
        public int elemdescFunc_tdesc_vt;
        public IntPtr elemdescFunc_tdesc_lpValue;
        public int elemdescFunc_paramdesc_pparamdescex;
        public short elemdescFunc_paramdesc_wParamFlags;
        public short wFuncFlags;
    }
}
