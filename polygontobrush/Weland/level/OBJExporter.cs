/*
 * This file is a modified version of OBJExporter.cs from Weland.
 * Modifications made by huriettic on 2026-09-12.
 * Original code licensed under the GPL.
 */

using System;
using System.IO;
using System.Collections.Generic;

namespace Weland
{
    public class OBJExporter
    {
        const double Scale = 128.0;

        struct Vector3
        {
            public double X, Y, Z;
            public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }

            public static Vector3 Cross(Vector3 b, Vector3 c)
            {
                return new Vector3(b.Y * c.Z - b.Z * c.Y, b.Z * c.X - b.X * c.Z, b.X * c.Y - b.Y * c.X);
            }

            public Vector3 Normalize()
            {
                double len = Math.Sqrt(X * X + Y * Y + Z * Z);
                if (len == 0) return new Vector3(0, 0, 0);
                return new Vector3(X / len, Y / len, Z / len);
            }
        }

        struct Vertex
        {
            public short X;
            public short Y;
            public short Z;

            public void Write(TextWriter w)
            {
                w.WriteLine("v {0} {1} {2}", World.ToDouble(X) * Scale, World.ToDouble(Z) * Scale, World.ToDouble(Y) * Scale);
            }
        }

        Level level;
        List<Vertex> vertices = new List<Vertex>();
        List<Vector3> normals = new List<Vector3>();
        List<Dictionary<short, int>> endpointVertices = new List<Dictionary<short, int>>();

        List<(int[] vertexIndices, int normalIndex)> faces = new List<(int[], int)>();

        public OBJExporter(Level level)
        {
            this.level = level;
        }

        public void Export(string path)
        {
            faces.Clear();
            vertices.Clear();
            normals.Clear();
            endpointVertices.Clear();

            for (int i = 0; i < level.Endpoints.Count; ++i)
            {
                endpointVertices.Add(new Dictionary<short, int>());
            }

            foreach (Polygon p in level.Polygons)
            {
                if (p.CeilingHeight > p.FloorHeight)
                {
                    if (p.FloorTransferMode != 9)
                    {
                        int[] f = FloorFace(p);
                        faces.Add((f, GetNormalIndex(f)));
                    }
                    if (p.CeilingTransferMode != 9)
                    {
                        int[] c = CeilingFace(p);
                        faces.Add((c, GetNormalIndex(c)));
                    }
                    for (int i = 0; i < p.VertexCount; ++i)
                    {
                        InsertLineFaces(level.Lines[p.LineIndexes[i]], p);
                    }
                }
            }

            using (TextWriter w = new StreamWriter(path))
            {
                foreach (Vertex v in vertices)
                {
                    v.Write(w);
                }

                foreach (Vector3 n in normals)
                {
                    w.WriteLine("vn {0:F6} {1:F6} {2:F6}", n.X, n.Y, n.Z);
                }

                foreach (var face in faces)
                {
                    w.Write("f");
                    for (int i = 0; i < face.vertexIndices.Length; ++i)
                    {
                        int vf = face.vertexIndices[i] + 1;
                        int nf = face.normalIndex + 1;
                        w.Write($" {vf}//{nf}");
                    }
                    w.WriteLine();
                }
            }
        }

        int GetVertexIndex(int endpointIndex, short height)
        {
            if (!endpointVertices[endpointIndex].ContainsKey(height))
            {
                Point p = level.Endpoints[endpointIndex];
                Vertex v = new Vertex();
                v.X = p.X;
                v.Y = p.Y;
                v.Z = height;
                endpointVertices[endpointIndex][height] = vertices.Count;
                vertices.Add(v);
            }
            return endpointVertices[endpointIndex][height];
        }

