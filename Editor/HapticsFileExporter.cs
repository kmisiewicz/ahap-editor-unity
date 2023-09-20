using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Chroma.Haptics.EditorWindow
{
    public static class HapticsFileExporter
    {
        internal static void Export(List<HapticEvent> events, SaveOptions saveOptions, ref UnityEngine.Object asset)
        {
            if (events.Count == 0)
            {
                EditorUtility.DisplayDialog("No events", "Create some events to save it in file.", "OK");
                return;
            }

            string json = string.Empty;
            string extension = "";
            if (saveOptions.FileFormat == FileFormat.AHAP)
            {
                JsonAHAP ahapFile = ConvertEventsToAHAPFile(events, saveOptions);
                json = JsonConvert.SerializeObject(ahapFile, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                extension = saveOptions.UseJSONFormat ? ".json" : ".ahap";
            }
            else if (saveOptions.FileFormat == FileFormat.Haptic)
            {
                JsonHaptic hapticFile = ConvertEventsToHapticFile(events, saveOptions);
                json = JsonConvert.SerializeObject(hapticFile, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                extension = saveOptions.UseJSONFormat ? ".json" : ".haptic";
            }

            string path;
            if (asset != null && saveOptions.Overwrite)
            {
                path = AssetDatabase.GetAssetPath(asset);
                path = Path.ChangeExtension(path, extension);
            }
            else
            {
                path = EditorUtility.SaveFilePanelInProject("Save file", "HapticClip", extension.TrimStart('.'), "Enter file name");
                if (File.Exists(Path.Combine(Environment.CurrentDirectory, path)))
                    saveOptions.Overwrite = true;
            }

            if (path.Length != 0)
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, path), json);
                if (!saveOptions.Overwrite)
                {
                    AssetDatabase.ImportAsset(path);
                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }
                EditorUtility.SetDirty(asset);
            }
        }

        static JsonAHAP ConvertEventsToAHAPFile(List<HapticEvent> events, SaveOptions saveOptions)
        {
            List<Pattern> patternList = new();
            foreach (var ev in events)
                patternList.AddRange(ev.ToAHAP());
            patternList.Sort();
            double version = double.TryParse(saveOptions.ProjectVersion, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 1;
            return new JsonAHAP(version, new Metadata(saveOptions.ProjectName), patternList);
        }

        static JsonHaptic ConvertEventsToHapticFile(List<HapticEvent> events, SaveOptions saveOptions)
        {
            events.Sort();
            List<ContinuousEvent> continuousEvents = new();
            List<TransientEvent> transientEvents = new();
            foreach (var ev in events)
            {
                if (ev is ContinuousEvent ce) continuousEvents.Add(ce);
                else if (ev is TransientEvent te) transientEvents.Add(te);
            }
            List<Amplitude> amplitudes = new();
            List<Frequency> frequencies = new();
            foreach (var ce in continuousEvents)
            {
                foreach (var intensity in ce.IntensityCurve)
                    amplitudes.Add(new Amplitude(intensity.Time, intensity.Value));
                foreach (var sharpness in ce.SharpnessCurve)
                    frequencies.Add(new Frequency(sharpness.Time, sharpness.Value));
            }
            if (amplitudes[0].time != 0)
            {
                amplitudes.Insert(0, new Amplitude(0, 0));
                amplitudes.Insert(1, new Amplitude(Mathf.Max((float)amplitudes[1].time - 0.001f, 0), 0));
            }
            if (frequencies[0].time != 0)
            {
                frequencies.Insert(0, new Frequency(0, 0));
                frequencies.Insert(1, new Frequency(Mathf.Max((float)frequencies[1].time - 0.001f, 0), 0));
            }

            (Amplitude lastAmplitude, Frequency lastFrequency) = (amplitudes[^1], frequencies[^1]);
            if (!Mathf.Approximately((float)lastAmplitude.time, (float)lastFrequency.time)) // shouldn't happen?
            {
                if (lastAmplitude.time > lastFrequency.time)
                    frequencies.Add(new Frequency(lastAmplitude.time, lastFrequency.frequency));
                else
                    amplitudes.Add(new Amplitude(lastFrequency.time, lastAmplitude.amplitude));
            }

            (lastAmplitude, lastFrequency) = (amplitudes[^1], frequencies[^1]);
            if (transientEvents.Count > 0 && transientEvents[^1].Time > (float)amplitudes[^1].time)
            {
                TransientEvent lastTransient = transientEvents[^1];
                amplitudes.Add(new Amplitude(lastAmplitude.time + 0.001f, 0));
                amplitudes.Add(new Amplitude(lastTransient.Time, 0));
                frequencies.Add(new Frequency(lastFrequency.time + 0.001f, 0));
                frequencies.Add(new Frequency(lastTransient.Time, 0));
            }

            int startIndex = 0;
            foreach (var te in transientEvents)
            {
                int index = amplitudes.FindIndex(a => Mathf.Approximately((float)a.time, te.Time));
                if (index != -1)
                {
                    amplitudes[index].emphasis = new Emphasis(te.Intensity.Value, te.Sharpness.Value);
                    startIndex = index;
                }
                else
                {
                    index = amplitudes.FindIndex(startIndex, a => a.time > te.Time);
                    if (index != -1)
                    {
                        float val1 = (float)amplitudes[index - 1].amplitude;
                        float val2 = (float)amplitudes[index].amplitude;
                        float value = Mathf.Lerp(val1, val2, (te.Time - val1) / (val2 - val1));
                        Amplitude a = new(te.Time, value);
                        a.emphasis = new Emphasis(te.Intensity.Value, te.Sharpness.Value);
                        amplitudes.Insert(index, a);
                        startIndex = index;
                    }
                }
            }

            JsonHaptic hapticFile = new();
            hapticFile.signals.continuous.envelopes.amplitude = amplitudes;
            hapticFile.signals.continuous.envelopes.frequency = frequencies;
            hapticFile.metadata.project = saveOptions.ProjectName;
            string[] versionComponents = saveOptions.ProjectVersion.Split('.');
            if (versionComponents.Length > 0)
                hapticFile.version.major = int.TryParse(versionComponents[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int major) ? major : 1;
            if (versionComponents.Length > 1)
                hapticFile.version.minor = int.TryParse(versionComponents[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minor) ? minor : 0;
            if (versionComponents.Length > 2)
                hapticFile.version.patch = int.TryParse(versionComponents[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int patch) ? patch : 0;
            return hapticFile;
        }
    }
}
