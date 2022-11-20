using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow
    {
        private void HandleSaving()
        {
            if (_events.Count == 0)
            {
                EditorUtility.DisplayDialog("No events", "Create some events to save it in file.", "OK");
                return;
            }

            var ahapFile = ConvertEventsToAHAPFile();
            string json = JsonConvert.SerializeObject(ahapFile, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (_ahapFile != null && EditorUtility.DisplayDialog("Overwrite file?", "Do you want to overwrite selected file?",
                "Yes, overwrite", "No, create new"))
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, AssetDatabase.GetAssetPath(_ahapFile)), json);
                EditorUtility.SetDirty(_ahapFile);
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("Save AHAP JSON", "ahap", "json", "Enter file name");
            if (path.Length != 0)
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, path), json);
                AssetDatabase.ImportAsset(path);
                _ahapFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                EditorUtility.SetDirty(_ahapFile);
            }
        }

        private void HandleImport()
        {
            if (_ahapFile != null)
            {
                AHAPFile ahap;
                try
                {
                    ahap = JsonConvert.DeserializeObject<AHAPFile>(_ahapFile.text);

                    _events ??= new List<VibrationEvent>();
                    _events.Clear();

                    foreach (var patternElement in ahap.Pattern)
                    {
                        Event e = patternElement.Event;
                        if (e != null)
                        {
                            int index = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_INTENSITY);
                            float intensity = index != -1 ? (float)e.EventParameters[index].ParameterValue : 1;
                            index = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_SHARPNESS);
                            float sharpness = index != -1 ? (float)e.EventParameters[index].ParameterValue : 0;
                            if (e.EventType == AHAPFile.EVENT_TRANSIENT)
                            {
                                _events.Add(new TransientEvent((float)e.Time, intensity, sharpness));
                            }
                            else if (e.EventType == AHAPFile.EVENT_CONTINUOUS)
                            {
                                ContinuousEvent ce = new();

                                List<EventPoint> points = new();
                                float t = (float)e.Time;
                                Pattern curve = ahap.FindCurveOnTime(AHAPFile.CURVE_INTENSITY, t);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                        points.Add(new EventPoint((float)point.Time, (float)point.ParameterValue, ce));
                                    t = points.Last().Time;
                                    curve = ahap.FindCurveOnTime(AHAPFile.CURVE_INTENSITY, t, curve);
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
                                curve = ahap.FindCurveOnTime(AHAPFile.CURVE_SHARPNESS, t);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                        points.Add(new EventPoint((float)point.Time, (float)point.ParameterValue, ce));
                                    t = points.Last().Time;
                                    curve = ahap.FindCurveOnTime(AHAPFile.CURVE_SHARPNESS, t, curve);
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
                    }
                    _time = GetLastPointTime();
                    _projectName = ahap.Metadata.Project;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while importing file {_ahapFile.name}{Environment.NewLine}{ex.Message}");
                }
            }
        }
    }
}
