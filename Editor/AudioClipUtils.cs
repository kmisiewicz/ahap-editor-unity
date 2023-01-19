using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DSPLib;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal class AudioClipUtils
    {
        // Adjusted code from answers
        // https://answers.unity.com/questions/699595/how-to-generate-waveform-from-audioclip.html
        public static Texture2D PaintAudioWaveform(AudioClip audio, int width, int height, Color backgroundColor, Color waveformColor, bool normalize = false)
        {
            // Calculate samples
            float[] samples = new float[audio.samples * audio.channels];
            float[] waveform = new float[width];
            audio.GetData(samples, 0);
            int packSize = (samples.Length / width) + 1;
            for (int i = 0, s = 0; i < samples.Length; i += packSize, s++)
                waveform[s] = Mathf.Abs(samples[i]);
            if (normalize)
            {
                float maxValue = waveform.Max();
                for (int x = 0; x < width; x++)
                    waveform[x] /= maxValue;
            }

            // Paint waveform
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(backgroundColor, width * height).ToArray());
            for (int x = 0; x < width; x++)
                for (int y = 0; y <= waveform[x] * height; y++)
                    texture.SetPixel(x, y, waveformColor);
            texture.Apply();

            return texture;
        }

        // Sound playing from editor
        // https://forum.unity.com/threads/way-to-play-audio-in-editor-using-an-editor-script.132042/#post-7015753
        public static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
        {
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

        public static float[] GetMonoSamples(AudioClip clip)
        {
            float[] multiChannelSamples = new float[clip.channels * clip.samples];
            clip.GetData(multiChannelSamples, 0);
            if (clip.channels == 1)
                return multiChannelSamples;

            float[] monoSamples = new float[clip.samples];
            int numProcessed = 0;
            float combinedChannelAverage = 0f;
            for (int i = 0; i < multiChannelSamples.Length; i++)
            {
                combinedChannelAverage += multiChannelSamples[i];

                if ((i + 1) % clip.channels == 0) // Average all channels sum
                {
                    monoSamples[numProcessed] = combinedChannelAverage / clip.channels;
                    numProcessed++;
                    combinedChannelAverage = 0f;
                }
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
