using System.Collections.Generic;
using UnityEngine;

namespace Chroma.Haptics.EditorWindow.Generation
{
    public interface IHapticsGenerator
    {
        public List<HapticEvent> AudioToHaptics(AudioClip clip);
    }
}
