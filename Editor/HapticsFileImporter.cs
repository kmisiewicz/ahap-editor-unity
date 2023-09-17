using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Chroma.Haptics.EditorWindow
{
    internal class ImportData
    {
        public string ProjectName;
        public string ProjectVersion;
    }

    public static class HapticsFileImporter
    {
        internal static void Import(UnityEngine.Object asset, List<HapticEvent> events, Action onBeforeImport = null, Action<ImportData> onAfterImport = null)
        {
            if (asset == null)
            {
                Debug.LogError("No file.");
                return;
            }

            string jsonText = string.Empty;
            if (asset is TextAsset textAsset)
            {
                jsonText = textAsset.text;
            }
            else
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string extension = Path.GetExtension(assetPath);
                if (extension == ".ahap" || extension == ".haptic")
                    jsonText = File.ReadAllText(assetPath);
            }

            if (!string.IsNullOrEmpty(jsonText))
            {
                StringBuilder errorMessageBuilder = new($"Error while importing file {asset.name}{Environment.NewLine}");

                try
                {
                    JsonAHAP ahap = JsonConvert.DeserializeObject<JsonAHAP>(jsonText);
                    onBeforeImport?.Invoke();
                    ImportAHAPFile(events, ahap, asset.name, onAfterImport);
                    return;
                }
                catch (Exception ex)
                {
                    errorMessageBuilder.AppendLine(ex.Message);
                }

                try
                {
                    JsonHaptic haptic = JsonConvert.DeserializeObject<JsonHaptic>(jsonText);
                    onBeforeImport?.Invoke();
                    ImportHapticFile(events, haptic, onAfterImport);
                }
                catch (Exception ex)
                {
                    errorMessageBuilder.AppendLine(Environment.NewLine);
                    errorMessageBuilder.AppendLine(ex.Message);
                    Debug.LogError(errorMessageBuilder.ToString());
                }
            }
            else
            {
                ImportHapticClip(asset, events, onBeforeImport, onAfterImport);
            }
        }

        static void ImportAHAPFile(List<HapticEvent> events, JsonAHAP ahap, string fileName, Action<ImportData> onAfterImport = null)
        {
            try
            {
                foreach (var patternElement in ahap.Pattern)
                {
                    if (patternElement.Event == null) continue;

                    Event e = patternElement.Event;
                    int index = e.EventParameters.FindIndex(param => param.ParameterID == JsonAHAP.PARAM_INTENSITY);
                    float intensity = (float)(index != -1 ? e.EventParameters[index].ParameterValue : 1);
                    index = e.EventParameters.FindIndex(param => param.ParameterID == JsonAHAP.PARAM_SHARPNESS);
                    float sharpness = (float)(index != -1 ? e.EventParameters[index].ParameterValue : 0);
                    if (e.EventType == JsonAHAP.EVENT_TRANSIENT)
                    {
                        events.Add(new TransientEvent((float)e.Time, intensity, sharpness));
                    }
                    else if (e.EventType == JsonAHAP.EVENT_CONTINUOUS)
                    {
                        ContinuousEvent ce = new();

                        List<EventPoint> points = new();
                        float t = (float)e.Time;
                        Pattern curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_INTENSITY, t);
                        while (curve != null)
                        {
                            foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                points.Add(new EventPoint((float)point.Time, (float)point.ParameterValue, ce));
                            t = points[^1].Time;
                            curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_INTENSITY, t, curve);
                        }
                        if (points.Count == 0)
                        {
                            points.Add(new EventPoint((float)e.Time, intensity, ce));
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), intensity, ce));
                        }
                        else if (!Mathf.Approximately(points[^1].Time, (float)(e.Time + e.EventDuration)))
                        {
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), points[^1].Value, ce));
                        }
                        ce.IntensityCurve = points;

                        points = new();
                        t = (float)e.Time;
                        curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_SHARPNESS, t);
                        while (curve != null)
                        {
                            foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                points.Add(new EventPoint((float)point.Time, (float)point.ParameterValue, ce));
                            t = points[^1].Time;
                            curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_SHARPNESS, t, curve);
                        }
                        if (points.Count == 0)
                        {
                            points.Add(new EventPoint((float)e.Time, sharpness, ce));
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), sharpness, ce));
                        }
                        else if (!Mathf.Approximately(points[^1].Time, (float)(e.Time + e.EventDuration)))
                        {
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), points[^1].Value, ce));
                        }
                        ce.SharpnessCurve = points;

                        events.Add(ce);
                    }
                }
                onAfterImport?.Invoke(new ImportData()
                {
                    ProjectName = ahap.Metadata.Project,
                    ProjectVersion = ahap.Version.ToString(CultureInfo.InvariantCulture)
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while importing file {fileName}{Environment.NewLine}{ex.Message}");
            }
        }

        static void ImportHapticClip(UnityEngine.Object asset, List<HapticEvent> events, Action onBeforeImport, Action<ImportData> onAfterImport)
        {
            string json;
            try
            {
                Type hapticClipType = asset.GetType();
                FieldInfo fieldInfo = hapticClipType.GetField("json");
                if (fieldInfo == null)
                {
                    Debug.LogError($"Error while importing file {asset.name}{Environment.NewLine}Invalid file format.");
                    return;
                }
                byte[] jsonBytes = (byte[])fieldInfo.GetValue(asset);
                json = Encoding.UTF8.GetString(jsonBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while importing file {asset.name}{Environment.NewLine}{ex.Message}");
                return;
            }

            try
            {
                JsonHaptic haptic = JsonConvert.DeserializeObject<JsonHaptic>(json);
                onBeforeImport?.Invoke();
                ImportHapticFile(events, haptic, onAfterImport);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while importing file {asset.name}{Environment.NewLine}{ex.Message}");
            }
        }

        static void ImportHapticFile(List<HapticEvent> events, JsonHaptic haptic, Action<ImportData> onAfterImport)
        {
            var amplitudes = haptic.signals.continuous.envelopes.amplitude;
            var frequencies = haptic.signals.continuous.envelopes.frequency;
            if (amplitudes == null || amplitudes.Count == 0 || frequencies == null || frequencies.Count == 0)
            {
                Debug.LogError("Haptic file contains invalid data.");
                return;
            }

            ContinuousEvent ce = new();

            List<EventPoint> points = new();
            foreach (var amplitude in amplitudes)
            {
                points.Add(new EventPoint((float)amplitude.time, (float)amplitude.amplitude, ce));
                if (amplitude.emphasis != null)
                {
                    events.Add(new TransientEvent((float)amplitude.time,
                        (float)amplitude.emphasis.amplitude, (float)amplitude.emphasis.frequency));
                }
            }
            (float minTime, float maxTime) = (points[0].Time, points[^1].Time);
            ce.IntensityCurve = points;

            points = new();
            foreach (var frequency in frequencies)
                points.Add(new EventPoint((float)frequency.time, (float)frequency.frequency, ce));

            if (!Mathf.Approximately(points[0].Time, minTime))
            {
                if (points[0].Time > minTime)
                    points.Insert(0, new EventPoint(minTime, points[0].Value, ce));
                else
                    ce.IntensityCurve.Insert(0, new EventPoint(points[0].Time, ce.IntensityCurve[0].Value, ce));
            }
            if (!Mathf.Approximately(points[^1].Time, maxTime))
            {
                if (points[^1].Time < maxTime)
                    points.Add(new EventPoint(maxTime, points[^1].Value, ce));
                else
                    ce.IntensityCurve.Add(new EventPoint(points[^1].Time, ce.IntensityCurve[^1].Value, ce));
            }

            ce.SharpnessCurve = points;

            events.Add(ce);

            onAfterImport?.Invoke(new ImportData()
            {
                ProjectName = haptic.metadata.project,
                ProjectVersion = string.Join('.', new int[] { haptic.version.major, haptic.version.minor, haptic.version.patch })
            });
        }
    }
}
