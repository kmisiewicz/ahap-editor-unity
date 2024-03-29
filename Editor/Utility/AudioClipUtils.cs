using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DSPLib;

namespace Chroma.Haptics.EditorWindow
{
    internal static class AudioClipUtils
    {
        // Adjusted code from answers
        // https://answers.unity.com/questions/699595/how-to-generate-waveform-from-audioclip.html
        public static Texture2D PaintAudioWaveform(AudioClip audio, int width, int height, Color backgroundColor, 
            Color waveformColor, bool normalize = false, float heightMultiplier = 1f)
        {
            if (width <= 0 || height <= 0)
                return null;

            // Calculate samples
            float[] samples = GetMonoSamples(audio, normalize);
            float[] waveform = new float[width];
            float chunkSize = samples.Length / (float)width;
            int i;
            for (i = 0; i < width; i++)
            {
                int index = Mathf.Clamp(Mathf.RoundToInt(i * chunkSize), 0, samples.Length);
                waveform[i] = Mathf.Abs(samples[index]);
            }

            // Paint waveform
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(Enumerable.Repeat((Color32)backgroundColor, width * height).ToArray());
            for (int x = 0; x < width; x++)
            {
                int maxY = (int)(Mathf.Clamp01(waveform[x] * heightMultiplier) * height);
                for (int y = 0; y <= maxY; y++)
                    texture.SetPixel(x, y, waveformColor);
            }
            texture.Apply();

            return texture;
        }

        // Sound playing from editor
        // https://forum.unity.com/threads/way-to-play-audio-in-editor-using-an-editor-script.132042/#post-7015753
        public static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
        {
            if (clip == null) 
                return;

            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            method.Invoke(null, new object[] { clip, startSample, loop });
        }

        public static void StopAllClips()
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { },
                null
            );

            method.Invoke(null, new object[] { });
        }

        public static float[] GetMonoSamples(AudioClip clip, bool normalize = false)
        {
            int multiChannelSampleCount = clip.channels * clip.samples;
            float[] multiChannelSamples = new float[multiChannelSampleCount];
            clip.GetData(multiChannelSamples, 0);

            float[] monoSamples = new float[clip.samples];
            int numProcessed = 0;
            float combinedChannelAverage = 0f;
            float maxValue = 0;
            for (int i = 0; i < multiChannelSampleCount; i++)
            {
                combinedChannelAverage += multiChannelSamples[i];

                if ((i + 1) % clip.channels == 0) // Average all channels sum
                {
                    monoSamples[numProcessed] = combinedChannelAverage / clip.channels;
                    maxValue = Mathf.Max(maxValue, monoSamples[numProcessed]);
                    numProcessed++;
                    combinedChannelAverage = 0f;
                }
            }

            if (normalize)
            {
                for (int i = 0; i < monoSamples.Length; i++)
                    monoSamples[i] /= maxValue;
            }

            return monoSamples;
        }

        public static void CalculateSpectrum(AudioClip clip, out double[][] spectrum, out double[] frequencySpan, int chunkSize = 1024)
        {
            float[] monoSamples = GetMonoSamples(clip);
            CalculateSpectrum(monoSamples, clip.frequency, out spectrum, out frequencySpan, chunkSize);
        }

        public static void CalculateSpectrum(float[] monoSamples, double samplingFrequencyHz,
            out double[][] spectrum, out double[] frequencySpan, int chunkSize = 1024)
        {
            if (!((chunkSize != 0) && ((chunkSize & (chunkSize - 1)) == 0)))
            {
                Debug.LogError("Chunk size must be power of 2 > 0");
                spectrum = Array.Empty<double[]>();
                frequencySpan = Array.Empty<double>();
                return;
            }

            FFT fft = new();
            fft.Initialize((UInt32)chunkSize);
            frequencySpan = fft.FrequencySpan(samplingFrequencyHz);
            int iterations = monoSamples.Length / chunkSize;
            spectrum = new double[iterations][];
            double[] sampleChunk = new double[chunkSize];
            for (int i = 0; i < iterations; i++)
            {
                // Grab the current chunk of audio sample data
                Array.Copy(monoSamples, i * chunkSize, sampleChunk, 0, chunkSize);

                // Apply FFT Window
                double[] windowCoefs = DSP.Window.Coefficients(DSP.Window.Type.Hanning, (uint)chunkSize);
                double[] scaledSpectrumChunk = DSP.Math.Multiply(sampleChunk, windowCoefs);
                double scaleFactor = DSP.Window.ScaleFactor.Signal(windowCoefs);

                // Perform the FFT and convert output (complex numbers) to Magnitude
                System.Numerics.Complex[] fftSpectrum = fft.Execute(scaledSpectrumChunk);
                double[] scaledFFTSpectrum = DSP.ConvertComplex.ToMagnitude(fftSpectrum);
                scaledFFTSpectrum = DSP.Math.Multiply(scaledFFTSpectrum, scaleFactor);
                spectrum[i] = scaledFFTSpectrum;
            }
        }
    }
}
