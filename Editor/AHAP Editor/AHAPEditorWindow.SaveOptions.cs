using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow
    {
        private class SaveOptionsWindow : PopupWindowContent
        {
            Action<bool, bool, DataFormat, FileFormat> _onSaveClicked;
            bool _fileInSlot;

            bool _overwrite;
            bool _saveAsJson;
            DataFormat _dataFormat;
            FileFormat _fileFormat;

            public SaveOptionsWindow(Action<bool, bool, DataFormat, FileFormat> onSaveClicked, UnityEngine.Object asset = null) : base()
            {
                _onSaveClicked = onSaveClicked;
                if (asset != null)
                {
                    _fileInSlot = _overwrite = true;
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    string extension = Path.GetExtension(assetPath);
                    _fileFormat = extension switch
                    {
                        ".haptic" => FileFormat.Haptic,
                        _ => FileFormat.AHAP,
                    };
                }
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(Screen.currentResolution.height * TOP_BAR_OPTIONS_SIZE_FACTOR,
                    EditorGUIUtility.singleLineHeight * 5 + EditorGUIUtility.standardVerticalSpacing * 7);
            }

            public override void OnGUI(Rect rect)
            {
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.dataFormatLabel).x + LABEL_WIDTH_OFFSET;

                GUI.enabled = _fileInSlot;
                _overwrite = EditorGUILayout.Toggle(Content.overwriteLabel, _overwrite);
                GUI.enabled = true;

                _saveAsJson = GUILayout.Toggle(_saveAsJson, Content.saveAsJsonLabel, GUI.skin.button);

                _dataFormat = (DataFormat)EditorGUILayout.EnumPopup(Content.dataFormatLabel, _dataFormat);

                _fileFormat = (FileFormat)EditorGUILayout.EnumPopup(Content.fileFormatLabel, _fileFormat);

                GUILayout.BeginHorizontal();
                bool shouldClose = false;
                if (GUILayout.Button(Content.cancelLabel))
                    shouldClose = true;
                if (GUILayout.Button(Content.saveLabel))
                {
                    _onSaveClicked?.Invoke(_overwrite, _saveAsJson, _dataFormat, _fileFormat);
                    shouldClose = true;
                }
                GUILayout.EndHorizontal();

                if (shouldClose)
                {
                    UnityEngine.Event.current.Use();
                    editorWindow.Close();
                }
            }
        }
    }
}
