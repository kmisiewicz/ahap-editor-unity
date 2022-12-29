using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal class Amplitude
    {
        public double time;
        public double amplitude;
        public Emphasis emphasis;
    }

    internal class Continuous
    {
        public Envelopes envelopes;
    }

    internal class Emphasis
    {
        public double amplitude;
        public double frequency;
    }

    internal class Envelopes
    {
        public List<Amplitude> amplitude;
        public List<Frequency> frequency;
    }

    internal class Frequency
    {
        public double time;
        public double frequency;
    }

    internal class HapticMetadata
    {
        public string author;
        public string editor;
        public string source;
        public string project;
        public List<object> tags;
        public string description;
    }

    internal class HapticFile
    {
        public Version version;
        public HapticMetadata metadata;
        public Signals signals;
    }

    internal class Signals
    {
        public Continuous continuous;
    }

    internal class Version
    {
        public int major;
        public int minor;
        public int patch;
    }
}
