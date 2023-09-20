using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chroma.Haptics.EditorWindow
{
    [Serializable]
    public class SaveOptions
    {
        public bool Overwrite;
        public bool UseJSONFormat;
        public FileFormat FileFormat;
        public string ProjectName;
        public string ProjectVersion;

        public SaveOptions() { }

        public SaveOptions(bool overwrite, bool useJSONFormat, FileFormat fileFormat)
        {
            Overwrite = overwrite;
            UseJSONFormat = useJSONFormat;
            FileFormat = fileFormat;
        }
    }

    public class SavePopup : PopupWindowContent
    {
        const string UXML_PATH = "Packages/com.chroma.ahapeditor/Editor/SavePopup.uxml";
        const string OVERWRITE_FIELD = "overwriteToggle";
        const string FORMAT_FIELD = "fileFormatEnumField";
        const string USE_JSON_FIELD = "jsonExtensionToggle";
        const string CLOSE_BUTTON = "cancelButton";
        const string SAVE_BUTTON = "saveButton";

        bool _fileInSlot;
        Action<SaveOptions> _onSave;

        public SavePopup(Action<SaveOptions> onSave)
        {
            _onSave = onSave;
        }

        public override void OnGUI(Rect rect) { }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, EditorGUIUtility.singleLineHeight * 4 +
                EditorGUIUtility.standardVerticalSpacing * 6 + 13);
        }

        public override void OnOpen()
        {
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            visualTreeAsset.CloneTree(editorWindow.rootVisualElement);

            var overwriteToggle = editorWindow.rootVisualElement.Q<Toggle>(OVERWRITE_FIELD);
            overwriteToggle.SetEnabled(_fileInSlot);
            if (!_fileInSlot)
                overwriteToggle.SetValueWithoutNotify(false);

            editorWindow.rootVisualElement.Q<Button>(CLOSE_BUTTON).clicked += editorWindow.Close;
            editorWindow.rootVisualElement.Q<Button>(SAVE_BUTTON).clicked += Save;
        }

        void Save()
        {
            SaveOptions saveOptions = new(editorWindow.rootVisualElement.Q<Toggle>(OVERWRITE_FIELD).value,
                editorWindow.rootVisualElement.Q<Toggle>(USE_JSON_FIELD).value,
                (FileFormat)editorWindow.rootVisualElement.Q<EnumField>(FORMAT_FIELD).value);

            _onSave?.Invoke(saveOptions);
            editorWindow.Close();

            Debug.Log($"Overwrite: {saveOptions.Overwrite} | As .json: {saveOptions.UseJSONFormat} | Format: {saveOptions.FileFormat}");
        }
    }
}
