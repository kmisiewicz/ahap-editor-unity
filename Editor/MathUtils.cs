using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal static class MathUtils
    {
        public static int FindNextPowerOf2(int value)
        {
            if (value <= 2)
                return 2;

            value--;
            while ((value & (value - 1)) != 0)
                value &= (value - 1);

            return value << 1;
        }
    }
}
