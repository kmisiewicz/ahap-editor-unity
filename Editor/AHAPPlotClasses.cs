using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal enum MouseLocation { Outside = 0, IntensityPlot = 1, SharpnessPlot = 2 }

    internal class EventPoint
    {
        public float Time;
        public float Value;

        public EventPoint(float time, float value)
        {
            Time = time;
            Value = value;
        }

        public static implicit operator EventPoint(Vector2 v2) => new EventPoint(v2.x, v2.y);

        public static implicit operator Vector2(EventPoint point) => new Vector2(point.Time, point.Value);
    }

    internal abstract class VibrationEvent
    {
        public float Time;

        public abstract bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint);

        public abstract bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location);

        public abstract List<Pattern> ToPatterns();
    }

    internal class TransientEvent : VibrationEvent
    {
        public EventPoint Intensity;
        public EventPoint Sharpness;

        public TransientEvent(float time, float intensity, float sharpness)
        {
            Time = time;
            Intensity = new EventPoint(time, intensity);
            Sharpness = new EventPoint(time, sharpness);
        }

        public override bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint)
        {
            eventPoint = null;
            if (location != MouseLocation.Outside)
            {
                Rect offsetRect = new(point - offset, offset * 2);
                var pointToTest = location == MouseLocation.IntensityPlot ? Intensity : Sharpness;
                if (offsetRect.Contains(pointToTest))
                {
                    eventPoint = pointToTest;
                    return true;
                }
            }
            return false;
        }

        public override bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location) => true;

        public override List<Pattern> ToPatterns()
        {
            Event e = new(Time, AHAPFile.EVENT_TRANSIENT, null, Intensity.Value, Sharpness.Value);
            return new List<Pattern>() { new Pattern(e, null) };
        }
    }

    internal class ContinuousEvent : VibrationEvent
    {
        public List<EventPoint> IntensityCurve;
        public List<EventPoint> SharpnessCurve;

        public ContinuousEvent() { }

        public ContinuousEvent(Vector2 startEndTimes, Vector2 intensity, Vector2 sharpness)
        {
            Time = startEndTimes.x;
            IntensityCurve = new List<EventPoint>() { new EventPoint(startEndTimes.x, intensity.x), new EventPoint(startEndTimes.y, intensity.y) };
            SharpnessCurve = new List<EventPoint>() { new EventPoint(startEndTimes.x, sharpness.x), new EventPoint(startEndTimes.y, sharpness.y) };
        }

        public override bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint)
        {
            eventPoint = null;
            if (location != MouseLocation.Outside)
            {
                Rect offsetRect = new(point - offset, offset * 2);
                List<EventPoint> curve = location == MouseLocation.IntensityPlot ? IntensityCurve : SharpnessCurve;
                foreach (EventPoint ep in curve)
                {
                    if (offsetRect.Contains(ep))
                    {
                        eventPoint = ep;
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location)
        {
            if (location != MouseLocation.Outside)
            {
                List<EventPoint> curve = location == MouseLocation.IntensityPlot ? IntensityCurve : SharpnessCurve;
                if (curve.Count > 2 && (pointToRemove == curve.First() || pointToRemove == curve.Last()))
                    return false;
                curve.Remove(pointToRemove);
                return curve.Count < 2;
            }
            return false;
        }

        public override List<Pattern> ToPatterns()
        {
            List<Pattern> list = new();

            // Event
            Event e = new(Time, AHAPFile.EVENT_CONTINUOUS, IntensityCurve.Last().Time - Time, 1, 0);
            list.Add(new Pattern(e, null));

            // Parameter curves
            list.AddRange(CurveToPatterns(IntensityCurve, AHAPFile.CURVE_INTENSITY));
            list.AddRange(CurveToPatterns(SharpnessCurve, AHAPFile.CURVE_SHARPNESS));

            return list;
        }

        public void AddPointToCurve(EventPoint point, MouseLocation plot)
        {
            if (plot == MouseLocation.Outside)
            {
                Debug.LogError("Wrong location!");
                return;
            }

            var curve = plot == MouseLocation.IntensityPlot ? IntensityCurve : SharpnessCurve;
            if (curve.Count < 2)
            {
                Debug.LogError("Invalid curve!");
                return;
            }

            curve.Add(point);
            curve.Sort((p1, p2) => p1.Time.CompareTo(p2.Time));
        }

        private List<Pattern> CurveToPatterns(List<EventPoint> pointsList, string curveName)
        {
            List<Pattern> list = new();
            var curve = new ParameterCurve(Time, curveName);
            foreach (var point in pointsList)
            {
                curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(point.Time, point.Value));
                if (curve.ParameterCurveControlPoints.Count >= 16)
                {
                    list.Add(new Pattern(null, curve));
                    curve = new ParameterCurve(point.Time, curveName);
                    curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(point.Time, point.Value));
                }
            }
            if (list.Count == 0 || (list.Count > 0 && curve.ParameterCurveControlPoints.Count > 1))
                list.Add(new Pattern(null, curve));
            return list;
        }
    }
}
