using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SavePopup : PopupWindowContent
{
    public override void OnGUI(Rect rect) { }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(300, EditorGUIUtility.singleLineHeight * 4 +
            EditorGUIUtility.standardVerticalSpacing * 6 + 13);
    }

    public override void OnOpen()
    {
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.chroma.ahapeditor/Editor/SavePopup.uxml");
        visualTreeAsset.CloneTree(editorWindow.rootVisualElement);
    }
}
