// Explorer thumbnail provider for .3mf files.
// Shows the preview image that slicers (Bambu Studio, PrusaSlicer, Orca, Cura ...) embed in the 3MF package.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using ComIStream = System.Runtime.InteropServices.ComTypes.IStream;

[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]

namespace ThreeMFThumb
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b824b49d-22ac-4161-ac8a-9916e8fa3f7f")]
    public interface IInitializeWithStream
    {
        void Initialize(ComIStream pstream, uint grfMode);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("e357fccd-a995-4576-b01f-234630154e96")]
    public interface IThumbnailProvider
    {
        void GetThumbnail(uint cx, out IntPtr phbmp, out uint pdwAlpha);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None), Guid("8d5dcf6c-7129-43f1-b09d-6532d963b208")]
    public class ThumbnailProvider : IInitializeWithStream, IThumbnailProvider
    {
        const uint WTSAT_ARGB = 2;
        const int E_FAIL = unchecked((int)0x80004005);

        byte[] data;

        public void Initialize(ComIStream pstream, uint grfMode)
        {
            data = ReadStream(pstream);
        }

        internal static byte[] ReadStream(ComIStream pstream)
        {
            var ms = new MemoryStream();
            var buf = new byte[81920];
            IntPtr pRead = Marshal.AllocCoTaskMem(4);
            try
            {
                while (true)
                {
                    pstream.Read(buf, buf.Length, pRead);
                    int n = Marshal.ReadInt32(pRead);
                    if (n <= 0) break;
                    ms.Write(buf, 0, n);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pRead);
                Marshal.ReleaseComObject(pstream);
            }
            return ms.ToArray();
        }

        // Embedded slicer preview if present, otherwise a render of the geometry. Null if neither works.
        internal static Bitmap LoadImage(byte[] file, int renderSize)
        {
            byte[] png = FindPreview(file);
            if (png != null) return new Bitmap(new MemoryStream(png));
            return MeshRenderer.Render(file, renderSize);
        }

        public void GetThumbnail(uint cx, out IntPtr phbmp, out uint pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WTSAT_ARGB;
            if (data == null) throw new COMException("not initialized", E_FAIL);
            using (var src = LoadImage(data, (int)Math.Min(cx, 1024)))
            {
                if (src == null) throw new COMException("no preview", E_FAIL);
                double scale = Math.Min((double)cx / src.Width, (double)cx / src.Height);
                if (scale > 1) scale = 1;
                int w = Math.Max(1, (int)Math.Round(src.Width * scale));
                int h = Math.Max(1, (int)Math.Round(src.Height * scale));
                using (var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(dst))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    phbmp = ToDib(dst);
                }
            }
        }

        static byte[] FindPreview(byte[] file)
        {
            using (var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read))
            {
                // Prefer the slicer's render of the model over cover photos (MakerWorld projects
                // put a photo of the print into the standard thumbnail slot).
                var candidates = new System.Collections.Generic.List<string> { "Metadata/plate_1.png", "Metadata/thumbnail.png" };

                // Standard OPC thumbnail relationship, then slicer specific ones.
                var rels = zip.GetEntry("_rels/.rels");
                if (rels != null)
                {
                    try
                    {
                        XDocument doc;
                        using (var s = rels.Open()) doc = XDocument.Load(s);
                        foreach (var r in doc.Root.Elements())
                        {
                            string type = (string)r.Attribute("Type") ?? "";
                            string target = ((string)r.Attribute("Target") ?? "").TrimStart('/');
                            if (type.EndsWith("/metadata/thumbnail") || type.EndsWith("/cover-thumbnail-middle")) candidates.Add(target);
                        }
                    }
                    catch (Exception) { }
                }
                foreach (var name in candidates)
                {
                    var e = zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, name, StringComparison.OrdinalIgnoreCase));
                    if (e != null && e.Length > 0) return ReadAll(e);
                }
                return null; // caller renders the geometry instead
            }
        }

        internal static byte[] ReadAll(ZipArchiveEntry e)
        {
            using (var s = e.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        // Top-down 32bpp DIB with premultiplied alpha, as the shell expects for WTSAT_ARGB.
        static IntPtr ToDib(Bitmap bmp)
        {
            var bi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = bmp.Width,
                biHeight = -bmp.Height,
                biPlanes = 1,
                biBitCount = 32
            };
            IntPtr bits;
            IntPtr hbmp = CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (hbmp == IntPtr.Zero) throw new COMException("CreateDIBSection failed", E_FAIL);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int rowBytes = bmp.Width * 4;
                var row = new byte[rowBytes];
                for (int y = 0; y < bmp.Height; y++)
                {
                    Marshal.Copy(bd.Scan0 + y * bd.Stride, row, 0, rowBytes);
                    Marshal.Copy(row, 0, bits + y * rowBytes, rowBytes);
                }
            }
            finally { bmp.UnlockBits(bd); }
            return hbmp;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight;
            public ushort biPlanes; public ushort biBitCount; public uint biCompression;
            public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public uint biClrUsed; public uint biClrImportant;
        }

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
    }
}
