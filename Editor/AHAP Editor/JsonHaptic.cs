using System.Collections.Generic;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal class Amplitude
    {
        public double time { get; set; }
        public double amplitude { get; set; }
        public Emphasis emphasis { get; set; } = null;

        public Amplitude() { }

        public Amplitude(double time, double amplitude)
        {
            this.time = time;
            this.amplitude = amplitude;
        }
    }

    internal class Continuous
    {
        public Envelopes envelopes { get; set; } = new();
    }

    internal class Emphasis
    {
        public double amplitude { get; set; }
        public double frequency { get; set; }

        public Emphasis() { }

        public Emphasis(double amplitude, double frequency)
        {
            this.amplitude = amplitude;
            this.frequency = frequency;
        }
    }

    internal class Envelopes
    {
        public List<Amplitude> amplitude { get; set; }
        public List<Frequency> frequency { get; set; }
    }

    internal class Frequency
    {
        public double time { get; set; }
        public double frequency { get; set; }

        public Frequency() { }

        public Frequency(double time, double frequency) 
        {
            this.time = time;
            this.frequency = frequency;
        }
    }

    internal class HapticMetadata
    {
        public string author { get; set; } = "";
        public string editor { get; set; } = "";
        public string source { get; set; } = "";
        public string project { get; set; } = "";
        public List<string> tags { get; set; } = new();
        public string description { get; set; } = "";
    }

    internal class JsonHaptic
    {
        public Version version { get; set; } = new();
        public HapticMetadata metadata { get; set; } = new();
        public Signals signals { get; set; } = new();

        public JsonHaptic() { }
    }

    internal class Signals
    {
        public Continuous continuous { get; set; } = new();
    }

    internal class Version
    {
        public int major { get; set; } = 1;
        public int minor { get; set; }
        public int patch { get; set; }

        public Version() { }

        public Version(int major = 1, int minor = 0, int patch = 0)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
        }
    }
}
