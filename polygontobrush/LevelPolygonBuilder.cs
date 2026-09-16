using System;
using System.Collections.Generic;
using Weland;

static class LevelPolygonBuilder
{
    static List<Point> loopPoints = new List<Point>();
    static List<Point> simplifiedPoints = new List<Point>();
    static List<Point> newEndpoints = new List<Point>();
    static List<Line> newLines = new List<Line>();

    private static int RoundToNearestEight(double value)
    {
        return (int)Math.Round(value / 8.0, MidpointRounding.AwayFromZero) * 8;
    }

    public static Level BuildLevelWithSinglePolygon(Level src, int polyIndex)
    {
        Polygon srcPoly = src.Polygons[polyIndex];

        loopPoints.Clear();

        for (int i = 0; i < srcPoly.VertexCount; i++)
        {
            int oldEpIndex = srcPoly.EndpointIndexes[i];
            Point originalPt = src.Endpoints[oldEpIndex];

            loopPoints.Add(originalPt);
        }

        simplifiedPoints.Clear();

        for (int i = 0; i < loopPoints.Count; i++)
        {
            Point prev = loopPoints[(i - 1 + loopPoints.Count) % loopPoints.Count];
            Point curr = loopPoints[i];
            Point next = loopPoints[(i + 1) % loopPoints.Count];

            int crossProduct = (curr.Y - prev.Y) * (next.X - curr.X) - (curr.X - prev.X) * (next.Y - curr.Y);

            if (crossProduct != 0)
            {
                simplifiedPoints.Add(curr);
            }
        }

        if (simplifiedPoints.Count < 3)
        {
            simplifiedPoints = loopPoints;
        }

        int sumX = 0;
        int sumY = 0;
        foreach (Point pt in simplifiedPoints)
        {
            sumX += pt.X;
            sumY += pt.Y;
        }

        int newCenterX = RoundToNearestEight((double)sumX / simplifiedPoints.Count);
        int newCenterY = RoundToNearestEight((double)sumY / simplifiedPoints.Count);

        newEndpoints.Clear();

        foreach (Point pt in simplifiedPoints)
        {
            int centeredX = pt.X - newCenterX;
            int centeredY = pt.Y - newCenterY;

            if (Math.Abs(centeredX) < 8) centeredX = (centeredX >= 0) ? 8 : -8;
            if (Math.Abs(centeredY) < 8) centeredY = (centeredY >= 0) ? 8 : -8;

            Point perfectlyCenteredPt = new Point
            {
                X = (short)centeredX,
                Y = (short)centeredY
            };
            newEndpoints.Add(perfectlyCenteredPt);
        }

        newLines.Clear();

        int newVertexCount = newEndpoints.Count;

        for (int i = 0; i < newVertexCount; i++)
        {
            int p1Index = i;
            int p2Index = (i + 1) % newVertexCount;

            Line newLine = new Line();
            newLine.EndpointIndexes[0] = (short)p1Index;
            newLine.EndpointIndexes[1] = (short)p2Index;

            newLine.ClockwisePolygonOwner = 0;
            newLine.CounterclockwisePolygonOwner = -1;

            newLines.Add(newLine);
        }

        int rawFloor = RoundToNearestEight(srcPoly.FloorHeight);
        int rawCeiling = RoundToNearestEight(srcPoly.CeilingHeight);
        int rawCenter = (rawFloor + rawCeiling) / 2;
        int verticalCenter = RoundToNearestEight(rawCenter);

        Polygon newPoly = new Polygon();
        newPoly.Type = 0;
        newPoly.Flags = 0;
        newPoly.VertexCount = (ushort)newVertexCount;
        newPoly.FloorHeight = (short)(rawFloor - verticalCenter);
        newPoly.CeilingHeight = (short)(rawCeiling - verticalCenter);
        newPoly.FloorTexture = ShapeDescriptor.Empty;
        newPoly.CeilingTexture = ShapeDescriptor.Empty;

        for (int i = 0; i < Polygon.MaxVertexCount; i++)
        {
            if (i < newVertexCount)
            {
                newPoly.LineIndexes[i] = (short)i;
                newPoly.EndpointIndexes[i] = (short)i;
            }
            else
            {
                newPoly.LineIndexes[i] = -1;
                newPoly.EndpointIndexes[i] = -1;
            }
            newPoly.AdjacentPolygonIndexes[i] = -1;
        }

        Level newLevel = new Level();
        newLevel.Endpoints = newEndpoints;
        newLevel.Lines = newLines;
        newLevel.Polygons.Add(newPoly);

        MapObject player = new MapObject();
        player.Type = ObjectType.Player;
        player.X = 0;
        player.Y = 0;
        player.PolygonIndex = 0;
        newLevel.Objects.Add(player);

        newLevel.Name = src.Name + $" (Poly {polyIndex} Simplified)";
        newLevel.Environment = src.Environment;
        newLevel.Landscape = src.Landscape;

        return newLevel;
    }
}