        int GetNormalIndex(int[] faceVertices)
        {
            if (faceVertices.Length < 3) return -1;

            Vertex v0 = vertices[faceVertices[0]];
            Vertex v1 = vertices[faceVertices[1]];
            Vertex v2 = vertices[faceVertices[2]];

            Vector3 p0 = new Vector3(World.ToDouble(v0.X) * Scale, World.ToDouble(v0.Z) * Scale, World.ToDouble(v0.Y) * Scale);
            Vector3 p1 = new Vector3(World.ToDouble(v1.X) * Scale, World.ToDouble(v1.Z) * Scale, World.ToDouble(v1.Y) * Scale);
            Vector3 p2 = new Vector3(World.ToDouble(v2.X) * Scale, World.ToDouble(v2.Z) * Scale, World.ToDouble(v2.Y) * Scale);

            Vector3 edge1 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 edge2 = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);

            Vector3 normal = Vector3.Cross(edge1, edge2).Normalize();
            normals.Add(normal);

            return normals.Count - 1;
        }

        int[] FloorFace(Polygon p)
        {
            int[] result = new int[p.VertexCount];
            for (int i = 0; i < p.VertexCount; ++i)
            {
                result[i] = GetVertexIndex(p.EndpointIndexes[i], p.FloorHeight);
            }
            return result;
        }

        int[] CeilingFace(Polygon p)
        {
            int[] result = new int[p.VertexCount];
            for (int i = 0; i < p.VertexCount; ++i)
            {
                result[i] = GetVertexIndex(p.EndpointIndexes[p.VertexCount - 1 - i], p.CeilingHeight);
            }
            return result;
        }

        int[] BuildFace(int left, int right, short ceiling, short floor)
        {
            int[] result = new int[4];

            result[0] = GetVertexIndex(left, floor);
            result[1] = GetVertexIndex(left, ceiling);
            result[2] = GetVertexIndex(right, ceiling);
            result[3] = GetVertexIndex(right, floor);

            return result;
        }

        void InsertLineFaces(Line line, Polygon p)
        {
            int left;
            int right;
            Polygon? opposite = null;
            Side? side = null;
            if (line.ClockwisePolygonOwner != -1 && level.Polygons[line.ClockwisePolygonOwner] == p)
            {
                left = line.EndpointIndexes[0];
                right = line.EndpointIndexes[1];
                if (line.CounterclockwisePolygonOwner != -1)
                {
                    opposite = level.Polygons[line.CounterclockwisePolygonOwner];
                }
                if (line.ClockwisePolygonSideIndex != -1)
                {
                    side = level.Sides[line.ClockwisePolygonSideIndex];
                }
            }
            else
            {
                left = line.EndpointIndexes[1];
                right = line.EndpointIndexes[0];
                if (line.ClockwisePolygonOwner != -1)
                {
                    opposite = level.Polygons[line.ClockwisePolygonOwner];
                }
                if (line.CounterclockwisePolygonSideIndex != -1)
                {
                    side = level.Sides[line.CounterclockwisePolygonSideIndex];
                }
            }

            bool landscapeTop = false;
            bool landscapeBottom = false;
            if (side != null)
            {
                if (side.Type == SideType.Low)
                {
                    if (side.PrimaryTransferMode == 9)
                    {
                        landscapeBottom = true;
                    }
                }
                else
                {
                    if (side.PrimaryTransferMode == 9)
                    {
                        landscapeTop = true;
                    }
                    if (side.SecondaryTransferMode == 9)
                    {
                        landscapeBottom = true;
                    }
                }
            }

            if (opposite == null || (opposite.FloorHeight > p.CeilingHeight || opposite.CeilingHeight < p.FloorHeight))
            {
                if (!landscapeTop)
                {
                    int[] f = BuildFace(left, right, p.CeilingHeight, p.FloorHeight);
                    faces.Add((f, GetNormalIndex(f)));
                }
            }
            else
            {
                if (opposite.FloorHeight > p.FloorHeight)
                {
                    if (!landscapeBottom)
                    {
                        int[] f = BuildFace(left, right, opposite.FloorHeight, p.FloorHeight);
                        faces.Add((f, GetNormalIndex(f)));
                    }
                }
                if (opposite.CeilingHeight < p.CeilingHeight)
                {
                    if (!landscapeTop)
                    {
                        int[] f = BuildFace(left, right, p.CeilingHeight, opposite.CeilingHeight);
                        faces.Add((f, GetNormalIndex(f)));
                    }
                }
            }
        }
    }
}
