using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal static class EditorUtils
    {
        public static void DrawRectWithBorder(Rect rect, float borderWidth, Color fillColor, Color borderColor)
        {
            EditorGUI.DrawRect(rect, fillColor);
            DrawRectBorder(rect, borderWidth, borderColor);
        }

        public static void DrawRectBorder(Rect rect, float borderWidth, Color borderColor)
        {
            Handles.color = borderColor;
            Handles.DrawAAPolyLine(borderWidth, rect.position, new Vector3(rect.xMax, rect.y),
                    rect.max, new Vector3(rect.x, rect.yMax), rect.position);
        }
    }
}
