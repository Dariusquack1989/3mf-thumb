// Minimal z-buffer renderer for the geometry inside a 3MF package.
// Resolves build items, components and the production extension (p:path) used by Bambu Studio.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;

namespace ThreeMFThumb
{
    static class MeshRenderer
    {
        const string ProdNs = "http://schemas.microsoft.com/3dmanufacturing/production/2015/06";

        class Obj
        {
            public List<double> V = new List<double>();
            public List<int> T = new List<int>();
            public List<Tuple<string, string, double[]>> Comps = new List<Tuple<string, string, double[]>>();
        }

        class Model
        {
            public Dictionary<string, Obj> Objects = new Dictionary<string, Obj>();
            public List<Tuple<string, string, double[]>> Build = new List<Tuple<string, string, double[]>>();
        }

        public static Bitmap Render(byte[] file, int size)
        {
            if (size < 16) size = 16;
            var tris = new List<double>(); // 9 doubles per triangle, world space
            using (var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read))
            {
                var models = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);
                Func<string, Model> get = null;
                get = path =>
                {
                    path = path.TrimStart('/');
                    Model m;
                    if (models.TryGetValue(path, out m)) return m;
                    var e = zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, path, StringComparison.OrdinalIgnoreCase));
                    m = e == null ? null : Parse(e);
                    models[path] = m;
                    return m;
                };

                var root = zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, "3D/3dmodel.model", StringComparison.OrdinalIgnoreCase))
                        ?? zip.Entries.FirstOrDefault(x => x.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase));
                if (root == null) return null;
                string rootPath = root.FullName;
                var rm = get(rootPath);
                if (rm == null) return null;

                Action<string, string, double[], int> emit = null;
                emit = (path, id, m, depth) =>
                {
                    if (depth > 16) return;
                    var mdl = get(path);
                    Obj o;
                    if (mdl == null || !mdl.Objects.TryGetValue(id, out o)) return;
                    for (int i = 0; i + 2 < o.T.Count; i += 3)
                        for (int k = 0; k < 3; k++)
                        {
                            int vi = o.T[i + k] * 3;
                            if (vi + 2 >= o.V.Count) { tris.RemoveRange(tris.Count - k * 3, k * 3); goto next; }
                            double x = o.V[vi], y = o.V[vi + 1], z = o.V[vi + 2];
                            tris.Add(x * m[0] + y * m[3] + z * m[6] + m[9]);
                            tris.Add(x * m[1] + y * m[4] + z * m[7] + m[10]);
                            tris.Add(x * m[2] + y * m[5] + z * m[8] + m[11]);
                        }
                        next:;
                    foreach (var c in o.Comps)
                        emit(c.Item1 ?? path, c.Item2, Mul(c.Item3, m), depth + 1);
                };

                var items = rm.Build.Count > 0 ? rm.Build
                    : rm.Objects.Keys.Select(k => Tuple.Create<string, string, double[]>(null, k, Identity())).ToList();
                foreach (var b in items)
                    emit(b.Item1 ?? rootPath, b.Item2, b.Item3, 0);
            }
            if (tris.Count < 9) return null;
            return Rasterize(tris, size);
        }

        static Model Parse(ZipArchiveEntry e)
        {
            var m = new Model();
            Obj cur = null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using (var s = e.Open())
            using (var r = XmlReader.Create(s, settings))
            {
                while (r.Read())
                {
                    if (r.NodeType != XmlNodeType.Element) continue;
                    switch (r.LocalName)
                    {
                        case "object":
                            cur = new Obj();
                            m.Objects[r.GetAttribute("id") ?? ""] = cur;
                            break;
                        case "vertex":
                            if (cur != null)
                            {
                                cur.V.Add(Num(r.GetAttribute("x")));
                                cur.V.Add(Num(r.GetAttribute("y")));
                                cur.V.Add(Num(r.GetAttribute("z")));
                            }
                            break;
                        case "triangle":
                            if (cur != null)
                            {
                                cur.T.Add(Int(r.GetAttribute("v1")));
                                cur.T.Add(Int(r.GetAttribute("v2")));
                                cur.T.Add(Int(r.GetAttribute("v3")));
                            }
                            break;
                        case "component":
                            if (cur != null)
                                cur.Comps.Add(Tuple.Create(r.GetAttribute("path", ProdNs), r.GetAttribute("objectid") ?? "",
                                    Transform(r.GetAttribute("transform"))));
                            break;
                        case "item":
                            m.Build.Add(Tuple.Create(r.GetAttribute("path", ProdNs), r.GetAttribute("objectid") ?? "",
                                Transform(r.GetAttribute("transform"))));
                            break;
                    }
                }
            }
            return m;
        }

        static double Num(string s)
        {
            double d;
            return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? d : 0;
        }

        static int Int(string s)
        {
            int i;
            return int.TryParse(s, out i) ? i : 0;
        }

        static double[] Identity() { return new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 }; }

        static double[] Transform(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Identity();
            var p = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(Num).ToArray();
            return p.Length == 12 ? p : Identity();
        }

        // 3MF affine matrices are row-vector 4x3: p' = p * A, then * B.
        static double[] Mul(double[] a, double[] b)
        {
            var r = new double[12];
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                {
                    double v = row == 3 ? b[9 + col] : 0;
                    for (int k = 0; k < 3; k++) v += a[row * 3 + k] * b[k * 3 + col];
                    r[row * 3 + col] = v;
                }
            return r;
        }

        static Bitmap Rasterize(List<double> t, int size)
        {
            // Isometric-ish view: rotate -45 deg around Z, tilt 35 deg, Z up.
            double ca = Math.Cos(-Math.PI / 4), sa = Math.Sin(-Math.PI / 4);
            double ce = Math.Cos(Math.PI * 35 / 180), se = Math.Sin(Math.PI * 35 / 180);
            int n = t.Count / 3;
            var px = new double[n]; var py = new double[n]; var pz = new double[n];
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double x = t[i * 3], y = t[i * 3 + 1], z = t[i * 3 + 2];
                double rx = x * ca - y * sa, ry = x * sa + y * ca;
                double sx = rx;                    // screen right
                double sy = z * ce - ry * se;      // screen up
                double depth = ry * ce + z * se;   // larger = farther
                px[i] = sx; py[i] = sy; pz[i] = depth;
                if (sx < minX) minX = sx; if (sx > maxX) maxX = sx;
                if (sy < minY) minY = sy; if (sy > maxY) maxY = sy;
            }
            double span = Math.Max(maxX - minX, maxY - minY);
            if (span <= 0) return null;
            double margin = size * 0.06;
            double scale = (size - 2 * margin) / span;
            double offX = (size - (maxX - minX) * scale) / 2, offY = (size - (maxY - minY) * scale) / 2;
            for (int i = 0; i < n; i++)
            {
                px[i] = (px[i] - minX) * scale + offX;
                py[i] = size - ((py[i] - minY) * scale + offY);
            }

            var zbuf = new double[size * size];
            for (int i = 0; i < zbuf.Length; i++) zbuf[i] = double.MaxValue;
            var pix = new int[size * size];

            // Light from upper-left-front in view space.
            double lx = -0.4, ly = 0.6, lz = -0.7;
            double ll = Math.Sqrt(lx * lx + ly * ly + lz * lz); lx /= ll; ly /= ll; lz /= ll;

            for (int f = 0; f + 2 < n; f += 3)
            {
                double x0 = px[f], y0 = py[f], z0 = pz[f];
                double x1 = px[f + 1], y1 = py[f + 1], z1 = pz[f + 1];
                double x2 = px[f + 2], y2 = py[f + 2], z2 = pz[f + 2];

                // Normal in view space (screen y down, so flip y).
                double ax = x1 - x0, ay = -(y1 - y0), az = z1 - z0;
                double bx = x2 - x0, by = -(y2 - y0), bz = z2 - z0;
                double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl == 0) continue;
                nx /= nl; ny /= nl; nz /= nl;
                double diff = Math.Abs(nx * lx + ny * ly + nz * lz);
                double shade = 0.35 + 0.65 * diff;
                int r = (int)(110 * shade + 20), g = (int)(160 * shade + 25), b = (int)(220 * shade + 30);
                int color = unchecked((int)0xFF000000) | (Math.Min(r, 255) << 16) | (Math.Min(g, 255) << 8) | Math.Min(b, 255);

                double area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
                if (area == 0) continue;
                int minx = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
                int maxx = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
                int miny = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
                int maxy = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
                for (int y = miny; y <= maxy; y++)
                {
                    double cy = y + 0.5;
                    for (int x = minx; x <= maxx; x++)
                    {
                        double cxp = x + 0.5;
                        double w0 = ((x1 - cxp) * (y2 - cy) - (x2 - cxp) * (y1 - cy)) / area;
                        double w1 = ((x2 - cxp) * (y0 - cy) - (x0 - cxp) * (y2 - cy)) / area;
                        double w2 = 1 - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        double d = w0 * z0 + w1 * z1 + w2 * z2;
                        int idx = y * size + x;
                        if (d < zbuf[idx]) { zbuf[idx] = d; pix[idx] = color; }
                    }
                }
            }

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            var bd = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < size; y++)
                    Marshal.Copy(pix, y * size, bd.Scan0 + y * bd.Stride, size);
            }
            finally { bmp.UnlockBits(bd); }
            return bmp;
        }
    }
}
