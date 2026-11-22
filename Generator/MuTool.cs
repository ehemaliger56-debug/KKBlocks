// MuTool.cs - .mu reader/dumper + generator for KKBlocks (Kerbal Konstructs block statics)
// Build (Windows, .NET Framework 4):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /out:MuTool.exe /r:System.Drawing.dll MuTool.cs
//
// Usage:
//   MuTool.exe dump <file.mu> [more.mu ...]     - parse and print structure
//   MuTool.exe gen <outdir> <size,size,...>     - generate cube + shape .mu files + KKBlockTex.png
//   MuTool.exe check <file.mu>                  - dump + winding consistency report
//   MuTool.exe colliders <dir>                  - list collider types used by all .mu below dir
//
// .mu format reference: taniwha/io_object_mu (mu.py), format version 5.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace MuToolNs
{
    class MuTool
    {
        const int MODEL_BINARY = 76543;
        const int FILE_VERSION = 5;

        static BinaryReader r;
        static int version;

        // ------------------------------------------------------------------ reader helpers

        static string ReadString()
        {
            int n = ReadVarint();
            byte[] b = r.ReadBytes(n);
            if (b.Length != n) throw new IOException("EOF while reading string");
            return Encoding.UTF8.GetString(b);
        }

        static int ReadVarint()
        {
            int val = 0, mult = 1;
            while (true)
            {
                byte b = r.ReadByte();
                val += (b & 127) * mult;
                if (b < 128) break;
                mult *= 128;
            }
            return val;
        }

        static float[] Vec() { return new float[] { r.ReadSingle(), r.ReadSingle(), r.ReadSingle() }; }
        static float[] Quat() { return new float[] { r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle() }; }

        // ------------------------------------------------------------------ data model

        class MeshData
        {
            public int numVerts, numSubmeshes;
            public List<float[]> verts = new List<float[]>();
            public List<float[]> uvs = new List<float[]>();
            public List<float[]> normals = new List<float[]>();
            public List<int[]> submeshes = new List<int[]>();
        }

        class Material
        {
            public string name, shader;
            public List<string> propNames = new List<string>();
            public List<int> propTypes = new List<int>();
            public List<object> propVals = new List<object>(); // float[4] color/vector, float scalar, object[] {int texIdx, float sx, float sy, float ox, float oy}
        }

        class Texture { public string name; public int type; }

        class Node
        {
            public string name;
            public float[] pos, rot, scale;
            public string tag = ""; public int layer;
            public byte trigger, convex;
            public float[] boxSize, boxCenter, sphereC, capsuleC;
            public float sphereR, capsuleR, capsuleH; public int capsuleDir;
            public MeshData mesh;
            public byte cast, recv; public int[] matIdx;
            public int colKind; // collider entry type found (0 = none, 25/3 mesh, 26/4 sphere, 27/5 capsule, 28/6 box)
            public List<Node> children = new List<Node>();
        }

        static List<Material> mats;
        static List<Texture> texs;

        static Material ReadMat()
        {
            Material m = new Material();
            m.name = ReadString();
            m.shader = ReadString();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                m.propNames.Add(ReadString());
                int t = r.ReadInt32();
                m.propTypes.Add(t);
                if (t == 0 || t == 1) m.propVals.Add(new float[] { r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle() });
                else if (t == 2 || t == 3) m.propVals.Add(r.ReadSingle());
                else if (t == 4)
                {
                    object[] tv = new object[5];
                    tv[0] = r.ReadInt32();
                    tv[1] = r.ReadSingle(); tv[2] = r.ReadSingle(); // scale
                    tv[3] = r.ReadSingle(); tv[4] = r.ReadSingle(); // offset
                    m.propVals.Add(tv);
                }
                else throw new IOException("unknown material prop type " + t);
            }
            return m;
        }

        static MeshData ReadMesh()
        {
            MeshData m = new MeshData();
            int start = r.ReadInt32();
            if (start != 13) throw new IOException("ET_MESH_START expected, got " + start);
            m.numVerts = r.ReadInt32();
            m.numSubmeshes = r.ReadInt32();
            while (true)
            {
                int t = r.ReadInt32();
                if (t == 22) break;
                switch (t)
                {
                    case 14: for (int i = 0; i < m.numVerts; i++) m.verts.Add(Vec()); break;
                    case 15: for (int i = 0; i < m.numVerts; i++) m.uvs.Add(new float[] { r.ReadSingle(), r.ReadSingle() }); break;
                    case 16: for (int i = 0; i < m.numVerts; i++) { r.ReadSingle(); r.ReadSingle(); } break; // uv2 (skip)
                    case 17: for (int i = 0; i < m.numVerts; i++) m.normals.Add(Vec()); break;
                    case 18: for (int i = 0; i < m.numVerts; i++) { r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); } break; // tangents (skip)
                    case 19:
                        {
                            int n = r.ReadInt32();
                            int[] tri = new int[n];
                            for (int i = 0; i < n; i++) tri[i] = r.ReadInt32();
                            m.submeshes.Add(tri);
                            break;
                        }
                    case 20: for (int i = 0; i < m.numVerts; i++) { for (int k = 0; k < 4; k++) r.ReadInt32(); for (int k = 0; k < 4; k++) r.ReadSingle(); } break;
                    case 21: { int n = r.ReadInt32(); for (int i = 0; i < n * 16; i++) r.ReadSingle(); break; }
                    case 32: for (int i = 0; i < m.numVerts; i++) { r.ReadByte(); r.ReadByte(); r.ReadByte(); r.ReadByte(); } break;
                    default: throw new IOException("unknown mesh entry " + t + " at 0x" + r.BaseStream.Position.ToString("X"));
                }
            }
            return m;
        }

        static void ReadAnim()
        {
            int nClips = r.ReadInt32();
            for (int c = 0; c < nClips; c++)
            {
                ReadString(); Vec(); Vec(); r.ReadInt32(); // name, lbCenter, lbSize, wrapMode
                int nCurves = r.ReadInt32();
                for (int k = 0; k < nCurves; k++)
                {
                    ReadString(); ReadString();
                    int type = r.ReadInt32();
                    int pre = r.ReadInt32();
                    int post;
                    int numKeys;
                    if (type == 8) { post = pre; numKeys = r.ReadInt32(); }
                    else { r.ReadInt32(); numKeys = r.ReadInt32(); }
                    for (int i = 0; i < numKeys; i++) { r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); r.ReadInt32(); }
                }
            }
            ReadString(); // active clip
            r.ReadByte(); // autoPlay
        }

        static void ReadSkinned()
        {
            int nMats = r.ReadInt32();
            for (int i = 0; i < nMats; i++) r.ReadInt32();
            Vec(); Vec(); r.ReadInt32(); r.ReadByte();
            int nBones = r.ReadInt32();
            for (int i = 0; i < nBones; i++) ReadString();
            ReadMesh();
        }

        static Node ReadObject()
        {
            Node n = new Node();
            n.name = ReadString();
            n.pos = Vec(); n.rot = Quat(); n.scale = Vec();
            while (true)
            {
                int t;
                try { t = r.ReadInt32(); }
                catch (EndOfStreamException) { break; } // files may end without ET_CHILD_TRANSFORM_END
                if (t == 0) { n.children.Add(ReadObject()); }
                else if (t == 1) { break; }
                else if (t == 24) { n.tag = ReadString(); n.layer = r.ReadInt32(); }
                else if (t == 25) { n.colKind = 25; n.trigger = r.ReadByte(); n.convex = r.ReadByte(); n.mesh = ReadMesh(); }
                else if (t == 26) { n.colKind = 26; n.trigger = r.ReadByte(); n.sphereR = r.ReadSingle(); n.sphereC = Vec(); }
                else if (t == 27) { n.colKind = 27; n.trigger = r.ReadByte(); n.capsuleR = r.ReadSingle(); n.capsuleH = r.ReadSingle(); n.capsuleDir = r.ReadInt32(); n.capsuleC = Vec(); }
                else if (t == 28) { n.colKind = 28; n.trigger = r.ReadByte(); n.boxSize = Vec(); n.boxCenter = Vec(); }
                else if (t == 4) { n.colKind = 4; n.sphereR = r.ReadSingle(); n.sphereC = Vec(); }
                else if (t == 5) { n.colKind = 5; n.capsuleR = r.ReadSingle(); n.capsuleH = r.ReadSingle(); n.capsuleDir = r.ReadInt32(); n.capsuleC = Vec(); }
                else if (t == 6) { n.colKind = 6; n.boxSize = Vec(); n.boxCenter = Vec(); }
                else if (t == 3) { n.colKind = 3; n.convex = r.ReadByte(); n.mesh = ReadMesh(); }
                else if (t == 7) { n.mesh = ReadMesh(); }
                else if (t == 8)
                {
                    n.cast = r.ReadByte(); n.recv = r.ReadByte();
                    int cnt = r.ReadInt32();
                    n.matIdx = new int[cnt];
                    for (int i = 0; i < cnt; i++) n.matIdx[i] = r.ReadInt32();
                }
                else if (t == 10) { int cnt = r.ReadInt32(); for (int i = 0; i < cnt; i++) mats.Add(ReadMat()); }
                else if (t == 12)
                {
                    int cnt = r.ReadInt32();
                    for (int i = 0; i < cnt; i++) { Texture tx = new Texture(); tx.name = ReadString(); tx.type = r.ReadInt32(); texs.Add(tx); }
                }
                else if (t == 23) { r.ReadInt32(); r.ReadSingle(); r.ReadSingle(); Vec(); r.ReadUInt32(); if (version > 1) r.ReadSingle(); }
                else if (t == 2) { ReadAnim(); }
                else if (t == 9) { ReadSkinned(); }
                else if (t == 30) { r.ReadInt32(); Vec(); r.ReadUInt32(); r.ReadByte(); r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); }
                else throw new IOException("Unknown node entry " + t + " at 0x" + r.BaseStream.Position.ToString("X"));
            }
            return n;
        }

        // ------------------------------------------------------------------ dump

        static string F(float v) { return v.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        static string V(float[] v) { return "(" + F(v[0]) + "," + F(v[1]) + "," + F(v[2]) + ")"; }

        static void DumpNode(Node n, string indent)
        {
            Console.WriteLine(indent + "Transform: " + n.name + "  pos=" + V(n.pos) + "  rot=" + V(new float[] { n.rot[0], n.rot[1], n.rot[2] }) + "," + F(n.rot[3]) + "  scale=" + V(n.scale) + "  tag=" + n.tag + " layer=" + n.layer);
            if (n.boxSize != null) Console.WriteLine(indent + "  BoxCollider: trigger=" + n.trigger + " size=" + V(n.boxSize) + " center=" + V(n.boxCenter));
            if (n.sphereC != null) Console.WriteLine(indent + "  SphereCollider: trigger=" + n.trigger + " radius=" + F(n.sphereR) + " center=" + V(n.sphereC));
            if (n.capsuleC != null) Console.WriteLine(indent + "  CapsuleCollider: trigger=" + n.trigger + " r=" + F(n.capsuleR) + " h=" + F(n.capsuleH) + " dir=" + n.capsuleDir + " center=" + V(n.capsuleC));
            if (n.colKind == 25 || n.colKind == 3)
                Console.WriteLine(indent + "  MeshCollider (ET " + n.colKind + "): trigger=" + n.trigger + " convex=" + n.convex);
            if (n.mesh != null)
            {
                MeshData m = n.mesh;
                Console.WriteLine(indent + "  Mesh: verts=" + m.numVerts + " submeshes=" + m.numSubmeshes + " (uvs:" + m.uvs.Count + " normals:" + m.normals.Count + ")");
                int report = Math.Min(12, m.numVerts);
                for (int i = 0; i < report; i++)
                    Console.WriteLine(indent + "    v" + i + " = " + V(m.verts[i]) + "  n=" + (i < m.normals.Count ? V(m.normals[i]) : "-") + "  uv=" + (i < m.uvs.Count ? "(" + F(m.uvs[i][0]) + "," + F(m.uvs[i][1]) + ")" : "-"));
                for (int s = 0; s < m.submeshes.Count; s++)
                {
                    int[] tri = m.submeshes[s];
                    StringBuilder sb = new StringBuilder();
                    int show = Math.Min(36, tri.Length);
                    for (int i = 0; i < show; i++) sb.Append(tri[i]).Append(i < show - 1 ? "," : "");
                    Console.WriteLine(indent + "    submesh " + s + ": " + (tri.Length / 3) + " tris [" + sb + (tri.Length > show ? " ...]" : "]"));
                }
            }
            if (n.matIdx != null)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < n.matIdx.Length; i++) sb.Append(n.matIdx[i]).Append(i < n.matIdx.Length - 1 ? "," : "");
                Console.WriteLine(indent + "  MeshRenderer: shadows=" + n.cast + "/" + n.recv + " materials=[" + sb + "]");
            }
            foreach (Node c in n.children) DumpNode(c, indent + "  ");
        }

        static int windingPos, windingNeg, windingZero;

        static void CheckWinding(Node n)
        {
            if (n.mesh != null && n.mesh.normals.Count == n.mesh.numVerts)
            {
                foreach (int[] sub in n.mesh.submeshes)
                {
                    for (int i = 0; i + 2 < sub.Length; i += 3)
                    {
                        float[] a = n.mesh.verts[sub[i]], b = n.mesh.verts[sub[i + 1]], c = n.mesh.verts[sub[i + 2]];
                        float[] nn = n.mesh.normals[sub[i]];
                        float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2];
                        float vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
                        float gx = uy * vz - uz * vy, gy = uz * vx - ux * vz, gz = ux * vy - uy * vx;
                        float d = gx * nn[0] + gy * nn[1] + gz * nn[2];
                        if (d > 1e-9f) windingPos++;
                        else if (d < -1e-9f) windingNeg++;
                        else windingZero++;
                    }
                }
            }
            foreach (Node c in n.children) CheckWinding(c);
        }

        static int Dump(string path, bool check)
        {
            Console.WriteLine("=== " + path + " ===");
            mats = new List<Material>();
            texs = new List<Texture>();
            windingPos = windingNeg = windingZero = 0;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (r = new BinaryReader(fs))
                {
                    int magic = r.ReadInt32();
                    version = r.ReadInt32();
                    if (magic != MODEL_BINARY || version < 0 || version > FILE_VERSION)
                    {
                        Console.WriteLine("  NOT a valid .mu (magic=" + magic + " version=" + version + ")");
                        return 1;
                    }
                    string rootName = ReadString();
                    Console.WriteLine("  version=" + version + " root='" + rootName + "'");
                    Node root = ReadObject();
                    DumpNode(root, "  ");
                    foreach (Material m in mats)
                    {
                        Console.WriteLine("  Material '" + m.name + "' shader=" + m.shader);
                        for (int i = 0; i < m.propNames.Count; i++)
                        {
                            object v = m.propVals[i];
                            if (v is float[])
                            {
                                float[] f = (float[])v;
                                Console.WriteLine("    " + m.propNames[i] + " (type " + m.propTypes[i] + ") = (" + F(f[0]) + "," + F(f[1]) + "," + F(f[2]) + "," + F(f[3]) + ")");
                            }
                            else if (v is object[])
                            {
                                object[] t = (object[])v;
                                Console.WriteLine("    " + m.propNames[i] + " (type 4 tex) idx=" + t[0] + " scale=(" + F((float)t[1]) + "," + F((float)t[2]) + ") offset=(" + F((float)t[3]) + "," + F((float)t[4]) + ")");
                            }
                            else Console.WriteLine("    " + m.propNames[i] + " (type " + m.propTypes[i] + ") = " + F((float)v));
                        }
                    }
                    foreach (Texture t in texs) Console.WriteLine("  Texture '" + t.name + "' type=" + t.type);
                    if (check) { CheckWinding(root); Console.WriteLine("  winding check: dot(cross,normal) >0:" + windingPos + "  <0:" + windingNeg + "  =0:" + windingZero); }
                    return 0;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("  ERROR: " + e.Message);
                return 1;
            }
        }

        // ------------------------------------------------------------------ collider scan

        static void ScanColliders(Node n, StringBuilder sb)
        {
            if (n.colKind != 0)
            {
                sb.Append(" ET" + n.colKind);
                if (n.colKind == 25 || n.colKind == 3) sb.Append("(mesh c" + n.convex + " t" + n.trigger + ")");
                else if (n.colKind == 26 || n.colKind == 4) sb.Append("(sphere)");
                else if (n.colKind == 27 || n.colKind == 5) sb.Append("(capsule)");
                else if (n.colKind == 28 || n.colKind == 6) sb.Append("(box)");
            }
            foreach (Node c in n.children) ScanColliders(c, sb);
        }

        // lists which collider entry types are used by all .mu files below a directory
        static int Scan(string dir)
        {
            int files = 0, withCol = 0, failed = 0;
            try
            {
                foreach (string f in Directory.EnumerateFiles(dir, "*.mu", SearchOption.AllDirectories))
                {
                    files++;
                    string name = Path.GetFileName(f);
                    try
                    {
                        using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read))
                        using (r = new BinaryReader(fs))
                        {
                            int magic = r.ReadInt32();
                            version = r.ReadInt32();
                            if (magic != MODEL_BINARY || version < 0 || version > FILE_VERSION) { Console.WriteLine(name + ": not v0-5"); continue; }
                            mats = new List<Material>();
                            texs = new List<Texture>();
                            ReadString();
                            Node root = ReadObject();
                            StringBuilder sb = new StringBuilder(name + ":");
                            ScanColliders(root, sb);
                            if (sb.Length > name.Length + 1) { Console.WriteLine(sb.ToString()); withCol++; }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(name + ": PARSE ERROR " + e.Message);
                        failed++;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
                return 1;
            }
            Console.WriteLine("scanned " + files + " .mu files, " + withCol + " with colliders, " + failed + " parse errors");
            return 0;
        }

        // ------------------------------------------------------------------ writer

        static void WInt(BinaryWriter w, int v) { w.Write(v); }
        static void WFloat(BinaryWriter w, float v) { w.Write(v); }
        static void WByte(BinaryWriter w, byte v) { w.Write(v); }
        static void WVarint(BinaryWriter w, int val)
        {
            if (val < 0) val += 1 << 32; // shouldn't happen for lengths
            while (val > 127)
            {
                w.Write((byte)((val & 127) + 128));
                val >>= 7;
            }
            w.Write((byte)val);
        }

        static void WString(BinaryWriter w, string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            WVarint(w, b.Length);
            w.Write(b);
        }
        static void WVec(BinaryWriter w, float x, float y, float z) { WFloat(w, x); WFloat(w, y); WFloat(w, z); }
        static void WQuat(BinaryWriter w, float x, float y, float z, float ww) { WFloat(w, x); WFloat(w, y); WFloat(w, z); WFloat(w, ww); }

        // cube: edge length s, x/z centered on origin, bottom face at y=0
        // Unity LHS, front faces are CW seen from outside == cross(v1-v0, v2-v0) points outward
        static MeshData BuildCube(float s)
        {
            MeshData m = new MeshData();
            float h = s / 2f;
            float[,] facePos = {
                // v0, v1, v2, v3 (x,y,z each) - perimeter order so that cross(v1-v0, v2-v0) = outward normal
                { -h, 0,  h,    h, 0,  h,    h, s,  h,   -h, s,  h },   // front  n=(0,0,1)
                { -h, 0, -h,   -h, s, -h,    h, s, -h,    h, 0, -h },   // back   n=(0,0,-1)
                {  h, 0, -h,    h, s, -h,    h, s,  h,    h, 0,  h },   // right  n=(1,0,0)
                { -h, 0, -h,   -h, 0,  h,   -h, s,  h,   -h, s, -h },   // left   n=(-1,0,0)
                { -h, s, -h,   -h, s,  h,    h, s,  h,    h, s, -h },   // top    n=(0,1,0)
                { -h, 0, -h,    h, 0, -h,    h, 0,  h,   -h, 0,  h }    // bottom n=(0,-1,0)
            };
            float[,] faceNormal = {
                { 0, 0, 1 }, { 0, 0, -1 }, { 1, 0, 0 }, { -1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }
            };
            // ONE submesh for all 6 faces (12 triangles): stock .mu meshes always use
            // exactly one submesh per material. A mesh with 6 submeshes but a single
            // material index makes Unity (2019.x / KSP 1.12) render only submesh 0,
            // which shows up as "only one face visible, the rest see-through".
            List<int> tris = new List<int>();
            for (int f = 0; f < 6; f++)
            {
                for (int v = 0; v < 4; v++)
                {
                    m.verts.Add(new float[] { facePos[f, v * 3], facePos[f, v * 3 + 1], facePos[f, v * 3 + 2] });
                    m.normals.Add(new float[] { faceNormal[f, 0], faceNormal[f, 1], faceNormal[f, 2] });
                    float uu = (v == 1 || v == 2) ? 1f : 0f;
                    float vv = (v == 2 || v == 3) ? 1f : 0f;
                    m.uvs.Add(new float[] { uu, vv });
                }
                int b = f * 4;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            m.submeshes.Add(tris.ToArray());
            m.numVerts = 24;
            m.numSubmeshes = 1;
            return m;
        }

        static void WriteMesh(BinaryWriter w, MeshData m)
        {
            WInt(w, 13);                 // ET_MESH_START
            WInt(w, m.numVerts);
            WInt(w, m.numSubmeshes);
            WInt(w, 14); foreach (float[] v in m.verts) { WFloat(w, v[0]); WFloat(w, v[1]); WFloat(w, v[2]); }
            WInt(w, 15); foreach (float[] uv in m.uvs) { WFloat(w, uv[0]); WFloat(w, uv[1]); }
            WInt(w, 17); foreach (float[] n in m.normals) { WFloat(w, n[0]); WFloat(w, n[1]); WFloat(w, n[2]); }
            foreach (int[] sub in m.submeshes)
            {
                WInt(w, 19);
                WInt(w, sub.Length);
                foreach (int i in sub) WInt(w, i);
            }
            WInt(w, 22);                 // ET_MESH_END
        }

        // ------------------------------------------------------------------ shape meshes

        // Mesh builder for the KK block shapes. Winding convention (Unity LHS, same
        // as BuildCube above and the stock .mu meshes): a face is visible from
        // outside when its vertices are ordered so that cross(v1-v0, v2-v0) points
        // outward. Triangles are CW seen from outside.
        class MeshBuilder
        {
            public List<float[]> verts = new List<float[]>();
            public List<float[]> uvs = new List<float[]>();
            public List<float[]> normals = new List<float[]>();
            public List<int> tris = new List<int>();

            // vertex with placeholder normal, filled in by Tri()
            public int V(float x, float y, float z, float u, float v)
            {
                verts.Add(new float[] { x, y, z });
                uvs.Add(new float[] { u, v });
                normals.Add(new float[] { 0, 0, 0 });
                return verts.Count - 1;
            }

            // vertex with explicit normal (smooth shading); use together with TriKeep()
            public int VN(float x, float y, float z, float u, float v, float nx, float ny, float nz)
            {
                int idx = V(x, y, z, u, v);
                normals[idx] = new float[] { nx, ny, nz };
                return idx;
            }

            // flat-shaded triangle: normal auto-computed from cross(v1-v0, v2-v0).
            // degenerate (zero area) triangles are skipped.
            public void Tri(int a, int b, int c)
            {
                float[] va = verts[a], vb = verts[b], vc = verts[c];
                float ux = vb[0] - va[0], uy = vb[1] - va[1], uz = vb[2] - va[2];
                float vx = vc[0] - va[0], vy = vc[1] - va[1], vz = vc[2] - va[2];
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                if (nx == 0 && ny == 0 && nz == 0) return;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                normals[a] = new float[] { nx / len, ny / len, nz / len };
                normals[b] = normals[a];
                normals[c] = normals[a];
                tris.Add(a); tris.Add(b); tris.Add(c);
            }

            // like Tri(), but keeps pre-set normals (for smooth shaded meshes)
            public void TriKeep(int a, int b, int c)
            {
                float[] va = verts[a], vb = verts[b], vc = verts[c];
                float ux = vb[0] - va[0], uy = vb[1] - va[1], uz = vb[2] - va[2];
                float vx = vc[0] - va[0], vy = vc[1] - va[1], vz = vc[2] - va[2];
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                if (nx == 0 && ny == 0 && nz == 0) return;
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
        }

        // packs a builder into the MeshData that WriteMesh() serializes
        static MeshData FinishBuilder(MeshBuilder mb)
        {
            MeshData m = new MeshData();
            m.numVerts = mb.verts.Count;
            m.numSubmeshes = 1;
            m.verts = mb.verts; m.uvs = mb.uvs; m.normals = mb.normals;
            m.submeshes.Add(mb.tris.ToArray());
            return m;
        }

        // generation-time safety net: all of these shapes are convex, so every
        // triangle normal must point away from the inside. The centroid of the
        // vertices can lie exactly ON a face (wedge!), so the test is relative:
        // flag a triangle only when its normal deviates clearly (> ~91 deg) from
        // the outward direction (v0 - centroid).
        static void CheckConvexOutward(MeshBuilder mb, string name)
        {
            int n = mb.verts.Count;
            if (n == 0) return;
            double cx = 0, cy = 0, cz = 0;
            foreach (float[] v in mb.verts) { cx += v[0]; cy += v[1]; cz += v[2]; }
            cx /= n; cy /= n; cz /= n;
            for (int i = 0; i + 2 < mb.tris.Count; i += 3)
            {
                float[] a = mb.verts[mb.tris[i]], b = mb.verts[mb.tris[i + 1]], c = mb.verts[mb.tris[i + 2]];
                float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2];
                float vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
                float gx = uy * vz - uz * vy, gy = uz * vx - ux * vz, gz = ux * vy - uy * vx;
                float px = a[0] - (float)cx, py = a[1] - (float)cy, pz = a[2] - (float)cz;
                float cmag = (float)Math.Sqrt(gx * gx + gy * gy + gz * gz);
                float pmag = (float)Math.Sqrt(px * px + py * py + pz * pz);
                if (cmag <= 0 || pmag <= 0) continue;
                float rel = (gx * px + gy * py + gz * pz) / (cmag * pmag);
                if (rel < -0.02f)
                    Console.WriteLine("WARNING " + name + ": triangle " + (i / 3) + " points inward (rel=" + rel.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ")");
            }
        }

        // one vertex on a sphere surface around center (0, cy, 0), radius r.
        // smooth normal = radial direction (pos - center). u = i/lon, v = j/lat.
        static int SphVert(MeshBuilder mb, float r, double cy, double phi, double theta, float u, float v)
        {
            double cp = Math.Cos(phi), sp = Math.Sin(phi);
            float rad = (float)(r * cp);
            if (Math.Abs(rad) < 1e-6f) rad = 0f; // exact pole (kills cos(+/-90 deg) float noise)
            float x = (float)(rad * Math.Cos(theta));
            float z = (float)(rad * Math.Sin(theta));
            float y = (float)(cy + r * sp);
            return mb.VN(x, y, z, u, v, x / r, (float)sp, z / r);
        }

        // connects a lower ring (lo) with the upper ring (hi), both int[lon]:
        // quad a=(lo,i) b=(lo,i+1) c=(hi,i+1) d=(hi,i) -> triangles (a,c,b) + (a,d,c).
        // At exact poles half of the triangles degenerate and are skipped by TriKeep.
        static void SphereBands(MeshBuilder mb, int[] lo, int[] hi)
        {
            int lon = lo.Length;
            for (int i = 0; i < lon; i++)
            {
                int i1 = (i + 1) % lon;
                mb.TriKeep(lo[i], hi[i1], lo[i1]);
                mb.TriKeep(lo[i], hi[i], hi[i1]);
            }
        }

        // ramp (45 degree wedge): origin at bottom center. Vertical back wall at
        // z = -h, slope from the top back edge down to the front bottom edge
        // (z = +h) - the driveable slope faces +z. Convex, 18 verts / 8 tris.
        static MeshBuilder BuildWedge(float s)
        {
            MeshBuilder mb = new MeshBuilder();
            float h = s / 2f;

            // slope quad, n = (0, 1, 1)/sqrt(2)
            int a = mb.V(-h, s, -h, 0, 0);
            int b = mb.V(-h, 0, h, 0, 1);
            int c = mb.V(h, 0, h, 1, 1);
            int d = mb.V(h, s, -h, 1, 0);
            mb.Tri(a, b, c); mb.Tri(a, c, d);

            // back wall z = -h, n = (0, 0, -1)
            a = mb.V(h, 0, -h, 0, 0);
            b = mb.V(-h, 0, -h, 1, 0);
            c = mb.V(-h, s, -h, 1, 1);
            d = mb.V(h, s, -h, 0, 1);
            mb.Tri(a, b, c); mb.Tri(a, c, d);

            // bottom y = 0, n = (0, -1, 0)
            a = mb.V(-h, 0, -h, 0, 0);
            b = mb.V(h, 0, -h, 1, 0);
            c = mb.V(h, 0, h, 1, 1);
            d = mb.V(-h, 0, h, 0, 1);
            mb.Tri(a, b, c); mb.Tri(a, c, d);

            // left side x = -h, triangle, n = (-1, 0, 0)
            mb.Tri(mb.V(-h, 0, -h, 0, 0), mb.V(-h, 0, h, 1, 0), mb.V(-h, s, -h, 0, 1));

            // right side x = +h, triangle, n = (1, 0, 0)
            mb.Tri(mb.V(h, 0, -h, 0, 0), mb.V(h, s, -h, 0, 1), mb.V(h, 0, h, 1, 0));

            return mb;
        }

        // pyramid: square base s x s at y = 0, apex at (0, s, 0), flat shaded
        static MeshBuilder BuildPyramid(float s)
        {
            MeshBuilder mb = new MeshBuilder();
            float h = s / 2f;

            // base y = 0, n = (0, -1, 0)
            int a = mb.V(-h, 0, -h, 0, 0);
            int b = mb.V(h, 0, -h, 1, 0);
            int c = mb.V(h, 0, h, 1, 1);
            int d = mb.V(-h, 0, h, 0, 1);
            mb.Tri(a, b, c); mb.Tri(a, c, d);

            // four side triangles (unique verts each -> crisp flat shading)
            mb.Tri(mb.V(-h, 0, h, 0, 0), mb.V(h, 0, h, 1, 0), mb.V(0, s, 0, 0.5f, 1));    // front  n=+z
            mb.Tri(mb.V(h, 0, h, 0, 0), mb.V(h, 0, -h, 1, 0), mb.V(0, s, 0, 0.5f, 1));    // right  n=+x
            mb.Tri(mb.V(h, 0, -h, 0, 0), mb.V(-h, 0, -h, 1, 0), mb.V(0, s, 0, 0.5f, 1));  // back   n=-z
            mb.Tri(mb.V(-h, 0, -h, 0, 0), mb.V(-h, 0, h, 1, 0), mb.V(0, s, 0, 0.5f, 1));  // left   n=-x

            return mb;
        }

        // full sphere: radius s/2, center (0, s/2, 0), 16 x 8 segments, smooth shading
        static MeshBuilder BuildSphere(float s)
        {
            const int lon = 16, lat = 8;
            MeshBuilder mb = new MeshBuilder();
            float r = s / 2f;
            int[][] rings = new int[lat + 1][];
            for (int j = 0; j <= lat; j++)
            {
                double phi = -Math.PI / 2 + Math.PI * j / lat;
                rings[j] = new int[lon];
                for (int i = 0; i < lon; i++)
                    rings[j][i] = SphVert(mb, r, r, phi, 2.0 * Math.PI * i / lon, (float)i / lon, (float)j / lat);
            }
            for (int j = 0; j < lat; j++) SphereBands(mb, rings[j], rings[j + 1]);
            return mb;
        }

        // cylinder: radius s/2, height s (y = 0 .. s), origin bottom center,
        // 16 segments, smooth side walls + exact flat caps
        static MeshBuilder BuildCylinder(float s)
        {
            const int lon = 16;
            MeshBuilder mb = new MeshBuilder();
            float r = s / 2f;

            // side: two rings with radial normals
            int[] bot = new int[lon], top = new int[lon];
            for (int i = 0; i < lon; i++)
            {
                double t = 2.0 * Math.PI * i / lon;
                float x = (float)(r * Math.Cos(t)), z = (float)(r * Math.Sin(t));
                bot[i] = mb.VN(x, 0, z, (float)i / lon, 0, x / r, 0, z / r);
                top[i] = mb.VN(x, s, z, (float)i / lon, 1, x / r, 0, z / r);
            }
            SphereBands(mb, bot, top);

            // flat caps: separate rim/center verts with vertical normals (no shading
            // bleed between side wall and caps)
            int[] br = new int[lon], tr = new int[lon];
            for (int i = 0; i < lon; i++)
            {
                double t = 2.0 * Math.PI * i / lon;
                float x = (float)(r * Math.Cos(t)), z = (float)(r * Math.Sin(t));
                float uu = 0.5f + 0.5f * (float)Math.Cos(t), vv = 0.5f + 0.5f * (float)Math.Sin(t);
                br[i] = mb.VN(x, 0, z, uu, vv, 0, -1, 0);
                tr[i] = mb.VN(x, s, z, uu, vv, 0, 1, 0);
            }
            int bc = mb.VN(0, 0, 0, 0.5f, 0.5f, 0, -1, 0);
            int tc = mb.VN(0, s, 0, 0.5f, 0.5f, 0, 1, 0);
            for (int i = 0; i < lon; i++)
            {
                int i1 = (i + 1) % lon;
                mb.TriKeep(tc, tr[i1], tr[i]); // top cap, n = (0, 1, 0)
                mb.TriKeep(bc, br[i], br[i1]); // bottom cap, n = (0, -1, 0)
            }
            return mb;
        }

        // dome (hemisphere): radius s/2 sitting on y = 0, 16 x 4 segments, smooth,
        // plus an exact flat base disk facing down
        static MeshBuilder BuildDome(float s)
        {
            const int lon = 16, lat = 4;
            MeshBuilder mb = new MeshBuilder();
            float r = s / 2f;

            int[][] rings = new int[lat + 1][];
            for (int j = 0; j <= lat; j++)
            {
                double phi = Math.PI / 2 * j / lat;
                rings[j] = new int[lon];
                for (int i = 0; i < lon; i++)
                    rings[j][i] = SphVert(mb, r, 0.0, phi, 2.0 * Math.PI * i / lon, (float)i / lon, (float)j / lat);
            }
            for (int j = 0; j < lat; j++) SphereBands(mb, rings[j], rings[j + 1]);

            // base disk y = 0, n = (0, -1, 0): fan from center
            int bc = mb.VN(0, 0, 0, 0.5f, 0.5f, 0, -1, 0);
            int[] br = new int[lon];
            for (int i = 0; i < lon; i++)
            {
                double t = 2.0 * Math.PI * i / lon;
                br[i] = mb.VN((float)(r * Math.Cos(t)), 0, (float)(r * Math.Sin(t)),
                    0.5f + 0.5f * (float)Math.Cos(t), 0.5f + 0.5f * (float)Math.Sin(t), 0, -1, 0);
            }
            for (int i = 0; i < lon; i++) mb.TriKeep(bc, br[i], br[(i + 1) % lon]);
            return mb;
        }

        static void WriteBlockMu(string path, float size)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                WInt(w, MODEL_BINARY);
                WInt(w, FILE_VERSION);
                WString(w, Path.GetFileNameWithoutExtension(path));

                // root transform
                WString(w, Path.GetFileNameWithoutExtension(path));
                WVec(w, 0, 0, 0); WQuat(w, 0, 0, 0, 1); WVec(w, 1, 1, 1);
                WInt(w, 24); WString(w, "Untagged"); WInt(w, 0);

                // child "Block": transform + tag + boxcollider + mesh + renderer
                WInt(w, 0); // ET_CHILD_TRANSFORM_START
                WString(w, "Block");
                WVec(w, 0, 0, 0); WQuat(w, 0, 0, 0, 1); WVec(w, 1, 1, 1);
                WInt(w, 24); WString(w, "Untagged"); WInt(w, 0);
                WInt(w, 28); WByte(w, 0); WVec(w, size, size, size); WVec(w, 0, size / 2f, 0); // ET_BOX_COLLIDER2, not trigger
                WInt(w, 7); WriteMesh(w, BuildCube(size));                                     // ET_MESH_FILTER
                WInt(w, 8); WByte(w, 1); WByte(w, 1); WInt(w, 1); WInt(w, 0);                  // ET_MESH_RENDERER
                WInt(w, 1); // ET_CHILD_TRANSFORM_END

                WriteMatTex(w);
            }
        }

        // shape static: same file layout as the cube, but the box collider is
        // replaced by a convex mesh collider (ramp/pyramid/cylinder/dome) or a
        // sphere collider (ball), and the mesh comes from a MeshBuilder.
        static void WriteShapeMu(string path, float size, MeshData mesh, bool sphereCollider)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                WInt(w, MODEL_BINARY);
                WInt(w, FILE_VERSION);
                WString(w, Path.GetFileNameWithoutExtension(path));

                // root transform
                WString(w, Path.GetFileNameWithoutExtension(path));
                WVec(w, 0, 0, 0); WQuat(w, 0, 0, 0, 1); WVec(w, 1, 1, 1);
                WInt(w, 24); WString(w, "Untagged"); WInt(w, 0);

                // child "Block": transform + tag + collider + mesh + renderer
                WInt(w, 0); // ET_CHILD_TRANSFORM_START
                WString(w, "Block");
                WVec(w, 0, 0, 0); WQuat(w, 0, 0, 0, 1); WVec(w, 1, 1, 1);
                WInt(w, 24); WString(w, "Untagged"); WInt(w, 0);
                if (sphereCollider)
                {
                    WInt(w, 26); WByte(w, 0);                       // ET_SPHERE_COLLIDER2, not trigger
                    WFloat(w, size / 2f); WVec(w, 0, size / 2f, 0); // radius = s/2, center (0, s/2, 0)
                }
                else
                {
                    WInt(w, 25); WByte(w, 0); WByte(w, 1);          // ET_MESH_COLLIDER2, not trigger, convex
                    WriteMesh(w, mesh);
                }
                WInt(w, 7); WriteMesh(w, mesh);                                     // ET_MESH_FILTER
                WInt(w, 8); WByte(w, 1); WByte(w, 1); WInt(w, 1); WInt(w, 0);       // ET_MESH_RENDERER
                WInt(w, 1); // ET_CHILD_TRANSFORM_END

                WriteMatTex(w);
            }
        }

        // material + texture section, identical for cubes and shapes
        static void WriteMatTex(BinaryWriter w)
        {
            // materials
            WInt(w, 10);
            WInt(w, 1);
            WString(w, "KKBlockMat");
            WString(w, "KSP/Diffuse");
            WInt(w, 8);
            WString(w, "_MainTex"); WInt(w, 4); WInt(w, 0); WFloat(w, 1); WFloat(w, 1); WFloat(w, 0); WFloat(w, 0);
            WString(w, "_Color"); WInt(w, 0); WFloat(w, 1); WFloat(w, 1); WFloat(w, 1); WFloat(w, 1);
            WString(w, "_Opacity"); WInt(w, 3); WFloat(w, 1);
            WString(w, "_RimFalloff"); WInt(w, 3); WFloat(w, 0);
            // NOTE: values mirror Squad stock materials (aerodynamicNoseCone.mu, KSP/Bumped Specular):
            // _TemperatureColor MUST be (0,0,0,0) - stock value. The KKflagDemo demo value
            // (0.63,0.35,0.15) is orange-brown and gets applied by the Deferred rendering mod
            // (PBR shader conversion), making the whole object look orange.
            WString(w, "_RimColor"); WInt(w, 0); WFloat(w, 0); WFloat(w, 0); WFloat(w, 0); WFloat(w, 1);
            WString(w, "_TemperatureColor"); WInt(w, 0); WFloat(w, 0); WFloat(w, 0); WFloat(w, 0); WFloat(w, 0);
            WString(w, "_BurnColor"); WInt(w, 0); WFloat(w, 1); WFloat(w, 1); WFloat(w, 1); WFloat(w, 1);
            WString(w, "_UnderwaterFogFactor"); WInt(w, 3); WFloat(w, 0);

            // textures
            WInt(w, 12);
            WInt(w, 1);
            WString(w, "KKBlockTex.png");
            WInt(w, 0); // TT_TEXTURE
        }

        static void WriteTexture(string path)
        {
            const int size = 128;
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // plain light gray, no border: variant color x texture = clean faces,
                    // no frame artifacts from DXT compression / mipmapping at the block edges
                    g.Clear(Color.FromArgb(255, 245, 245, 245));
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }

        static string FileSuffix(float s)
        {
            if (s < 1f) return ((int)Math.Round(s * 10)).ToString("00");
            return ((int)Math.Round(s)).ToString();
        }

        // generates one shape file: name_<suffix>.mu with the given collider type
        static void GenShape(string outDir, string name, float s, MeshBuilder mb, bool sphereCollider)
        {
            string id = name + "_" + FileSuffix(s);
            CheckConvexOutward(mb, id);
            MeshData mesh = FinishBuilder(mb);
            string p = Path.Combine(outDir, id + ".mu");
            WriteShapeMu(p, s, mesh, sphereCollider);
            Console.WriteLine("wrote " + p + " (" + F(s) + " m, " + mesh.numVerts + " verts, " + (mesh.submeshes[0].Length / 3) + " tris, " + (sphereCollider ? "sphere collider" : "convex mesh collider") + ")");
        }

        static int Gen(string outDir, string sizeList)
        {
            try
            {
                Directory.CreateDirectory(outDir);
                List<float> sizes = new List<float>();
                foreach (string part in sizeList.Split(','))
                {
                    float s = float.Parse(part.Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    if (s <= 0) throw new Exception("invalid size " + part);
                    sizes.Add(s);
                }
                string texPath = Path.Combine(outDir, "KKBlockTex.png");
                WriteTexture(texPath);
                Console.WriteLine("wrote " + texPath);
                foreach (float s in sizes)
                {
                    string p = Path.Combine(outDir, "KKBlock_" + FileSuffix(s) + ".mu");
                    WriteBlockMu(p, s);
                    Console.WriteLine("wrote " + p + " (" + F(s) + " m, box collider)");

                    GenShape(outDir, "KKBlockRamp", s, BuildWedge(s), false);
                    GenShape(outDir, "KKBlockPyramid", s, BuildPyramid(s), false);
                    GenShape(outDir, "KKBlockSphere", s, BuildSphere(s), true);
                    GenShape(outDir, "KKBlockCylinder", s, BuildCylinder(s), false);
                    GenShape(outDir, "KKBlockDome", s, BuildDome(s), false);
                }
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
                return 1;
            }
        }

        // ------------------------------------------------------------------ main

        static int Main(string[] args)
        {
            if (args.Length == 0) { Usage(); return 2; }
            switch (args[0])
            {
                case "dump":
                    {
                        int rc = 0;
                        for (int i = 1; i < args.Length; i++) if (Dump(args[i], false) != 0) rc = 1;
                        return rc;
                    }
                case "check":
                    {
                        int rc = 0;
                        for (int i = 1; i < args.Length; i++) if (Dump(args[i], true) != 0) rc = 1;
                        return rc;
                    }
                case "colliders":
                    if (args.Length < 2) { Usage(); return 2; }
                    return Scan(args[1]);
                case "gen":
                    if (args.Length < 3) { Usage(); return 2; }
                    return Gen(args[1], args[2]);
                default:
                    Usage();
                    return 2;
            }
        }

        static void Usage()
        {
            Console.WriteLine("MuTool - .mu dump + KK block/shape generator");
            Console.WriteLine("  MuTool.exe dump <file.mu> ...");
            Console.WriteLine("  MuTool.exe check <file.mu> ...");
            Console.WriteLine("  MuTool.exe colliders <dir>    - list collider types used by all .mu below dir");
            Console.WriteLine("  MuTool.exe gen <outdir> <0.5,1,2,4,8,...>");
            Console.WriteLine("    gen writes KKBlock_<n>.mu (cube) + KKBlockRamp/PYRAMID/Sphere/Cylinder/Dome_<n>.mu");
        }
    }
}
