using System;
using System.Collections.Generic;
using System.Linq;

namespace Worksheet.Core.Models.Gates
{
    public readonly record struct Point01(double X01, double Y01);

    public sealed class GateGeometry
    {
        private GateGeometry(
            GateType type,
            double xMin01,
            double xMax01,
            double yMin01,
            double yMax01,
            double centerX01,
            double centerY01,
            double radiusX01,
            double radiusY01,
            double angleDeg,
            IReadOnlyList<Point01>? polygonPoints01)
        {
            Type = type;

            XMin01 = xMin01;
            XMax01 = xMax01;
            YMin01 = yMin01;
            YMax01 = yMax01;

            CenterX01 = centerX01;
            CenterY01 = centerY01;
            RadiusX01 = radiusX01;
            RadiusY01 = radiusY01;
            AngleDeg = angleDeg;

            PolygonPoints01 = polygonPoints01;
        }

        public GateType Type { get; }

        // Rectangle (normalized)
        public double XMin01 { get; }
        public double XMax01 { get; }
        public double YMin01 { get; }
        public double YMax01 { get; }

        // Ellipse (normalized)
        public double CenterX01 { get; }
        public double CenterY01 { get; }
        public double RadiusX01 { get; }
        public double RadiusY01 { get; }
        public double AngleDeg { get; }

        // Polygon (normalized)
        public IReadOnlyList<Point01>? PolygonPoints01 { get; }

        public static GateGeometry Rectangle01(double xMin01, double xMax01, double yMin01, double yMax01)
        {
            NormalizeRect01(ref xMin01, ref xMax01, ref yMin01, ref yMax01);
            return new GateGeometry(GateType.Rectangle, xMin01, xMax01, yMin01, yMax01, 0, 0, 0, 0, 0, null);
        }

        public static GateGeometry Ellipse01(double centerX01, double centerY01, double radiusX01, double radiusY01, double angleDeg = 0)
        {
            return new GateGeometry(GateType.Ellipse, 0, 0, 0, 0, centerX01, centerY01, radiusX01, radiusY01, angleDeg, null);
        }

        public static GateGeometry Polygon01(IReadOnlyList<Point01> points01)
        {
            var pts = points01?.Where(p => double.IsFinite(p.X01) && double.IsFinite(p.Y01)).ToArray() ?? Array.Empty<Point01>();
            return new GateGeometry(GateType.Polygon, 0, 0, 0, 0, 0, 0, 0, 0, 0, pts);
        }

        public static GateGeometry FromBinRectangle(double xMinBin, double xMaxBin, double yMinBin, double yMaxBin, int binCount)
        {
            if (binCount <= 0)
                binCount = 1;

            double inv = 1.0 / binCount;
            return Rectangle01(
                xMinBin * inv,
                xMaxBin * inv,
                yMinBin * inv,
                yMaxBin * inv);
        }

        public GateBinGeometry ToBinGeometry(int binCount)
        {
            if (binCount <= 0)
                binCount = 1;

            switch (Type)
            {
                case GateType.Rectangle:
                {
                    double xMin = Clamp01(XMin01) * binCount;
                    double xMax = Clamp01(XMax01) * binCount;
                    double yMin = Clamp01(YMin01) * binCount;
                    double yMax = Clamp01(YMax01) * binCount;
                    NormalizeRect(ref xMin, ref xMax, ref yMin, ref yMax);
                    xMin = Math.Clamp(xMin, 0, binCount);
                    xMax = Math.Clamp(xMax, 0, binCount);
                    yMin = Math.Clamp(yMin, 0, binCount);
                    yMax = Math.Clamp(yMax, 0, binCount);
                    return GateBinGeometry.Rectangle(xMin, xMax, yMin, yMax);
                }
                case GateType.Ellipse:
                {
                    double cx = Clamp01(CenterX01) * binCount;
                    double cy = Clamp01(CenterY01) * binCount;
                    double rx = Math.Max(0, Clamp01(RadiusX01) * binCount);
                    double ry = Math.Max(0, Clamp01(RadiusY01) * binCount);
                    return GateBinGeometry.Ellipse(cx, cy, rx, ry, AngleDeg);
                }
                case GateType.Polygon:
                {
                    var pts = PolygonPoints01 ?? Array.Empty<Point01>();
                    var binPts = pts
                        .Select(p => new GateBinPoint(Math.Clamp(p.X01, 0, 1) * binCount, Math.Clamp(p.Y01, 0, 1) * binCount))
                        .ToArray();
                    return GateBinGeometry.Polygon(binPts);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unsupported gate geometry type.");
            }
        }

        public int GetGeometryHash()
        {
            var hc = new HashCode();
            hc.Add(Type);
            hc.Add(XMin01);
            hc.Add(XMax01);
            hc.Add(YMin01);
            hc.Add(YMax01);
            hc.Add(CenterX01);
            hc.Add(CenterY01);
            hc.Add(RadiusX01);
            hc.Add(RadiusY01);
            hc.Add(AngleDeg);
            if (PolygonPoints01 != null)
            {
                hc.Add(PolygonPoints01.Count);
                foreach (var p in PolygonPoints01)
                {
                    hc.Add(p.X01);
                    hc.Add(p.Y01);
                }
            }
            return hc.ToHashCode();
        }

        private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

        private static void NormalizeRect01(ref double xMin01, ref double xMax01, ref double yMin01, ref double yMax01)
        {
            if (xMin01 > xMax01)
                (xMin01, xMax01) = (xMax01, xMin01);
            if (yMin01 > yMax01)
                (yMin01, yMax01) = (yMax01, yMin01);
        }

        private static void NormalizeRect(ref double xMin, ref double xMax, ref double yMin, ref double yMax)
        {
            if (xMin > xMax)
                (xMin, xMax) = (xMax, xMin);
            if (yMin > yMax)
                (yMin, yMax) = (yMax, yMin);
        }
    }

    public readonly record struct GateBinPoint(double X, double Y);

    public sealed class GateBinGeometry
    {
        private GateBinGeometry(
            GateType type,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            double cx,
            double cy,
            double rx,
            double ry,
            double angleDeg,
            IReadOnlyList<GateBinPoint>? polygonPoints)
        {
            Type = type;
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            CenterX = cx;
            CenterY = cy;
            RadiusX = rx;
            RadiusY = ry;
            AngleDeg = angleDeg;
            PolygonPoints = polygonPoints;
        }

        public GateType Type { get; }

        // Rectangle (bin coords)
        public double XMin { get; }
        public double XMax { get; }
        public double YMin { get; }
        public double YMax { get; }

        // Ellipse (bin coords)
        public double CenterX { get; }
        public double CenterY { get; }
        public double RadiusX { get; }
        public double RadiusY { get; }
        public double AngleDeg { get; }

        // Polygon (bin coords)
        public IReadOnlyList<GateBinPoint>? PolygonPoints { get; }

        public static GateBinGeometry Rectangle(double xMin, double xMax, double yMin, double yMax) =>
            new(GateType.Rectangle, xMin, xMax, yMin, yMax, 0, 0, 0, 0, 0, null);

        public static GateBinGeometry Ellipse(double cx, double cy, double rx, double ry, double angleDeg) =>
            new(GateType.Ellipse, 0, 0, 0, 0, cx, cy, rx, ry, angleDeg, null);

        public static GateBinGeometry Polygon(IReadOnlyList<GateBinPoint> points) =>
            new(GateType.Polygon, 0, 0, 0, 0, 0, 0, 0, 0, 0, points);
    }
}

