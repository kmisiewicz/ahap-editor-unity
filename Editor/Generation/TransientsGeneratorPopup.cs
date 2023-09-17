using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DSPLib;

namespace Chroma.Haptics.EditorWindow.Generation
{
    public class TransientsGeneratorPopup : PopupWindowContent, IHapticsGenerator
    {
        const string UXML_PATH = "Packages/com.chroma.ahapeditor/Editor/Generation/TransientsGenerator.uxml";
        const string CHUNK_SIZE_FIELD = "chunkSizeField";
        const string SENSITIVITY_FIELD = "sensitivityField";
        const string RMS_THRESHOLD_FIELD = "rmsThresholdField";
        const string GENERATE_BUTTON = "generateButton";

        AudioClip _audioClip;
        Action<List<HapticEvent>> _onEventsGenerated;

        public TransientsGeneratorPopup(AudioClip clip, Action<List<HapticEvent>> onEventsGenerated)
        {
            _audioClip = clip;
            _onEventsGenerated = onEventsGenerated;
        }

        public override void OnGUI(Rect rect) { }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, EditorGUIUtility.singleLineHeight * 4 +
                EditorGUIUtility.standardVerticalSpacing * 6 + 13);
        }

        public override void OnOpen()
        {
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            visualTreeAsset.CloneTree(editorWindow.rootVisualElement);

            editorWindow.rootVisualElement.Q<Button>(GENERATE_BUTTON).clicked += Generate;
        }

        void Generate()
        {
            var events = AudioToHaptics(_audioClip);
            _onEventsGenerated?.Invoke(events);
        }

        public List<HapticEvent> AudioToHaptics(AudioClip clip)
        {
            int chunkSize = (int)editorWindow.rootVisualElement.Q<UnsignedIntegerField>(CHUNK_SIZE_FIELD).value;
            float sensitivity = editorWindow.rootVisualElement.Q<Slider>(SENSITIVITY_FIELD).value;
            float rmsThreshold = editorWindow.rootVisualElement.Q<Slider>(RMS_THRESHOLD_FIELD).value;

            float[] monoSamples = AudioClipUtils.GetMonoSamples(clip);
            AudioClipUtils.CalculateSpectrum(monoSamples, clip.frequency,
                out double[][] spectrumOverTime, out double[] frequencySpan, chunkSize);

            StringBuilder outputMessage = new($"[Transients from onsets] ({clip.name}):");
            SpectralFluxAnalyzer spectralFlux = new(chunkSize, sensitivity);

            float chunkTime = (1f / clip.frequency) * chunkSize;
            int spectrumSize = spectrumOverTime[0].Length;
            float[] fakeSpectrum = new float[spectrumSize];
            int fluxWindowSize = spectralFlux.ThresholdWindowSize;
            float currentChunkTime = -fluxWindowSize * chunkTime;
            List<HapticEvent> events = new();

            // Prepend and extend with 0s to detect peaks in the beggining and at the end
            int i;
            for (i = -fluxWindowSize; i < 0; i++, currentChunkTime += chunkTime)
                spectralFlux.AnalyzeSpectrum(fakeSpectrum, currentChunkTime);
            for (i = 0; i < spectrumOverTime.Length; i++, currentChunkTime += chunkTime) // Actual samples
                spectralFlux.AnalyzeSpectrum(Array.ConvertAll(spectrumOverTime[i], x => (float)x), currentChunkTime);
            for (i = 0; i < fluxWindowSize; i++, currentChunkTime += chunkTime)
                spectralFlux.AnalyzeSpectrum(fakeSpectrum, currentChunkTime);

            // Remove the extra samples
            spectralFlux.SpectralFluxSamples.RemoveRange(0, fluxWindowSize);
            spectralFlux.SpectralFluxSamples.RemoveRange(spectralFlux.SpectralFluxSamples.Count - fluxWindowSize - 1, fluxWindowSize);

            for (i = 0; i < spectralFlux.SpectralFluxSamples.Count; i++)
            {
                var spectralFluxSample = spectralFlux.SpectralFluxSamples[i];
                if (!spectralFluxSample.IsPeak)
                    continue;

                float[] audioChunk = new float[chunkSize];
                Array.Copy(monoSamples, i * chunkSize, audioChunk, 0, chunkSize);
                double[] doubleSamples = Array.ConvertAll(audioChunk, x => (double)x);
                double rms = DSP.Analyze.FindRms(doubleSamples, 2, 2);
                double rmsSpectrum = DSP.Analyze.FindRms(Array.ConvertAll(spectrumOverTime[i], x => (double)x), 1, 1);
                double f = DSP.Analyze.FindMaxFrequency(spectrumOverTime[i], frequencySpan);
                outputMessage.Append(string.Format("\nTime: {0} | RMS: {1} | Spectrum RMS: {2} | Frequency: {3} | Spectral flux: {4} | Pruned flux: {5}",
                    string.Format("{0,7:#0.000}", spectralFluxSample.Time), string.Format("{0:0.0000}", rms),
                    string.Format("{0:0.0000}", rmsSpectrum), string.Format("{0,5:#0.00}", f),
                    string.Format("{0,6:#0.0000}", spectralFluxSample.PrunedSpectralFlux),
                    string.Format("{0:#0.0000}", spectralFluxSample.SpectralFlux)));

                if (rms >= rmsThreshold)
                {
                    // TODO: something for frequency...
                    events.Add(new TransientEvent(spectralFluxSample.Time, Mathf.Sqrt((float)rms), 0.5f));
                    outputMessage.Append($" | Transient({Mathf.Sqrt((float)rms)}, {0.5f})");
                }
            }

            Debug.Log(outputMessage.ToString());
            return events;
        }
    }
}
