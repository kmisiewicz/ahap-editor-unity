using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public class AHAPEditorWindow : EditorWindow
	{
        private enum SnapMode 
        { 
            None = 0,
            [InspectorName("0.1")] Tenth = 1,
            [InspectorName("0.01")] Hundredth = 2,
            [InspectorName("0.001")] Thousandth = 3 
        }

        private enum PointDragMode { FreeMove = 0, LockTime = 1, LockValue = 2 }

        private enum MouseState { Unclicked = 0, MouseDown = 1, MouseDrag = 2 }

        private enum MouseButton { Left = 0, Right = 1, Middle = 2 }

        // Const settings
        const float MIN_TIME = 0.1f;
        const float MAX_TIME = 30f;
        const float HOVER_OFFSET = 5f;
        const float ZOOM_INCREMENT = 0.1f;
        const float MAX_ZOOM = 7f;
        const float SCROLL_INCREMENT = 0.05f;
        const float HOVER_HIGHLIGHT_SIZE = 10f;
        const float HOVER_DOT_SIZE = 3f;
        const float PLOT_EVENT_POINT_SIZE = 5f;
        const float PLOT_EVENT_LINE_WIDTH = 4f;
        const float PLOT_BORDER_WIDTH = 5f;
        const float NEIGHBOURING_POINT_OFFSET = 0.001f;
        const float MIN_WAVEFORM_RENDER_SCALE = 0.1f;
        const float MAX_WAVEFORM_RENDER_SCALE = 2f;
        const float CUSTOM_LABEL_WIDTH_OFFSET = 3;

        static readonly Vector3 POINT_NORMAL = new(0, 0, 1);
        static readonly Vector2 MARGIN = new(3, 2);

        // Colors
        static readonly Color COLOR_PLOT_BORDER = Color.white;
        static readonly Color COLOR_PLOT_GRID = Color.gray;
        static readonly Color COLOR_WAVEFORM = new(0.42f, 1f, 0f, 0.2f);
        static readonly Color COLOR_WAVEFORM_BG = Color.clear;
        static readonly Color COLOR_EVENT_TRANSIENT = new(0.22f, 0.6f, 1f);
        static readonly Color COLOR_EVENT_CONTINUOUS = new(1f, 0.6f, 0.2f);
        static readonly Color COLOR_EVENT_CONTINUOUS_CREATION = new(1f, 0.6f, 0.2f, 0.5f);
        static readonly Color COLOR_HOVER_POINT = new(0.8f, 0.8f, 0.8f, 0.2f);
        static readonly Color COLOR_HOVER_GUIDES = new(0.7f, 0f, 0f);

        // Data
        TextAsset ahapFile;
        AudioClip audioClip;
		float zoom = 1f;
        float time = 1f;        
        string projectName;

        List<VibrationEvent> events = new();
        Vector2 scrollPosition = Vector2.zero;
        Vector2 plotSize = Vector2.one;
        float visiblePlotWidth = 1f;
        PointDragMode pointDragMode = PointDragMode.FreeMove;
        SnapMode snapMode = SnapMode.None;
        MouseState mouseState = MouseState.Unclicked;
        MouseLocation mouseLocation = MouseLocation.Outside;
        MouseLocation mouseClickLocation = MouseLocation.Outside;        
        Vector2 mouseClickPlotPosition;
        float continuousEventWindowPos;
        float sharpnessPlotHeightOffset;
        EventPoint hoverPoint = null;
        VibrationEvent hoverPointEvent = null;
        EventPoint draggedPoint = null;
        VibrationEvent draggedPointEvent = null;
        float dragMin, dragMax;
        
        // Audio waveform
        bool audioWaveformVisible = false;
        float lastAudioClipPaintedZoom = 1f;
        Texture2D audioClipTexture;
        string lastAudioClipName = "None";
        bool normalize, wasNormalized;
        float renderScale = 1f;


        [MenuItem("Window/AHAP Editor")]
        public static void OpenWindow()
        {
            AHAPEditorWindow window = GetWindow<AHAPEditorWindow>("AHAP Editor");
            var content = EditorGUIUtility.IconContent("d_HoloLensInputModule Icon", "AHAP Editor");
            content.text = "AHAP Editor";
            window.titleContent = content;
            window.Clear();
        }

        private void Clear()
        {
            if (events != null) events.Clear();
            else events = new List<VibrationEvent>();
            time = 1f;
            zoom = 1f;
        }

        private void OnGUI()
		{
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float lineSpacing = EditorGUIUtility.standardVerticalSpacing;
            float lineWithSpacing = lineHeight + lineSpacing;
            float lineHalfHeight = lineHeight / 2f;

            float topBarHeight = lineWithSpacing * 3 + lineHalfHeight + lineSpacing * 2;
            Rect topBarRect = new(MARGIN, new Vector2(position.width - MARGIN.x * 2, topBarHeight));
            //EditorGUI.DrawRect(topBarRect, Color.blue); //debug
            float topBarOptionsContainerWidth = 0.12f * Screen.currentResolution.width;
            var topBarMaxWidthOption = GUILayout.MaxWidth(topBarOptionsContainerWidth);

            float bottomPartHeight = position.height - MARGIN.y * 2 - topBarHeight - lineHalfHeight - lineSpacing;
            Rect bottomPartRect = new(MARGIN.x, MARGIN.y + topBarHeight + lineHalfHeight, topBarRect.width, bottomPartHeight);
            //EditorGUI.DrawRect(bottomPartRect, Color.black); //debug

            float plotAreaWidth = bottomPartRect.width * 0.8f;
            Rect plotAreaRect = new(bottomPartRect.position, new Vector2(plotAreaWidth, bottomPartRect.height));
            //EditorGUI.DrawRect(plotAreaRect, Color.yellow); //debug

            float pointEditAreaWidth = bottomPartRect.width - plotAreaWidth - lineSpacing * 2;
            Rect pointEditAreaRect = new Rect(bottomPartRect.position + new Vector2(plotAreaRect.width + lineSpacing * 2, 0),
                new Vector2(pointEditAreaWidth, bottomPartHeight));
            //EditorGUI.DrawRect(pointEditAreaRect, Color.green); //debug

            #region Zoom and scroll handling (mouse wheel)

            var currentEvent = UnityEngine.Event.current;
            if (currentEvent.type == EventType.ScrollWheel)
            {
                if (currentEvent.control)
                {
                    zoom += currentEvent.delta.y < 0 ? ZOOM_INCREMENT : -ZOOM_INCREMENT;
                    zoom = Mathf.Clamp(zoom, 1, MAX_ZOOM);
                }
                else if (zoom > 1f)
                {
                    scrollPosition.x += plotSize.x * (currentEvent.delta.y > 0 ? SCROLL_INCREMENT : -SCROLL_INCREMENT);
                    scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0, plotSize.x - visiblePlotWidth);
                }
                currentEvent.Use();
            }

            #endregion

            #region Top Bar

            GUILayout.BeginArea(topBarRect, EditorStyles.helpBox);
            GUILayout.BeginHorizontal();

            // File
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("File", EditorStyles.boldLabel);
            ahapFile = EditorGUILayout.ObjectField(GUIContent.none, ahapFile, typeof(TextAsset), false) as TextAsset;
            GUILayout.BeginHorizontal();
            GUI.enabled = ahapFile != null;
            if (GUILayout.Button("Import"))
                HandleImport();
            GUI.enabled = true;
            if (GUILayout.Button("Save"))
                HandleSaving();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Audio waveform
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("Reference waveform", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            audioClip = EditorGUILayout.ObjectField(GUIContent.none, audioClip, typeof(AudioClip), false, GUILayout.MinWidth(topBarOptionsContainerWidth * 0.25f)) as AudioClip;
            if (EditorGUI.EndChangeCheck())
            {
                AudioClipUtils.StopAllClips();
                audioWaveformVisible = false;
            }
            GUI.enabled = audioClip != null;
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"), GUILayout.MaxWidth(30)))
                AudioClipUtils.PlayClip(audioClip);
            GUILayout.FlexibleSpace();
            var waveformVisibleLabel = new GUIContent("Visible");
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(waveformVisibleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            audioWaveformVisible = EditorGUILayout.Toggle(waveformVisibleLabel, audioWaveformVisible);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (!audioWaveformVisible)
                lastAudioClipPaintedZoom = 0;
            var normalizeLabel = new GUIContent("Normalize");
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(normalizeLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            normalize = EditorGUILayout.Toggle(normalizeLabel, normalize);
            var renderScaleLabel = new GUIContent("Render scale");
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(renderScaleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            renderScale = Mathf.Clamp(EditorGUILayout.FloatField(renderScaleLabel, renderScale),
                MIN_WAVEFORM_RENDER_SCALE, MAX_WAVEFORM_RENDER_SCALE);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Plot
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("Plot", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            var projectNameLabel = new GUIContent("Project", "Name that will be save in project's metadata.");
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(projectNameLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            projectName = EditorGUILayout.TextField(projectNameLabel, projectName);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.MaxWidth(topBarOptionsContainerWidth * 0.33f)))
                Clear();
            if (GUILayout.Button("Reset zoom", GUILayout.MaxWidth(topBarOptionsContainerWidth * 0.33f)))
                zoom = 1;
            var timeLabel = new GUIContent("Time");
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(timeLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            time = Mathf.Max(Mathf.Max(EditorGUILayout.FloatField(timeLabel, time), GetLastPointTime()), MIN_TIME);
            EditorGUIUtility.labelWidth = 0;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Point editing
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("Point editing", EditorStyles.boldLabel);
            var pointDragModes = Enum.GetNames(typeof(PointDragMode));
            for (int i = 0; i < pointDragModes.Length; i++)
                pointDragModes[i] = string.Concat(pointDragModes[i].Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            pointDragMode = (PointDragMode)GUILayout.SelectionGrid((int)pointDragMode, pointDragModes, pointDragModes.Length);
            snapMode = (SnapMode)EditorGUILayout.EnumPopup("Snapping", snapMode);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();            

            #endregion

            #region Plot area

            Rect refRect = EditorGUILayout.GetControlRect();
            Rect bottomLine = new(new Vector2(refRect.x, position.height - lineWithSpacing), refRect.size);
            int toplinesTaken = 4;
            int bottomLinesTaken = 1;
			float topOffset = toplinesTaken * lineWithSpacing + 2 * lineSpacing;
			float bottomOffset = bottomLinesTaken * lineWithSpacing + lineSpacing;
            Rect plotArea = position;
            plotArea.position = new Vector2(refRect.x, topOffset);
			plotArea.width -= plotArea.x * 2 + 10;
			plotArea.height -= plotArea.y + bottomOffset;
			Rect intensityPlotRect = new(plotArea);
			GUIStyle plotTitleStyle = new(GUI.skin.label);
			plotTitleStyle.alignment = TextAnchor.UpperCenter;
			plotTitleStyle.fontStyle = FontStyle.Bold;
			GUI.Label(intensityPlotRect, "Intensity", plotTitleStyle);
			intensityPlotRect.height = (intensityPlotRect.height - lineHeight) / 2;
			Rect sharpnessPlotRect = new(intensityPlotRect);
			sharpnessPlotRect.position += new Vector2(0, sharpnessPlotRect.height);
			GUI.Label(sharpnessPlotRect, "Sharpness", plotTitleStyle);
            Vector2 plotOffset = new(35, lineWithSpacing);
            intensityPlotRect = new(intensityPlotRect.position + plotOffset,
                new Vector2(intensityPlotRect.width - 35, intensityPlotRect.height - plotOffset.y * 2));
            sharpnessPlotRect = new(sharpnessPlotRect.position + plotOffset,
                new Vector2(sharpnessPlotRect.width - 35, sharpnessPlotRect.height - plotOffset.y * 2));
            visiblePlotWidth = intensityPlotRect.width;

            // Y axis labels and horizontal lines
            int yAxisLabelCount = Mathf.Clamp((int)(intensityPlotRect.height / (lineHeight * 1.5)), 2, 11);
            if (yAxisLabelCount < 11)
            {
                if (yAxisLabelCount >= 6) yAxisLabelCount = 6;
                else if (yAxisLabelCount >= 3 && yAxisLabelCount < 5) yAxisLabelCount = 3;
            }
            float verticalAxisLabelInterval = 1f / (yAxisLabelCount - 1);
            Rect intensityYAxisLabelRect = new(refRect);
            intensityYAxisLabelRect.width = 30;
            intensityYAxisLabelRect.position = new Vector2(intensityYAxisLabelRect.x, intensityPlotRect.y - (lineHeight / 2f));
            Rect sharpnessYAxisLabelRect = new(intensityYAxisLabelRect);
            sharpnessYAxisLabelRect.position = new Vector2(sharpnessYAxisLabelRect.x, sharpnessPlotRect.y - (lineHeight / 2f));
            Vector2 yAxisLabelOffset = new(0, intensityPlotRect.height / (yAxisLabelCount - 1));
            GUIStyle yAxisLabelStyle = new(GUI.skin.label);
            yAxisLabelStyle.alignment = TextAnchor.MiddleRight;
            Handles.color = COLOR_PLOT_GRID;
            for (int i = 0; i < yAxisLabelCount; i++)
            {
                string label = (1 - i * verticalAxisLabelInterval).ToString("0.##");

                GUI.Label(intensityYAxisLabelRect, label, yAxisLabelStyle);
                Handles.DrawLine(new Vector3(intensityPlotRect.x, intensityYAxisLabelRect.y + lineHalfHeight),
                    new Vector3(intensityPlotRect.x + intensityPlotRect.width, intensityYAxisLabelRect.y + lineHalfHeight));
                intensityYAxisLabelRect.position += yAxisLabelOffset;

                GUI.Label(sharpnessYAxisLabelRect, label, yAxisLabelStyle);
                Handles.DrawLine(new Vector3(sharpnessPlotRect.x, sharpnessYAxisLabelRect.y + lineHalfHeight),
                    new Vector3(sharpnessPlotRect.x + sharpnessPlotRect.width, sharpnessYAxisLabelRect.y + lineHalfHeight));
                sharpnessYAxisLabelRect.position += yAxisLabelOffset;
            }

            Rect scrollArea = new(plotArea.position + plotOffset, plotArea.size - plotOffset);
            GUIStyle xAxisLabelStyle = new(GUI.skin.label);
            xAxisLabelStyle.alignment = TextAnchor.UpperLeft;

            #endregion

            #region Plot scroll

            plotSize = new Vector2(scrollArea.width * zoom, intensityPlotRect.height);
            scrollPosition = GUI.BeginScrollView(scrollArea, scrollPosition, new Rect(0, 0, plotSize.x, scrollArea.height),
                true, false, GUI.skin.horizontalScrollbar, GUIStyle.none);

            sharpnessPlotHeightOffset = sharpnessPlotRect.y - intensityPlotRect.y;
            int xAxisLabelCount = (int)(plotSize.x / 75);
            Vector2 xAxisLabelOffset = new(plotSize.x / (float)xAxisLabelCount, 0);
            Rect intensityXAxisLabelRect = new(0, intensityPlotRect.height + lineSpacing, 40, lineHeight);
            Rect sharpnessXAxisLabelRect = new(0, sharpnessPlotHeightOffset + sharpnessPlotRect.height + lineSpacing, 40, lineHeight);

            if (audioClip != null && audioWaveformVisible)
            {
                if (Mathf.Abs(zoom - lastAudioClipPaintedZoom) > 0.5f || audioClip.name != lastAudioClipName || normalize != wasNormalized)
                {
                    audioClipTexture = AudioClipUtils.PaintAudioWaveform(audioClip, (int)(plotSize.x * renderScale), (int)(plotSize.y * renderScale), COLOR_WAVEFORM_BG, COLOR_WAVEFORM, normalize);
                    lastAudioClipPaintedZoom = zoom;
                    lastAudioClipName = audioClip.name;
                    wasNormalized = normalize;
                }
                GUI.DrawTexture(new Rect(Vector2.zero, plotSize), audioClipTexture, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(new Vector2(0, sharpnessPlotHeightOffset), plotSize), audioClipTexture, ScaleMode.StretchToFill);
            }

            Vector2 xAxisLabelMiddleOffset = new (20, 0);
            GUI.Label(intensityXAxisLabelRect, "0", xAxisLabelStyle);
            intensityXAxisLabelRect.position += xAxisLabelOffset - xAxisLabelMiddleOffset;
            Handles.DrawLine(new Vector3(xAxisLabelOffset.x / 2, 0),
                    new Vector3(xAxisLabelOffset.x / 2, intensityPlotRect.height));
            GUI.Label(sharpnessXAxisLabelRect, "0", xAxisLabelStyle);
            sharpnessXAxisLabelRect.position += xAxisLabelOffset - xAxisLabelMiddleOffset;
            Handles.DrawLine(new Vector3(xAxisLabelOffset.x / 2, sharpnessPlotHeightOffset),
                    new Vector3(xAxisLabelOffset.x / 2, sharpnessPlotHeightOffset + sharpnessPlotRect.height));
            xAxisLabelStyle.alignment = TextAnchor.UpperCenter;
            for (int i = 1; i < xAxisLabelCount; i++)
            {
                string label = (i * time / xAxisLabelCount).ToString("#0.###");

                GUI.Label(intensityXAxisLabelRect, label, xAxisLabelStyle);
                Handles.DrawLine(new Vector3(i * xAxisLabelOffset.x, 0),
                    new Vector3(i * xAxisLabelOffset.x, intensityPlotRect.height));
                Handles.DrawLine(new Vector3(i * xAxisLabelOffset.x + xAxisLabelOffset.x / 2, 0),
                    new Vector3(i * xAxisLabelOffset.x + xAxisLabelOffset.x / 2, intensityPlotRect.height));
                intensityXAxisLabelRect.position += xAxisLabelOffset;

                GUI.Label(sharpnessXAxisLabelRect, label, xAxisLabelStyle);
                Handles.DrawLine(new Vector3(i * xAxisLabelOffset.x, sharpnessPlotHeightOffset),
                    new Vector3(i * xAxisLabelOffset.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height));
                Handles.DrawLine(new Vector3(i * xAxisLabelOffset.x + xAxisLabelOffset.x / 2, sharpnessPlotHeightOffset),
                    new Vector3(i * xAxisLabelOffset.x + xAxisLabelOffset.x / 2, sharpnessPlotHeightOffset + sharpnessPlotRect.height));
                sharpnessXAxisLabelRect.position += xAxisLabelOffset;
            }
            intensityXAxisLabelRect.position -= xAxisLabelMiddleOffset;
            sharpnessXAxisLabelRect.position -= xAxisLabelMiddleOffset;
            xAxisLabelStyle.alignment = TextAnchor.UpperRight;
            xAxisLabelStyle.margin.right = 0;
            GUI.Label(intensityXAxisLabelRect, time.ToString("#0.##"), xAxisLabelStyle);
            GUI.Label(sharpnessXAxisLabelRect, time.ToString("#0.##"), xAxisLabelStyle);
                        
            Handles.color = COLOR_PLOT_BORDER;
            Handles.DrawAAPolyLine(PLOT_BORDER_WIDTH, new Vector3(1, 1), new Vector3(plotSize.x - 1, 1), 
                new Vector3(plotSize.x - 1, intensityPlotRect.height - 1), new Vector3(1, intensityPlotRect.height - 1), new Vector3(1, 1));
            Handles.DrawAAPolyLine(PLOT_BORDER_WIDTH, new Vector3(1, sharpnessPlotHeightOffset + 1), new Vector3(plotSize.x - 1, sharpnessPlotHeightOffset + 1),
                new Vector3(plotSize.x - 1, sharpnessPlotHeightOffset + sharpnessPlotRect.height - 1), 
                new Vector3(1, sharpnessPlotHeightOffset + sharpnessPlotRect.height - 1), new Vector3(1, sharpnessPlotHeightOffset + 1));

            foreach (var vibraEvent in events)
            {
                if (vibraEvent is TransientEvent transientEvent)
                {
                    Handles.color = COLOR_EVENT_TRANSIENT;

                    Vector3 intensityPoint = PointToPlotCoords(transientEvent.Time, transientEvent.Intensity.Value, MouseLocation.IntensityPlot);
                    Handles.DrawSolidDisc(intensityPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, intensityPoint, new Vector3(intensityPoint.x, intensityPlotRect.height));

                    Vector3 sharpnessPoint = PointToPlotCoords(transientEvent.Time, transientEvent.Sharpness.Value, MouseLocation.SharpnessPlot);
                    Handles.DrawSolidDisc(sharpnessPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, sharpnessPoint, new Vector3(sharpnessPoint.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height));
                }
                else if (vibraEvent is ContinuousEvent continuousEvent)
                {
                    Handles.color = COLOR_EVENT_CONTINUOUS;

                    List<Vector3> points = new();
                    points.Add(PointToPlotCoords(continuousEvent.IntensityCurve[0].Time, 0, MouseLocation.IntensityPlot));
                    for (int i = 0; i < continuousEvent.IntensityCurve.Count; i++)
                    {
                        EventPoint point = continuousEvent.IntensityCurve[i];
                        points.Add(PointToPlotCoords(point.Time, point.Value, MouseLocation.IntensityPlot));
                        Handles.DrawSolidDisc(points.Last(), POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    }
                    points.Add(PointToPlotCoords(continuousEvent.IntensityCurve.Last().Time, 0, MouseLocation.IntensityPlot));
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, points.ToArray());

                    points.Clear();
                    points.Add(PointToPlotCoords(continuousEvent.SharpnessCurve[0].Time, 0, MouseLocation.SharpnessPlot));
                    for (int i = 0; i < continuousEvent.SharpnessCurve.Count; i++)
                    {
                        EventPoint point = continuousEvent.SharpnessCurve[i];
                        points.Add(PointToPlotCoords(point.Time, point.Value, MouseLocation.SharpnessPlot));
                        Handles.DrawSolidDisc(points.Last(), POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    }
                    points.Add(PointToPlotCoords(continuousEvent.SharpnessCurve.Last().Time, 0, MouseLocation.SharpnessPlot));
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, points.ToArray());
                }
            }

            GUI.EndScrollView();

            #endregion

            #region Mouse location

            Handles.color = COLOR_HOVER_GUIDES;
            Vector2 mousePosition = currentEvent.mousePosition;
            Vector2 plotPosition = Vector2.negativeInfinity;
            Vector2 plotRectMousePosition = Vector2.negativeInfinity;
            Vector2 realPlotPosition = Vector2.zero;
            if (intensityPlotRect.Contains(mousePosition))
            {
                mouseLocation = MouseLocation.IntensityPlot;
                Handles.DrawLine(new Vector3(mousePosition.x, sharpnessPlotRect.y), 
                    new Vector3(mousePosition.x, sharpnessPlotRect.y + sharpnessPlotRect.height));
                plotRectMousePosition = mousePosition - intensityPlotRect.position;
            }
            else if (sharpnessPlotRect.Contains(mousePosition))
            {
                mouseLocation = MouseLocation.SharpnessPlot;
                Handles.DrawLine(new Vector3(mousePosition.x, intensityPlotRect.y),
                    new Vector3(mousePosition.x, intensityPlotRect.y + intensityPlotRect.height));
                plotRectMousePosition = mousePosition - sharpnessPlotRect.position;
            }
            else
            {
                mouseLocation = MouseLocation.Outside;
            }

            if (mouseLocation != MouseLocation.Outside)
            {
                Handles.DrawSolidDisc(new Vector3(mousePosition.x, mousePosition.y, 0), POINT_NORMAL, HOVER_DOT_SIZE);
                float x = (scrollPosition.x + plotRectMousePosition.x) / plotSize.x * time;
                float y = (intensityPlotRect.height - plotRectMousePosition.y) / intensityPlotRect.height;
                realPlotPosition = new Vector2(x, y);
                if (snapMode != SnapMode.None)
                {
                    x = (float)Math.Round(x, (int)snapMode);
                    y = (float)Math.Round(y, (int)snapMode);
                }
                plotPosition = new Vector2(x, y);
                GUI.Label(bottomLine, $"{(mouseLocation == MouseLocation.IntensityPlot ? "Intensity" : "Sharpness")}: x={x}, y={y}");

                // Highlight hover point
                hoverPoint = draggedPoint ?? GetEventPointOnPosition(realPlotPosition, mouseLocation, out hoverPointEvent);
                if (hoverPoint != null)
                {
                    Vector3 windowSpaceHoverPoint;
                    if (mouseLocation == MouseLocation.IntensityPlot)
                    {
                        windowSpaceHoverPoint = new(intensityPlotRect.x + hoverPoint.Time / time * plotSize.x - scrollPosition.x, 
                            intensityPlotRect.y + intensityPlotRect.height - hoverPoint.Value * intensityPlotRect.height, 0);
                    }
                    else //if (mouseLocation == MouseLocation.SharpnessPlot)
                    {
                        windowSpaceHoverPoint = new(sharpnessPlotRect.x + hoverPoint.Time / time * plotSize.x - scrollPosition.x,
                            sharpnessPlotRect.y + sharpnessPlotRect.height - hoverPoint.Value * sharpnessPlotRect.height, 0);
                    }
                    Handles.color = COLOR_HOVER_POINT;
                    Handles.DrawSolidDisc(windowSpaceHoverPoint, POINT_NORMAL, HOVER_HIGHLIGHT_SIZE);
                }
            }

            #endregion

            #region Mouse click

            if (currentEvent.button == (int)MouseButton.Left)
            {
                if (hoverPoint == null)
                {
                    if (mouseLocation != MouseLocation.Outside && mouseState == MouseState.Unclicked && currentEvent.type == EventType.MouseDown)
                    {
                        mouseState = MouseState.MouseDown;
                        mouseClickLocation = mouseLocation;
                        mouseClickPlotPosition = plotPosition;
                        continuousEventWindowPos = mousePosition.x;
                    }
                    else if (mouseState == MouseState.MouseDown && currentEvent.type == EventType.MouseDrag)
                    {
                        mouseState = GetContinuousEventIfBetween(plotPosition.x) != null ? MouseState.Unclicked : MouseState.MouseDrag;
                    }
                    else if (mouseState != MouseState.Unclicked && currentEvent.type == EventType.MouseUp)
                    {
                        if (mouseClickLocation == mouseLocation)
                        {
                            if (mouseState == MouseState.MouseDown) // Add transient event or continuous event point
                            {
                                ContinuousEvent ce = GetContinuousEventIfBetween(plotPosition.x);
                                if (ce == null)
                                {
                                    events.Add(new TransientEvent(plotPosition.x,
                                        mouseLocation == MouseLocation.IntensityPlot ? plotPosition.y : 0.5f,
                                        mouseLocation == MouseLocation.SharpnessPlot ? plotPosition.y : 0.5f));
                                }
                                else
                                {
                                    if (mouseLocation == MouseLocation.IntensityPlot)
                                    {
                                        ce.IntensityCurve.Add(plotPosition);
                                        ce.IntensityCurve.Sort((p1, p2) => p1.Time.CompareTo(p2.Time));
                                    }
                                    else if (mouseLocation == MouseLocation.SharpnessPlot)
                                    {
                                        ce.SharpnessCurve.Add(plotPosition);
                                        ce.SharpnessCurve.Sort((p1, p2) => p1.Time.CompareTo(p2.Time));
                                    }
                                }
                            }
                            else if (mouseState == MouseState.MouseDrag && GetContinuousEventIfBetween(plotPosition.x) == null) // Add continuous event
                            {
                                events.Add(new ContinuousEvent(new Vector2(Mathf.Min(mouseClickPlotPosition.x, plotPosition.x), Mathf.Max(mouseClickPlotPosition.x, plotPosition.x)),
                                    mouseLocation == MouseLocation.IntensityPlot ? new Vector2(plotPosition.y, plotPosition.y) : Vector2.one * 0.5f,
                                    mouseLocation == MouseLocation.SharpnessPlot ? new Vector2(plotPosition.y, plotPosition.y) : Vector2.one * 0.5f));
                            }
                        }
                        mouseState = MouseState.Unclicked;
                    }
                    else if (mouseState == MouseState.MouseDrag && mouseLocation == mouseClickLocation)
                    {
                        EditorGUI.DrawRect(new Rect(continuousEventWindowPos, mousePosition.y, mousePosition.x - continuousEventWindowPos,
                            mouseLocation == MouseLocation.IntensityPlot ? intensityPlotRect.y + intensityPlotRect.height - mousePosition.y :
                            sharpnessPlotRect.y + sharpnessPlotRect.height - mousePosition.y), COLOR_EVENT_CONTINUOUS_CREATION);
                    }
                }
                else if (draggedPoint == null && currentEvent.type == EventType.MouseDown)
                {
                    draggedPoint = hoverPoint;
                    draggedPointEvent = hoverPointEvent;
                    
                    dragMin = scrollPosition.x / plotSize.x * time + NEIGHBOURING_POINT_OFFSET;
                    dragMax = (scrollPosition.x + visiblePlotWidth) / plotSize.x * time - NEIGHBOURING_POINT_OFFSET;
                    if (hoverPointEvent is ContinuousEvent continuousEvent)
                    {
                        ContinuousEvent ce = GetContinuousEventIfBetween(hoverPoint.Time);
                        if (ce != null)
                        {
                            var curve = mouseLocation == MouseLocation.IntensityPlot ? continuousEvent.IntensityCurve : continuousEvent.SharpnessCurve;
                            dragMin = curve.FindLast(point => point.Time < hoverPoint.Time).Time + NEIGHBOURING_POINT_OFFSET;
                            dragMax = curve.Find(point => point.Time > hoverPoint.Time).Time - NEIGHBOURING_POINT_OFFSET;
                        }
                        else if (draggedPoint == continuousEvent.IntensityCurve.First() || draggedPoint == continuousEvent.SharpnessCurve.First())
                        {
                            dragMax = Mathf.Min(continuousEvent.IntensityCurve[1].Time, continuousEvent.SharpnessCurve[1].Time) - NEIGHBOURING_POINT_OFFSET;
                            var previousEvent = events.FindLast(ev => ev.Time < hoverPoint.Time && ev is ContinuousEvent);
                            if (previousEvent != null)
                                dragMin = ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + NEIGHBOURING_POINT_OFFSET;
                        }
                        else if (draggedPoint == continuousEvent.IntensityCurve.Last() || draggedPoint == continuousEvent.SharpnessCurve.Last())
                        {
                            dragMin = Mathf.Max(continuousEvent.IntensityCurve[continuousEvent.IntensityCurve.Count - 2].Time,
                                continuousEvent.SharpnessCurve[continuousEvent.SharpnessCurve.Count - 2].Time) + NEIGHBOURING_POINT_OFFSET;
                            var nextEvent = events.Find(ev => ev.Time > hoverPoint.Time && ev is ContinuousEvent);
                            if (nextEvent != null)
                                dragMax = ((ContinuousEvent)nextEvent).IntensityCurve.First().Time - NEIGHBOURING_POINT_OFFSET;
                        }
                    }

                    mouseClickLocation = mouseLocation;
                }
                else if (draggedPoint != null)
                {
                    if (currentEvent.type == EventType.MouseDrag && mouseLocation == mouseClickLocation)
                    {
                        if (pointDragMode != PointDragMode.LockTime && !currentEvent.shift)
                            draggedPoint.Time = Mathf.Clamp(plotPosition.x, dragMin, dragMax);
                        if (pointDragMode != PointDragMode.LockValue && !currentEvent.alt)
                            draggedPoint.Value = Mathf.Clamp(plotPosition.y, 0, 1);
                        if (draggedPointEvent is TransientEvent te)
                        {
                            te.Time = te.Intensity.Time = te.Sharpness.Time = draggedPoint.Time;
                        }
                        else if (draggedPointEvent is ContinuousEvent ce)
                        {
                            if (draggedPoint == ce.IntensityCurve.First() || draggedPoint == ce.SharpnessCurve.First())
                                ce.Time = ce.IntensityCurve.First().Time = ce.SharpnessCurve.First().Time = draggedPoint.Time;
                            else if (draggedPoint == ce.IntensityCurve.Last() || draggedPoint == ce.SharpnessCurve.Last())
                                ce.IntensityCurve.Last().Time = ce.SharpnessCurve.Last().Time = draggedPoint.Time;
                        }
                    }
                    else if (currentEvent.type == EventType.MouseUp)
                    {
                        draggedPoint = null;
                        draggedPointEvent = null;
                    }
                }
            }
            else if (currentEvent.type == EventType.MouseUp && mouseLocation != MouseLocation.Outside && hoverPoint != null && 
                ((currentEvent.button == (int)MouseButton.Right && hoverPointEvent.ShouldRemoveEventAfterRemovingPoint(hoverPoint, mouseLocation)) || 
                currentEvent.button == (int)MouseButton.Middle))
            {
                events.Remove(hoverPointEvent);
                hoverPoint = null;
                hoverPointEvent = null;
            }

            #endregion

            if (mouseOverWindow == this)
                Repaint();
        }

        #region Helper functions

        private Vector3 PointToPlotCoords(float time, float value, MouseLocation plot)
        {
            return new Vector3(time / this.time * plotSize.x, 
                plotSize.y - value * plotSize.y + (plot == MouseLocation.SharpnessPlot ? sharpnessPlotHeightOffset : 0), 0);
        }

        private ContinuousEvent GetContinuousEventIfBetween(float time)
        {
            foreach (var ev in events)
            {
                if (ev.Time < time && ev is ContinuousEvent continuousEvent && 
                    time > continuousEvent.Time && time < continuousEvent.IntensityCurve.Last().Time)
                    return continuousEvent;
            }
            return null;
        }

        private EventPoint GetEventPointOnPosition(Vector2 plotPosition, MouseLocation plot, out VibrationEvent vibrationEvent)
        {
            vibrationEvent = null;
            Vector2 pointOffset = new(HOVER_OFFSET * time / plotSize.x, HOVER_OFFSET / plotSize.y);
            foreach (var ev in events)
            {
                if (ev.IsOnPointInEvent(plotPosition, pointOffset, plot, out EventPoint eventPoint))
                {
                    vibrationEvent = ev;
                    return eventPoint;
                }
            }            
            return null;
        }

        private float GetLastPointTime()
        {
            float lastPointTime = 0, t;
            foreach (var ev in events)
            {
                t = ev is TransientEvent ? ev.Time : ((ContinuousEvent)ev).IntensityCurve.Last().Time;

                if (t > lastPointTime)
                    lastPointTime = t;
            }
            return lastPointTime;
        }

        private AHAPFile ConvertEventsToAHAPFile()
        {
            List<Pattern> patternList = new();
            foreach (var ev in events)
                patternList.AddRange(ev.ToPatterns());
            patternList.Sort();
            return new AHAPFile(1, new Metadata(projectName), patternList);
        }

        private void HandleSaving()
        {
            if (events.Count == 0)
            {
                EditorUtility.DisplayDialog("No events", "Create some events to save it in file.", "OK");
                return;
            }

            var ahapFile = ConvertEventsToAHAPFile();
            string json = JsonConvert.SerializeObject(ahapFile, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            if (this.ahapFile != null && EditorUtility.DisplayDialog("Overwrite file?", "Do you want to overwrite selected file?",
                "Yes, overwrite", "No, create new"))
            {                
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, AssetDatabase.GetAssetPath(this.ahapFile)), json);
                EditorUtility.SetDirty(this.ahapFile);
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("Save AHAP JSON", "ahap", "json", "Enter file name");
            if (path.Length != 0)
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, path), json);
                AssetDatabase.ImportAsset(path);
                this.ahapFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                EditorUtility.SetDirty(this.ahapFile);
            }
        }

        private void HandleImport()
        {
            if (ahapFile != null)
            {
                AHAPFile ahap;
                try
                {
                    ahap = JsonConvert.DeserializeObject<AHAPFile>(ahapFile.text);

                    if (events != null) events.Clear();
                    else events = new List<VibrationEvent>();

                    foreach (var patternElement in ahap.Pattern)
                    {
                        Event e = patternElement.Event;
                        if (e != null)
                        {
                            int index = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_INTENSITY);
                            float intensity = index != -1 ? (float)e.EventParameters[index].ParameterValue : 1;
                            index = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_SHARPNESS);
                            float sharpness = index != -1 ? (float)e.EventParameters[index].ParameterValue : 0;
                            if (e.EventType == AHAPFile.EVENT_TRANSIENT)
                            {
                                events.Add(new TransientEvent((float)e.Time, intensity, sharpness));
                            }
                            else if (e.EventType == AHAPFile.EVENT_CONTINUOUS)
                            {
                                List<EventPoint> intensityPoints = new();
                                float t = (float)e.Time;
                                Pattern curve = FindCurveOnTime(AHAPFile.CURVE_INTENSITY, t);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                        intensityPoints.Add(new EventPoint((float)point.Time, (float)point.ParameterValue));

                                    t = intensityPoints.Last().Time;
                                    curve = FindCurveOnTime(AHAPFile.CURVE_INTENSITY, t, curve);
                                }
                                if (intensityPoints.Count == 0)
                                {
                                    intensityPoints.Add(new EventPoint((float)e.Time, intensity));
                                    intensityPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), intensity));
                                }
                                else if (!Mathf.Approximately(intensityPoints.Last().Time, (float)(e.Time + e.EventDuration)))
                                {
                                    intensityPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), intensityPoints.Last().Value));
                                }

                                List<EventPoint> sharpnessPoints = new();
                                t = (float)e.Time;
                                curve = FindCurveOnTime(AHAPFile.CURVE_SHARPNESS, t);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                        sharpnessPoints.Add(new EventPoint((float)point.Time, (float)point.ParameterValue));

                                    t = sharpnessPoints.Last().Time;
                                    curve = FindCurveOnTime(AHAPFile.CURVE_SHARPNESS, t, curve);
                                }
                                if (sharpnessPoints.Count == 0)
                                {
                                    sharpnessPoints.Add(new EventPoint((float)e.Time, sharpness));
                                    sharpnessPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), sharpness));
                                }
                                else if (!Mathf.Approximately(sharpnessPoints.Last().Time, (float)(e.Time + e.EventDuration)))
                                {
                                    intensityPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), sharpnessPoints.Last().Value));
                                }

                                ContinuousEvent ce = new();
                                ce.Time = (float)e.Time;
                                ce.IntensityCurve = intensityPoints;
                                ce.SharpnessCurve = sharpnessPoints;
                                events.Add(ce);
                            }
                        }
                    }
                    time = GetLastPointTime();
                    projectName = ahap.Metadata.Project;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while importing file {ahapFile.name}{Environment.NewLine}{ex.Message}");
                }

                Pattern FindCurveOnTime(string curveType, float time, Pattern previousCurve = null)
                {
                    return ahap.Pattern.Find(element => element.ParameterCurve != null && (float)element.ParameterCurve.Time == time &&
                        element.ParameterCurve.ParameterID == curveType && element != previousCurve);
                }
            }
        }

        #endregion
    }
}
