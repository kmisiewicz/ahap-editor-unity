using System;
using System.Collections.Generic;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal class Event
    {
        public double Time { get; set; }
        public string EventType { get; set; }
        public double? EventDuration { get; set; } = null;
        public List<EventParameter> EventParameters { get; set; }

        public Event(double time, string type, double? duration, double intensity, double sharpness)
        {
            Time = time;
            EventType = type;
            EventDuration = duration;
            EventParameters = new List<EventParameter>() {
                    new EventParameter(JsonAHAP.PARAM_INTENSITY, intensity),
                    new EventParameter(JsonAHAP.PARAM_SHARPNESS, sharpness)};
        }
    }

    internal class EventParameter
    {
        public string ParameterID { get; set; }
        public double ParameterValue { get; set; }

        public EventParameter(string parameterID, double parameterValue)
        {
            ParameterID = parameterID;
            ParameterValue = parameterValue;
        }
    }

    internal class Metadata
    {
        public string Project { get; set; }
        public string Created { get; set; }

        public Metadata(string project = "")
        {
            Project = project;
            Created = DateTime.Now.ToString();
        }
    }

    internal class ParameterCurve
    {
        public string ParameterID { get; set; }
        public double Time { get; set; }
        public List<ParameterCurveControlPoint> ParameterCurveControlPoints { get; set; } = new();

        public ParameterCurve(double time, string parameterID)
        {
            Time = time;
            ParameterID = parameterID;
        }
    }

    internal class ParameterCurveControlPoint
    {
        public double Time { get; set; }
        public double ParameterValue { get; set; }

        public ParameterCurveControlPoint(double time, double value)
        {
            Time = time;
            ParameterValue = value;
        }
    }

    internal class Pattern : IComparable<Pattern>
    {
        public Event Event { get; set; } = null;
        public ParameterCurve ParameterCurve { get; set; } = null;

        public Pattern(Event e, ParameterCurve curve)
        {
            Event = e;
            ParameterCurve = curve;
        }

        public int CompareTo(Pattern other)
        {
            if (other == null) return 1;

            if (Event != null && ParameterCurve == null && other.Event == null && other.ParameterCurve != null)
                return -1;

            if (Event == null && ParameterCurve != null && other.Event != null && other.ParameterCurve == null)
                return 1;

            if (Event != null && other.Event != null)
                return Event.Time.CompareTo(other.Event.Time);

            if (ParameterCurve != null && other.ParameterCurve != null)
            {
                if (ParameterCurve.ParameterID != other.ParameterCurve.ParameterID)
                    return ParameterCurve.ParameterID == JsonAHAP.CURVE_INTENSITY ? -1 : 1;
                else
                    return ParameterCurve.Time.CompareTo(other.ParameterCurve.Time);
            }

            return 0;
        }
    }

    internal class JsonAHAP
    {
        public const string EVENT_TRANSIENT = "HapticTransient";
        public const string EVENT_CONTINUOUS = "HapticContinuous";
        public const string PARAM_INTENSITY = "HapticIntensity";
        public const string PARAM_SHARPNESS = "HapticSharpness";
        public const string CURVE_INTENSITY = "HapticIntensityControl";
        public const string CURVE_SHARPNESS = "HapticSharpnessControl";

        public double Version { get; set; }
        public Metadata Metadata { get; set; }
        public List<Pattern> Pattern { get; set; }

        public JsonAHAP(double version, Metadata metadata, List<Pattern> pattern)
        {
            Version = version;
            Metadata = metadata;
            Pattern = pattern;
        }

        public Pattern FindCurveOnTime(string curveType, float time, Pattern previousCurve = null)
        {
            return Pattern.Find(element => element.ParameterCurve != null && (float)element.ParameterCurve.Time == time &&
                element.ParameterCurve.ParameterID == curveType && element != previousCurve);
        }
    }
}
