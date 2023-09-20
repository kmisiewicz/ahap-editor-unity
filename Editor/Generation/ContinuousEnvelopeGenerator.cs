using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DSPLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chroma.Haptics.EditorWindow.Generation
{
    public class ContinuousEnvelopeGenerator : PopupWindowContent, IHapticsGenerator
    {
        const string UXML_PATH = "Packages/com.chroma.ahapeditor/Editor/Generation/ContinuousEnvelopeGenerator.uxml";
        const string NORMALIZE_TOGGLE = "normalizeToggle";
        const string TOLERANCE_FIELD = "toleranceField";
        const string RMS_CHUNK_FIELD = "rmsChunkField";
        const string FILTER_SLIDER = "filterSlider";
        const string FILTER_MIN_FIELD = "filterMinField";
        const string FILTER_MAX_FIELD = "filterMaxField";
        const string FFT_CHUNK_FIELD = "fftChunkField";
        const string MULTIPLY_BY_RMS_TOGGLE = "multiplyRmsToggle";
        const string LERP_TO_RMS_FIELD = "lerpRmsField";
        const string GENERATE_BUTTON = "generateButton";

        AudioClip _audioClip;
        Action<List<HapticEvent>> _onEventsGenerated;

        public ContinuousEnvelopeGenerator(AudioClip clip, Action<List<HapticEvent>> onEventsGenerated)
        {
            _audioClip = clip;
            _onEventsGenerated = onEventsGenerated;
        }

        public override void OnGUI(Rect rect) { }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, EditorGUIUtility.singleLineHeight * 12 +
                EditorGUIUtility.standardVerticalSpacing * 14 + 3 * 13);
        }

        public override void OnOpen()
        {
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            visualTreeAsset.CloneTree(editorWindow.rootVisualElement);

            editorWindow.rootVisualElement.Q<MinMaxSlider>(FILTER_SLIDER).RegisterValueChangedCallback(OnSliderChanged);
            editorWindow.rootVisualElement.Q<UnsignedIntegerField>(FILTER_MIN_FIELD).RegisterValueChangedCallback(OnFilterMinValueChanged);
            editorWindow.rootVisualElement.Q<UnsignedIntegerField>(FILTER_MAX_FIELD).RegisterValueChangedCallback(OnFilterMaxValueChanged);
            editorWindow.rootVisualElement.Q<Button>(GENERATE_BUTTON).clicked += Generate;
        }

        void Generate()
        {
            var events = AudioToHaptics(_audioClip);
            _onEventsGenerated?.Invoke(events);
        }

        void OnSliderChanged(ChangeEvent<Vector2> changeEvent)
        {
            Vector2 rounded = changeEvent.newValue;
            rounded.Set(Mathf.Round(rounded.x), Mathf.Round(rounded.y));
            ((MinMaxSlider)changeEvent.target).SetValueWithoutNotify(rounded);
            editorWindow.rootVisualElement.Q<UnsignedIntegerField>(FILTER_MIN_FIELD).SetValueWithoutNotify((uint)rounded.x);
            editorWindow.rootVisualElement.Q<UnsignedIntegerField>(FILTER_MAX_FIELD).SetValueWithoutNotify((uint)rounded.y);
        }

        void OnFilterMinValueChanged(ChangeEvent<uint> changeEvent)
        {
            var filterSlider = editorWindow.rootVisualElement.Q<MinMaxSlider>(FILTER_SLIDER);
            uint clamped = (uint)Mathf.Clamp(changeEvent.newValue, filterSlider.lowLimit, filterSlider.maxValue);
            ((UnsignedIntegerField)changeEvent.target).SetValueWithoutNotify(clamped);
            filterSlider.SetValueWithoutNotify(new Vector2(clamped, filterSlider.maxValue));
        }

        void OnFilterMaxValueChanged(ChangeEvent<uint> changeEvent)
        {
            var filterSlider = editorWindow.rootVisualElement.Q<MinMaxSlider>(FILTER_SLIDER);
            uint clamped = (uint)Mathf.Clamp(changeEvent.newValue, filterSlider.minValue, filterSlider.highLimit);
            ((UnsignedIntegerField)changeEvent.target).SetValueWithoutNotify(clamped);
            filterSlider.SetValueWithoutNotify(new Vector2(filterSlider.minValue, clamped));
        }

        public List<HapticEvent> AudioToHaptics(AudioClip clip)
        {
            bool normalizeAudio = editorWindow.rootVisualElement.Q<Toggle>(NORMALIZE_TOGGLE).value;
            int rmsChunkSize = (int)editorWindow.rootVisualElement.Q<UnsignedIntegerField>(RMS_CHUNK_FIELD).value;
            int fftChunkSize = (int)editorWindow.rootVisualElement.Q<UnsignedIntegerField>(FFT_CHUNK_FIELD).value;
            Vector2 bandpassFilter = editorWindow.rootVisualElement.Q<MinMaxSlider>(FILTER_SLIDER).value;
            float simplification = editorWindow.rootVisualElement.Q<FloatField>(TOLERANCE_FIELD).value;
            float lerpToRms = editorWindow.rootVisualElement.Q<Slider>(LERP_TO_RMS_FIELD).value;
            bool multiplyByRms = editorWindow.rootVisualElement.Q<Toggle>(MULTIPLY_BY_RMS_TOGGLE).value;

            StringBuilder outputMessage = new($"[Continuous envelopes] ({clip.name}):\n");
            float[] monoSamples = AudioClipUtils.GetMonoSamples(clip, normalizeAudio);
            AudioClipUtils.CalculateSpectrum(monoSamples, clip.frequency,
                out double[][] spectrumOverTime, out double[] frequencySpan, fftChunkSize);

            int i, minIndex = 0, maxIndex = frequencySpan.Length - 1;
            for (i = 1; i < frequencySpan.Length; i++, minIndex++)
            {
                if (bandpassFilter.x < frequencySpan[minIndex])
                    break;
            }
            for (i = maxIndex - 1; i > minIndex; i++, maxIndex--)
            {
                if (bandpassFilter.y > frequencySpan[maxIndex])
                    break;
            }
            int filteredSpanLength = maxIndex - minIndex + 1;
            double[] filteredSpan = new double[filteredSpanLength];
            Array.Copy(frequencySpan, minIndex, filteredSpan, 0, filteredSpanLength);

            List<Vector2> rmsPoints = new();
            List<Vector2> frequencyPoints = new();

            float currentChunkTime = 0f;
            float rmsChunkTime = 1f / clip.frequency * rmsChunkSize;
            int iterations = clip.samples / rmsChunkSize;
            double[] audioChunk = new double[rmsChunkSize];
            for (i = 0; i < iterations; i++, currentChunkTime += rmsChunkTime)
            {
                Array.Copy(monoSamples, i * rmsChunkSize, audioChunk, 0, rmsChunkSize);
                double rms = DSP.Analyze.FindRms(audioChunk, 1, 1);
                rmsPoints.Add(new Vector2(currentChunkTime, Mathf.Clamp01((float)rms)));
            }

            currentChunkTime = 0f;
            float frequencyChunkTime = 1f / clip.frequency * fftChunkSize;
            double[] filteredSpectrum = new double[filteredSpanLength];
            audioChunk = new double[fftChunkSize];
            for (i = 0; i < spectrumOverTime.Length; i++, currentChunkTime += frequencyChunkTime)
            {
                Array.Copy(spectrumOverTime[i], minIndex, filteredSpectrum, 0, filteredSpanLength);
                double f = DSP.Analyze.FindMaxFrequency(filteredSpectrum, filteredSpan);
                f = (f - bandpassFilter.x) / (bandpassFilter.y -bandpassFilter.x);
                if (lerpToRms > 0 || multiplyByRms)
                {
                    Array.Copy(monoSamples, i * fftChunkSize, audioChunk, 0, fftChunkSize);
                    double rms = DSP.Analyze.FindRms(audioChunk, 1, 1);
                    if (multiplyByRms)
                        f *= rms;
                    f = (double)Mathf.Lerp((float)f, (float)rms, lerpToRms);
                }
                frequencyPoints.Add(new Vector2(currentChunkTime, Mathf.Clamp01((float)f)));
            }

            List<Vector2> simplifiedRmsPoints = new();
            LineUtility.Simplify(rmsPoints, simplification, simplifiedRmsPoints);
            if (simplifiedRmsPoints.Count < 2)
            {
                simplifiedRmsPoints = rmsPoints;
                outputMessage.AppendLine("Simplification error, returning full list of RMS points.");
            }
            else
            {
                if (simplifiedRmsPoints[0].x != rmsPoints[0].x)
                    simplifiedRmsPoints.Insert(0, rmsPoints[0]);
                if (simplifiedRmsPoints[^1].x != rmsPoints[^1].x)
                    simplifiedRmsPoints.Add(rmsPoints[^1]);
            }

            List<Vector2> simplifiedFrequencyPoints = new();
            LineUtility.Simplify(frequencyPoints, simplification, simplifiedFrequencyPoints);
            if (simplifiedFrequencyPoints.Count < 2)
            {
                simplifiedFrequencyPoints = frequencyPoints;
                outputMessage.AppendLine("Simplification error, returning full list of frequency points.");
            }
            else
            {
                if (simplifiedFrequencyPoints[0].x != frequencyPoints[0].x)
                    simplifiedFrequencyPoints.Insert(0, frequencyPoints[0]);
                if (simplifiedFrequencyPoints[^1].x != frequencyPoints[^1].x)
                    simplifiedFrequencyPoints.Add(frequencyPoints[^1]);
            }

            if (simplifiedRmsPoints[^1].x > simplifiedFrequencyPoints[^1].x)
                simplifiedFrequencyPoints.Add(new Vector2(simplifiedRmsPoints[^1].x, simplifiedFrequencyPoints[^1].y));
            else if (simplifiedFrequencyPoints[^1].x > simplifiedRmsPoints[^1].x)
                simplifiedRmsPoints.Add(new Vector2(simplifiedFrequencyPoints[^1].x, simplifiedRmsPoints[^1].y));

            outputMessage.AppendLine($"RMS points: {rmsPoints.Count} => Simplified: {simplifiedRmsPoints.Count}");
            outputMessage.AppendLine($"Frequency points: {frequencyPoints.Count} => Simplified: {simplifiedFrequencyPoints.Count}");

            ContinuousEvent ev = new() { IntensityCurve = new List<EventPoint>(), SharpnessCurve = new List<EventPoint>() };
            simplifiedRmsPoints.ForEach(point => ev.IntensityCurve.Add(new EventPoint(point.x, point.y, ev)));
            simplifiedFrequencyPoints.ForEach(point => ev.SharpnessCurve.Add(new EventPoint(point.x, point.y, ev)));
            List<HapticEvent> events = new() { ev };

            Debug.Log(outputMessage.ToString());
            return events;
        }        
    }
}
