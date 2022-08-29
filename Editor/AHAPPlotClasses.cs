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
                if (location == MouseLocation.IntensityPlot && offsetRect.Contains(Intensity))
                {
                    eventPoint = Intensity;
                    return true;
                }
                else if (location == MouseLocation.SharpnessPlot && offsetRect.Contains(Sharpness))
                {
                    eventPoint = Sharpness;
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

        public ContinuousEvent(Vector2 time, Vector2 intensity, Vector2 sharpness)
        {
            Time = time.x;
            IntensityCurve = new List<EventPoint>() { new EventPoint(time.x, intensity.x), new EventPoint(time.y, intensity.y) };
            SharpnessCurve = new List<EventPoint>() { new EventPoint(time.x, sharpness.x), new EventPoint(time.y, sharpness.y) };
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
            var curve = new ParameterCurve(Time, AHAPFile.CURVE_INTENSITY);
            foreach (var intensityPoint in IntensityCurve)
            {
                curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(intensityPoint.Time, intensityPoint.Value));
                if (curve.ParameterCurveControlPoints.Count >= 16)
                {
                    list.Add(new Pattern(null, curve));
                    curve = new ParameterCurve(intensityPoint.Time, AHAPFile.CURVE_INTENSITY);
                    curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(intensityPoint.Time, intensityPoint.Value));
                }
            }
            if (list.Count == 1 || (list.Count > 1 && curve.ParameterCurveControlPoints.Count > 1))
                list.Add(new Pattern(null, curve));

            int count = list.Count;
            curve = new ParameterCurve(Time, AHAPFile.CURVE_SHARPNESS);
            foreach (var sharpnessPoint in SharpnessCurve)
            {
                curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(sharpnessPoint.Time, sharpnessPoint.Value));
                if (curve.ParameterCurveControlPoints.Count >= 16)
                {
                    list.Add(new Pattern(null, curve));
                    curve = new ParameterCurve(sharpnessPoint.Time, AHAPFile.CURVE_SHARPNESS);
                    curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(sharpnessPoint.Time, sharpnessPoint.Value));
                }
            }
            if (list.Count == count || (list.Count > count && curve.ParameterCurveControlPoints.Count > 1))
                list.Add(new Pattern(null, curve));

            return list;
        }
    }
}
