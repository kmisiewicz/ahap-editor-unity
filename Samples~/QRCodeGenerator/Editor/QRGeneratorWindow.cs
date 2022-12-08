using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class QRGeneratorWindow : EditorWindow
{
    const int TEXTURE_SIZE = 256;
    const float TEXT_AREA_HEIGHT = 80;
    const float TEXTURE_MARGIN = 5;

    string _input;
    Texture2D _output;

    [MenuItem("Window/QR Code Generator")]
    public static void OpenWindow()
    {
        QRGeneratorWindow window = GetWindow<QRGeneratorWindow>("QR Code Generator");
        window.ResetState();
        //window.ShowModalUtility();
    }

    private void ResetState()
    {
        _input = string.Empty;
        _output = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE);
        _output.SetPixels(Enumerable.Repeat(Color.white, TEXTURE_SIZE * TEXTURE_SIZE).ToArray());
        _output.Apply();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);

        bool previousWordWrapState = EditorStyles.textField.wordWrap;
        EditorStyles.textField.wordWrap = true;
        _input = EditorGUILayout.TextArea(_input, GUILayout.Height(TEXT_AREA_HEIGHT));
        EditorStyles.textField.wordWrap = previousWordWrapState;

        if (GUILayout.Button("Generate"))
            QRCodeGenerator.GenerateQRCode(ref _output, _input);

        GUILayout.Space(EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        float topOffset = TEXT_AREA_HEIGHT + EditorGUIUtility.singleLineHeight * 4 + EditorGUIUtility.standardVerticalSpacing * 7;
        float bottomOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2;
        //float textureHeight = position.height - topOffset - bottomOffset;
        Rect textureRect = new(TEXTURE_MARGIN, topOffset, position.width - TEXTURE_MARGIN * 2,
            position.height - topOffset - bottomOffset);
        GUI.DrawTexture(textureRect, _output, ScaleMode.ScaleToFit);

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField("Info:");
    }
}
