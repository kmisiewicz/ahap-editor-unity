using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal class AudioClipUtils
    {
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
    }
}
