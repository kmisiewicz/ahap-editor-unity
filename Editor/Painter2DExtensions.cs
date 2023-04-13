using UnityEngine;
using UnityEngine.UIElements;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal static class Painter2DExtensions
    {
        public static void DrawLine(this Painter2D painter, float? width = null, Color? color = null,
            bool closePath = false, params Vector2[] points)
        {
            if (points.Length < 2)
                return;

            if (width.HasValue)
                painter.lineWidth = width.Value;
            if (color.HasValue)
                painter.strokeColor = color.Value;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++)
                painter.LineTo(points[i]);
            if (closePath)
                painter.ClosePath();
            painter.Stroke();
        }

        public static void DrawLinesWithInterval(this Painter2D painter, Vector2 firstPoint, Vector2 secondPoint,
            float interval, int count, bool horizontal, float? width = null, Color? color = null)
        {
            if (width.HasValue)
                painter.lineWidth = width.Value;
            if (color.HasValue)
                painter.strokeColor = color.Value;

            painter.BeginPath();
            for (int i = 0; i < count; i++)
            {
                painter.MoveTo(firstPoint);
                painter.LineTo(secondPoint);
                if (horizontal)
                    firstPoint.y = secondPoint.y = firstPoint.y + interval;
                else
                    firstPoint.x = secondPoint.x = firstPoint.x + interval;
            }
            painter.Stroke();
        }

        public static void DrawFilledCircle(this Painter2D painter, Vector2 center, float radius, Color? color = null)
        {
            if (color.HasValue)
                painter.fillColor = color.Value;
            painter.BeginPath();
            painter.Arc(center, radius, 0, 360);
            painter.Fill();
        }
    }
}
