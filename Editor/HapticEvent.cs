using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal enum MouseLocation { Outside = 0, IntensityPlot = 1, SharpnessPlot = 2 }

    internal class EventPoint : IComparable<EventPoint>
    {
        public float Time;
        public float Value;
        public HapticEvent ParentEvent;

        public EventPoint(float time, float value, HapticEvent parent)
        {
            Time = time;
            Value = value;
            ParentEvent = parent;
        }

        public int CompareTo(EventPoint other)
        {
            if (Time < other.Time) return -1;
            if (Time == other.Time) return 0;
            return 1;
        }

        //public static implicit operator EventPoint(Vector2 v2) => new EventPoint(v2.x, v2.y);

        public static implicit operator Vector2(EventPoint point) => new Vector2(point.Time, point.Value);
    }

    internal abstract class HapticEvent : IComparable<HapticEvent>
    {
        public abstract float Time { get; }

        public int CompareTo(HapticEvent other)
        {
            if (Time < other.Time) return -1;
            if (Time == other.Time) return 0;
            return 1;
        }

        public abstract bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint);

        public abstract bool IsOnPointInEvent(in Rect offsetRect, MouseLocation location, out EventPoint eventPoint);

        public abstract bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location);

        public abstract List<Pattern> ToAHAP();
    }

    internal class TransientEvent : HapticEvent
    {
        public EventPoint Intensity;
        public EventPoint Sharpness;

        public override float Time => Intensity?.Time ?? 0;

        public TransientEvent(float time, float intensity, float sharpness)
        {
            Intensity = new EventPoint(time, intensity, this);
            Sharpness = new EventPoint(time, sharpness, this);
        }

        public TransientEvent(Vector2 pointPosition, MouseLocation plot)
        {
            EventPoint point = new(pointPosition.x, pointPosition.y, this);
            EventPoint midPoint = new(point.Time, 0.5f, this);
            Intensity = plot == MouseLocation.IntensityPlot ? point : midPoint;
            Sharpness = plot == MouseLocation.SharpnessPlot ? point : midPoint;
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

        public override bool IsOnPointInEvent(in Rect offsetRect, MouseLocation location, out EventPoint eventPoint)
        {
            eventPoint = null;
            if (location != MouseLocation.Outside)
            {
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

        public override List<Pattern> ToAHAP()
        {
            Event e = new(Time, JsonAHAP.EVENT_TRANSIENT, null, Intensity.Value, Sharpness.Value);
            return new List<Pattern>() { new Pattern(e, null) };
        }
    }

    internal class ContinuousEvent : HapticEvent
    {
        public List<EventPoint> IntensityCurve;
        public List<EventPoint> SharpnessCurve;

        public override float Time => IntensityCurve?.FirstOrDefault()?.Time ?? 0;
        public float TimeMax => IntensityCurve?.LastOrDefault()?.Time ?? 0;

        public ContinuousEvent() { }

        public ContinuousEvent(Vector2 startEndTimes, Vector2 intensity, Vector2 sharpness)
        {
            IntensityCurve = new List<EventPoint>() { new EventPoint(startEndTimes.x, intensity.x, this),
                new EventPoint(startEndTimes.y, intensity.y, this) };
            SharpnessCurve = new List<EventPoint>() { new EventPoint(startEndTimes.x, sharpness.x, this),
                new EventPoint(startEndTimes.y, sharpness.y, this) };
        }

        public ContinuousEvent(Vector2 firstPoint, Vector2 secondPoint, MouseLocation plot)
        {
            if (firstPoint.x > secondPoint.x)
                (firstPoint, secondPoint) = (secondPoint, firstPoint);
            List<EventPoint> specCurve = new() { new EventPoint(firstPoint.x, firstPoint.y, this),
                new EventPoint(secondPoint.x, secondPoint.y, this) };
            List<EventPoint> defaultCurve = new() { new EventPoint(firstPoint.x, 0.5f, this),
                new EventPoint(secondPoint.x, 0.5f, this) };
            IntensityCurve = plot == MouseLocation.IntensityPlot ? specCurve : defaultCurve;
            SharpnessCurve = plot == MouseLocation.SharpnessPlot ? specCurve : defaultCurve;
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

        public override bool IsOnPointInEvent(in Rect offsetRect, MouseLocation location, out EventPoint eventPoint)
        {
            eventPoint = null;
            if (location != MouseLocation.Outside || offsetRect.xMax < Time || offsetRect.xMin > IntensityCurve[^1].Time)
            {
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

        public override List<Pattern> ToAHAP()
        {
            List<Pattern> list = new();

            // Event
            Event e = new(Time, JsonAHAP.EVENT_CONTINUOUS, IntensityCurve.Last().Time - Time, 1, 0);
            list.Add(new Pattern(e, null));

            // Parameter curves
            list.AddRange(CurveToPatterns(IntensityCurve, JsonAHAP.CURVE_INTENSITY));
            list.AddRange(CurveToPatterns(SharpnessCurve, JsonAHAP.CURVE_SHARPNESS));

            return list;
        }

        public void AddPointToCurve(Vector2 pointPosition, MouseLocation plot)
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

            EventPoint point = new(pointPosition.x, pointPosition.y, this);

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
