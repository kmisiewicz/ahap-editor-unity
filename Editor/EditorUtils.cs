using System;
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

        public static bool ConfirmDialog(string message = "Are you sure you want to perform this operation?", 
            string title = "Confirm", string ok = "Yes", string cancel = "No", Action onOk = null, Action onCancel = null)
        {
            if (EditorUtility.DisplayDialog(title, message, ok, cancel))
            {
                onOk?.Invoke();
                return true;
            }
            
            onCancel?.Invoke();
            return false;
        }

        /// <summary>
        /// Draws <see cref="EditorGUILayout.FloatField"/> with <see cref="EditorGUIUtility.labelWidth"/> 
        /// calculated with <paramref name="content"/>, adjusted by <paramref name="labelWidthOffset"/>. 
        /// Resets <see cref="EditorGUIUtility.labelWidth"/> after.
        /// </summary>
        /// <returns>The value entered by the user.</returns>
        public static float LabelLayoutFloatField(GUIContent content, float labelWidthOffset, float value, params GUILayoutOption[] options)
        {
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(content).x + labelWidthOffset;
            float returnValue = EditorGUILayout.FloatField(content, value, options);
            EditorGUIUtility.labelWidth = 0;
            return returnValue;
        }
    }
}
