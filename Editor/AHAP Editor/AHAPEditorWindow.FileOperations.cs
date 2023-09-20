using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow
    {
        private void HandleSaving(bool overwrite, bool jsonExt, DataFormat dataFormat, FileFormat fileFormat)
        {
            if (_events.Count == 0)
            {
                EditorUtility.DisplayDialog("No events", "Create some events to save it in file.", "OK");
                return;
            }

            string json = string.Empty;
            string extension = "";
            if (fileFormat == FileFormat.AHAP)
            {
                JsonAHAP ahapFile = ConvertEventsToAHAPFile(dataFormat);
                json = JsonConvert.SerializeObject(ahapFile, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                extension = jsonExt ? ".json" : ".ahap";

            }
            else if (fileFormat == FileFormat.Haptic)
            {
                JsonHaptic hapticFile = ConvertEventsToHapticFile(dataFormat);
                json = JsonConvert.SerializeObject(hapticFile, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                extension = jsonExt ? ".json" : ".haptic";
            }

            string path = string.Empty;
            if (_vibrationAsset != null && overwrite)
            {
                path = AssetDatabase.GetAssetPath(_vibrationAsset);
                path = Path.ChangeExtension(path, extension);                
            }
            else
            {
                path = EditorUtility.SaveFilePanelInProject("Save file", "HapticClip", extension.TrimStart('.'), "Enter file name");
                if (File.Exists(Path.Combine(Environment.CurrentDirectory, path)))
                    overwrite = true;
            }

            if (path.Length != 0)
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, path), json);
                if (!overwrite)
                {
                    AssetDatabase.ImportAsset(path);
                    _vibrationAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }
                EditorUtility.SetDirty(_vibrationAsset);
            }
        }

        private void HandleImport(DataFormat dataFormat = DataFormat.Linear)
        {
            if (_vibrationAsset == null)
            {
                Debug.LogError("No file.");
                return;
            }

            string jsonText = string.Empty;
            if (_vibrationAsset is TextAsset textAsset) 
                jsonText = textAsset.text;
            else
            {
                string assetPath = AssetDatabase.GetAssetPath(_vibrationAsset);
                string extension = Path.GetExtension(assetPath);
                if (extension == ".ahap" || extension == ".haptic")
                    jsonText = File.ReadAllText(assetPath);
            }

            if (!string.IsNullOrEmpty(jsonText))
            {
                StringBuilder errorMessageBuilder = new($"Error while importing file {_vibrationAsset.name}{Environment.NewLine}");

                try
                {
                    JsonAHAP ahap = JsonConvert.DeserializeObject<JsonAHAP>(jsonText);
                    Clear();
                    ImportAHAPFile(ahap, dataFormat);
                    return;
                }
                catch (Exception ex)
                {
                    errorMessageBuilder.AppendLine(ex.Message);
                }

                try
                {
                    JsonHaptic haptic = JsonConvert.DeserializeObject<JsonHaptic>(jsonText);
                    Clear();
                    ImportHapticFile(haptic, dataFormat);
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
                ImportHapticClip();
            }
        }

        private void ImportAHAPFile(JsonAHAP ahap, DataFormat dataFormat = DataFormat.Linear)
        {
            try
            {
                foreach (var patternElement in ahap.Pattern)
                {
                    if (patternElement.Event == null) continue;

                    Event e = patternElement.Event;
                    int index = e.EventParameters.FindIndex(param => param.ParameterID == JsonAHAP.PARAM_INTENSITY);
                    float intensity = index != -1 ? CalculateImportParameter(e.EventParameters[index].ParameterValue, dataFormat) : 1;
                    index = e.EventParameters.FindIndex(param => param.ParameterID == JsonAHAP.PARAM_SHARPNESS);
                    float sharpness = index != -1 ? CalculateImportParameter(e.EventParameters[index].ParameterValue, dataFormat) : 0;
                    if (e.EventType == JsonAHAP.EVENT_TRANSIENT)
                    {
                        _events.Add(new TransientEvent((float)e.Time, intensity, sharpness));
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
                                points.Add(new EventPoint((float)point.Time, CalculateImportParameter(point.ParameterValue, dataFormat), ce));
                            t = points.Last().Time;
                            curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_INTENSITY, t, curve);
                        }
                        if (points.Count == 0)
                        {
                            points.Add(new EventPoint((float)e.Time, intensity, ce));
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), intensity, ce));
                        }
                        else if (!Mathf.Approximately(points.Last().Time, (float)(e.Time + e.EventDuration)))
                        {
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), points.Last().Value, ce));
                        }
                        ce.IntensityCurve = points;

                        points = new();
                        t = (float)e.Time;
                        curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_SHARPNESS, t);
                        while (curve != null)
                        {
                            foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                points.Add(new EventPoint((float)point.Time, CalculateImportParameter(point.ParameterValue, dataFormat), ce));
                            t = points.Last().Time;
                            curve = ahap.FindCurveOnTime(JsonAHAP.CURVE_SHARPNESS, t, curve);
                        }
                        if (points.Count == 0)
                        {
                            points.Add(new EventPoint((float)e.Time, sharpness, ce));
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), sharpness, ce));
                        }
                        else if (!Mathf.Approximately(points.Last().Time, (float)(e.Time + e.EventDuration)))
                        {
                            points.Add(new EventPoint((float)(e.Time + e.EventDuration), points.Last().Value, ce));
                        }
                        ce.SharpnessCurve = points;

                        _events.Add(ce);
                    }
                }
                _time = GetLastPointTime();
                _projectName = ahap.Metadata.Project;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while importing file {_vibrationAsset.name}{Environment.NewLine}{ex.Message}");
            }
        }

        private void ImportHapticClip(DataFormat dataFormat = DataFormat.Linear)
        {
            string json = string.Empty;
            try 
            {
                Type hapticClipType = _vibrationAsset.GetType();
                FieldInfo fieldInfo = hapticClipType.GetField("json");
                if (fieldInfo == null) 
                {
                    Debug.LogError($"Error while importing file {_vibrationAsset.name}{Environment.NewLine}Invalid file format.");
                    return;
                }
                byte[] jsonBytes = (byte[])fieldInfo.GetValue(_vibrationAsset);
                json = Encoding.UTF8.GetString(jsonBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while importing file {_vibrationAsset.name}{Environment.NewLine}{ex.Message}");
                return;
            }

            try
            {
                JsonHaptic haptic = JsonConvert.DeserializeObject<JsonHaptic>(json);
                Clear();
                ImportHapticFile(haptic, dataFormat);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while importing file {_vibrationAsset.name}{Environment.NewLine}{ex.Message}");
            }
        }

        private void ImportHapticFile(JsonHaptic haptic, DataFormat dataFormat = DataFormat.Linear)
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
                points.Add(new EventPoint((float)amplitude.time, CalculateImportParameter((float)amplitude.amplitude, dataFormat), ce));
                if (amplitude.emphasis != null)
                {
                    _events.Add(new TransientEvent((float)amplitude.time,
                        CalculateImportParameter(amplitude.emphasis.amplitude, dataFormat),
                        CalculateImportParameter(amplitude.emphasis.frequency, dataFormat)));
                }
            }
            (float minTime, float maxTime) = (points[0].Time, points.Last().Time);
            ce.IntensityCurve = points;

            points = new();
            foreach (var frequency in frequencies)
                points.Add(new EventPoint((float)frequency.time, CalculateImportParameter(frequency.frequency, dataFormat), ce));

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

            _events.Add(ce);
            _time = GetLastPointTime();
            _projectName = haptic.metadata.project;
        }

        private static float CalculateImportParameter(double value, DataFormat dataFormat)
        {
            float floatInput = (float)value;
            return dataFormat switch
            {
                DataFormat.Squared => Mathf.Sqrt(floatInput),
                DataFormat.Power2_28 => Mathf.Pow(floatInput, 1 / 2.28f),
                _ => floatInput,
            };
        }

        private static double CalculateExportParameter(float value, DataFormat dataFormat)
        {
            return dataFormat switch
            {
                DataFormat.Squared => value * value,
                DataFormat.Power2_28 => Mathf.Pow(value, 2.28f),
                _ => value,
            };
        }
    }
}
