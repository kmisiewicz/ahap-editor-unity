using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using DSPLib;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal class HapticsGeneratorData
    {
        public AudioClip Clip;
        public Action<List<HapticEvent>> OnHapticsGenerated;

        public static HapticsGeneratorData DummyData => new HapticsGeneratorData(null);

        public HapticsGeneratorData(AudioClip clip, Action<List<HapticEvent>> onHapticsGenerated = null)
        {
            Clip = clip; 
            OnHapticsGenerated = onHapticsGenerated;
        }
    }

    internal abstract class HapticsGenerator : PopupWindowContent
    {
        protected float _windowWidth;
        
        public override Vector2 GetWindowSize() => CalculateWindowSize();

        public override void OnGUI(Rect rect) => DrawGeneratorDataGUI(rect);

        protected Vector2 GetSize(int lineCount)
        {
            return new Vector2(_windowWidth, EditorGUIUtility.singleLineHeight * lineCount +
                EditorGUIUtility.standardVerticalSpacing * (lineCount + 2));
        }

        public abstract Vector2 CalculateWindowSize();

        public abstract void DrawGeneratorDataGUI(Rect rect);

        protected abstract void AudioToHaptics();
    }

    #region Transients from onsets

    internal class TransientsOnsetsGeneratorData : HapticsGeneratorData
    {
        public int ChunkSize;
        public float Sensitivity;
        public float RmsThreshold;

        public TransientsOnsetsGeneratorData(AudioClip clip, float sensitivity = 0.5f, float rmsThreshold = 0.5f,
            int chunkSize = 1024, Action<List<HapticEvent>> onHapticsGenerated = null) 
            : base(clip, onHapticsGenerated)
        {
            ChunkSize = chunkSize;
            Sensitivity = sensitivity;
            RmsThreshold = rmsThreshold;
        }
    }

    internal class TransientsOnsetsGenerator : HapticsGenerator
    {
        TransientsOnsetsGeneratorData _myData;

        public TransientsOnsetsGenerator(TransientsOnsetsGeneratorData data, float windowWidth)
        {
            _myData = data;
            _windowWidth = windowWidth;
        }

        public override Vector2 CalculateWindowSize() => GetSize(4);

        public override void DrawGeneratorDataGUI(Rect rect)
        {
            _myData.ChunkSize = EditorGUILayout.IntField("Chunk Size", _myData.ChunkSize);

            _myData.Sensitivity = EditorGUILayout.Slider(new GUIContent("Sensitivity"), _myData.Sensitivity, 0, 1);

            _myData.RmsThreshold = EditorGUILayout.Slider(new GUIContent("RMS Threshold"), _myData.RmsThreshold, 0, 1);

            if (GUILayout.Button("Generate"))
            {
                AudioToHaptics();
                UnityEngine.Event.current.Use();
                editorWindow.Close();
            }
        }

        protected override void AudioToHaptics()
        {
            float[] monoSamples = AudioClipUtils.GetMonoSamples(_myData.Clip);
            AudioClipUtils.CalculateSpectrum(monoSamples, _myData.Clip.frequency,
                out double[][] spectrumOverTime, out double[] frequencySpan, _myData.ChunkSize);

            StringBuilder outputMessage = new($"[Transients from onsets] ({_myData.Clip.name}):");
            SpectralFluxAnalyzer spectralFlux = new(_myData.ChunkSize, _myData.Sensitivity);

            float chunkTime = (1f / _myData.Clip.frequency) * _myData.ChunkSize;
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

                float[] audioChunk = new float[_myData.ChunkSize];
                Array.Copy(monoSamples, i * _myData.ChunkSize, audioChunk, 0, _myData.ChunkSize);
                double[] doubleSamples = Array.ConvertAll(audioChunk, x => (double)x);
                double rms = DSP.Analyze.FindRms(doubleSamples, 2, 2);
                double rmsSpectrum = DSP.Analyze.FindRms(Array.ConvertAll(spectrumOverTime[i], x => (double)x), 1, 1);
                double f = DSP.Analyze.FindMaxFrequency(spectrumOverTime[i], frequencySpan);
                outputMessage.Append(string.Format("\nTime: {0} | RMS: {1} | Spectrum RMS: {2} | Frequency: {3} | Spectral flux: {4} | Pruned flux: {5}",
                    string.Format("{0,7:#0.000}", spectralFluxSample.Time), string.Format("{0:0.0000}", rms),
                    string.Format("{0:0.0000}", rmsSpectrum), string.Format("{0,5:#0.00}", f),
                    string.Format("{0,6:#0.0000}", spectralFluxSample.PrunedSpectralFlux),
                    string.Format("{0:#0.0000}", spectralFluxSample.SpectralFlux)));

                if (rms >= _myData.RmsThreshold)
                {
                    // TODO: something for frequency...
                    events.Add(new TransientEvent(spectralFluxSample.Time, Mathf.Sqrt((float)rms), 0.5f));
                    outputMessage.Append($" | Transient({Mathf.Sqrt((float)rms)}, {0.5f})");
                }
            }

            Debug.Log(outputMessage.ToString());
            _myData.OnHapticsGenerated?.Invoke(events);
        }
    }

    #endregion

    #region

    internal class ContinuousRmsFftGeneratorData : HapticsGeneratorData
    {
        public Vector2 BandpassFilter;
        public float Simplification;
        public int RmsChunkSize;
        public int FftChunkSize;

        public ContinuousRmsFftGeneratorData(AudioClip clip, float bandpassFilterMin = 0, float simplification = 0.05f,
            float bandpassFilterMax = 20000, int rmsChunkSize = 256, int fftChunkSize = 1024,
            Action<List<HapticEvent>> onHapticsGenerated = null)
            : base(clip, onHapticsGenerated)
        {
            BandpassFilter = new Vector2(bandpassFilterMin, bandpassFilterMax);
            Simplification = simplification;
            RmsChunkSize = rmsChunkSize;
            FftChunkSize = fftChunkSize;
        }
    }

    internal class ContinuousRmsFftGenerator : HapticsGenerator
    {
        ContinuousRmsFftGeneratorData _myData;

        public ContinuousRmsFftGenerator(ContinuousRmsFftGeneratorData myData, float windowWidth)
        {
            _myData = myData;
            _windowWidth = windowWidth;
        }

        public override Vector2 CalculateWindowSize() => GetSize(6) + new Vector2(0, 3);

        public override void DrawGeneratorDataGUI(Rect rect)
        {
            EditorGUILayout.MinMaxSlider("Filter", ref _myData.BandpassFilter.x, ref _myData.BandpassFilter.y, 0, 20000);

            GUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            _myData.BandpassFilter.x = EditorGUILayout.FloatField(_myData.BandpassFilter.x);
            if (EditorGUI.EndChangeCheck())
                _myData.BandpassFilter.x = Mathf.Clamp(_myData.BandpassFilter.x, 0, _myData.BandpassFilter.x);

            EditorGUI.BeginChangeCheck();
            _myData.BandpassFilter.y = EditorGUILayout.FloatField(_myData.BandpassFilter.y);
            if (EditorGUI.EndChangeCheck())
                _myData.BandpassFilter.y = Mathf.Clamp(_myData.BandpassFilter.y, _myData.BandpassFilter.x, 20000);

            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            _myData.Simplification = EditorGUILayout.FloatField("Simplification", _myData.Simplification);

            _myData.RmsChunkSize = EditorGUILayout.IntField("RMS Chunk", _myData.RmsChunkSize);

            _myData.FftChunkSize = EditorGUILayout.IntField("FFT Chunk", _myData.FftChunkSize);

            if (GUILayout.Button("Generate"))
            {
                AudioToHaptics();
                UnityEngine.Event.current.Use();
                editorWindow.Close();
            }
        }

        protected override void AudioToHaptics()
        {
            float[] monoSamples = AudioClipUtils.GetMonoSamples(_myData.Clip);
            AudioClipUtils.CalculateSpectrum(monoSamples, _myData.Clip.frequency,
                out double[][] spectrumOverTime, out double[] frequencySpan, _myData.FftChunkSize);

            int i, minIndex = 0, maxIndex = frequencySpan.Length - 1;
            for (i = 1; i < frequencySpan.Length; i++, minIndex++)
            {
                if (_myData.BandpassFilter.x < frequencySpan[minIndex])
                    break;
            }
            for (i = maxIndex - 1; i > minIndex; i++, maxIndex--)
            {
                if (_myData.BandpassFilter.y > frequencySpan[maxIndex])
                    break;
            }
            int filteredSpanLength = maxIndex - minIndex + 1;
            double[] filteredSpan = new double[filteredSpanLength];
            Array.Copy(frequencySpan, minIndex, filteredSpan, 0, filteredSpanLength);

            List<Vector2> rmsPoints = new();
            List<Vector2> frequencyPoints = new();

            float currentChunkTime = 0f;
            float rmsChunkTime = 1f / _myData.Clip.frequency * _myData.RmsChunkSize;
            int iterations = _myData.Clip.samples / _myData.RmsChunkSize;
            float[] audioChunk = new float[_myData.RmsChunkSize];
            for (i = 0; i < iterations; i++, currentChunkTime += rmsChunkTime)
            {
                Array.Copy(monoSamples, i * _myData.RmsChunkSize, audioChunk, 0, _myData.RmsChunkSize);
                double rms = DSP.Analyze.FindRms(Array.ConvertAll(audioChunk, x => (double)x), 1, 1);
                rmsPoints.Add(new Vector2(currentChunkTime, (float)rms));
            }

            currentChunkTime = 0f;
            float frequencyChunkTime = 1f / _myData.Clip.frequency * _myData.FftChunkSize;
            for (i = 0; i < spectrumOverTime.Length; i++, currentChunkTime += frequencyChunkTime)
            {

                double[] filteredSpectrum = new double[filteredSpanLength];
                Array.Copy(spectrumOverTime[i], minIndex, filteredSpectrum, 0, filteredSpanLength);
                double f = DSP.Analyze.FindMaxFrequency(filteredSpectrum, filteredSpan);
                f = (f - _myData.BandpassFilter.x) / (_myData.BandpassFilter.y - _myData.BandpassFilter.x);
                frequencyPoints.Add(new Vector2(currentChunkTime, (float)f));
            }

            List<Vector2> simplifiedRMSPoints = new();
            List<Vector2> simplifiedFrequencyPoints = new();
            LineUtility.Simplify(rmsPoints, _myData.Simplification, simplifiedRMSPoints);
            LineUtility.Simplify(frequencyPoints, _myData.Simplification, simplifiedFrequencyPoints);
            ContinuousEvent ev = new();
            ev.IntensityCurve = new List<EventPoint>();
            simplifiedRMSPoints.ForEach(point => ev.IntensityCurve.Add(new EventPoint(point.x, point.y, ev)));
            ev.SharpnessCurve = new List<EventPoint>();
            simplifiedFrequencyPoints.ForEach(point => ev.SharpnessCurve.Add(new EventPoint(point.x, point.y, ev)));
            List<HapticEvent> events = new() { ev };

            _myData.OnHapticsGenerated?.Invoke(events);
        }
    }

    #endregion
}
