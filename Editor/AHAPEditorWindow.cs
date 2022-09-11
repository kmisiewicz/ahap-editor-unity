using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

        class Content
        {
            public static GUIContent waveformVisibleLabel = EditorGUIUtility.TrTextContent("Visible");
            public static GUIContent normalizeLabel = EditorGUIUtility.TrTextContent("Normalize");
            public static GUIContent renderScaleLabel = EditorGUIUtility.TrTextContent("Render scale");
            public static GUIContent projectNameLabel = EditorGUIUtility.TrTextContent("Project", "Name that will be save in project's metadata.");
            public static GUIContent timeLabel = EditorGUIUtility.TrTextContent("Time");

            public static GUIStyle plotTitleStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            public static GUIStyle yAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            public static GUIStyle xAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            public static GUIContent yAxisLabelDummy = EditorGUIUtility.TrTextContent("#.##");
            public static GUIContent xAxisLabelDummy = EditorGUIUtility.TrTextContent("##.###");

        }

        // Data
        TextAsset ahapFile;
		float zoom = 1f;
        float time = 1f;        
        string projectName = "";
        List<VibrationEvent> events = new();
        Vector2 scrollPositionOld = Vector2.zero;
        Vector2 scrolledPlotSizeOld = Vector2.one;
        PointDragMode pointDragMode = PointDragMode.FreeMove;
        SnapMode snapMode = SnapMode.None;
        MouseState mouseState = MouseState.Unclicked;
        MouseLocation mouseLocation = MouseLocation.Outside;
        MouseLocation mouseClickLocation = MouseLocation.Outside;        
        Vector2 mouseClickPlotPosition;
        float continuousEventWindowPos;
        float sharpnessPlotHeightOffsetOld;
        EventPoint hoverPoint = null;
        VibrationEvent hoverPointEvent = null;
        EventPoint draggedPoint = null;
        VibrationEvent draggedPointEvent = null;
        float dragMin, dragMax;
                
        // Audio waveform
        AudioClip audioClip;
        bool audioWaveformVisible = false;
        float lastAudioClipPaintedZoom = 1f;
        Texture2D audioClipTexture;
        bool normalizeWaveform;
        float renderScale = 1f;
        bool shouldRepaintWaveform = false;

        // Help data NEW        
        Vector2 plotScreenSize; // Size of single plot rect on screen
        Vector2 plotScrollSize; // Size of scroll view
        Vector2 scrollPosition;
        float plotHeightOffset; // Difference between plots top left corner
        string[] pointDragModes;

        // Debug
        bool drawRects;

        [MenuItem("Window/AHAP Editor")]
        public static void OpenWindow()
        {
            AHAPEditorWindow window = GetWindow<AHAPEditorWindow>("AHAP Editor");
            var content = EditorGUIUtility.IconContent("d_HoloLensInputModule Icon", "AHAP Editor");
            content.text = "AHAP Editor";
            window.titleContent = content;
        }

        private void OnEnable()
        {
            Clear();
            ahapFile = null;
            projectName = "";
            audioClip = null;
            renderScale = 1f;
            audioWaveformVisible = normalizeWaveform = shouldRepaintWaveform = false;
            pointDragModes = Enum.GetNames(typeof(PointDragMode));
            for (int i = 0; i < pointDragModes.Length; i++)
                pointDragModes[i] = string.Concat(pointDragModes[i].Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
        }

        private void Clear()
        {
            events ??= new List<VibrationEvent>();
            events.Clear();
            time = zoom = 1f;
        }

        private void OnGUI()
		{
            #region Size and positions calculations

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float lineSpacing = EditorGUIUtility.standardVerticalSpacing;
            float lineWithSpacing = lineHeight + lineSpacing;
            float lineHalfHeight = lineHeight * 0.5f;
            float lineDoubleSpacing = lineSpacing * 2;

            float topBarHeight = lineWithSpacing * 3 + lineHalfHeight + lineDoubleSpacing;
            Rect topBarRect = new(MARGIN, new Vector2(position.width - MARGIN.x * 2, topBarHeight));
            float topBarOptionsContainerWidth = 0.12f * Screen.currentResolution.width;
            var topBarMaxWidthOption = GUILayout.MaxWidth(topBarOptionsContainerWidth);
            var topBarContainerThirdOption = GUILayout.MaxWidth(topBarOptionsContainerWidth * 0.33f);

            float bottomPartHeight = position.height - MARGIN.y * 2 - topBarHeight - lineDoubleSpacing * 2;
            Rect bottomPartRect = new(MARGIN.x, MARGIN.y + topBarHeight + lineDoubleSpacing, topBarRect.width, bottomPartHeight);

            float plotAreaWidth = bottomPartRect.width * 0.85f;
            Rect plotAreaRect = new(bottomPartRect.position, new Vector2(plotAreaWidth, bottomPartRect.height));

            float pointEditAreaWidth = bottomPartRect.width - plotAreaWidth - lineHalfHeight;
            Rect pointEditAreaRect = new(bottomPartRect.position + new Vector2(plotAreaWidth + lineHalfHeight, 0),
                new Vector2(pointEditAreaWidth, bottomPartHeight));

            float singlePlotAreaHeight = (plotAreaRect.height - lineHeight) * 0.5f - lineSpacing;
            Rect intensityPlotAreaRect = new(plotAreaRect.position, new Vector2(plotAreaWidth, singlePlotAreaHeight));
            Rect sharpnessPlotAreaRect = new(new Vector2(plotAreaRect.x, plotAreaRect.y + intensityPlotAreaRect.height + lineDoubleSpacing),
                intensityPlotAreaRect.size);

            Vector2 yAxisLabelSize = EditorStyles.label.CalcSize(Content.yAxisLabelDummy);
            yAxisLabelSize.x += CUSTOM_LABEL_WIDTH_OFFSET;
            yAxisLabelSize.y *= 1.5f;
            Vector2 xAxisLabelSize = EditorStyles.label.CalcSize(Content.xAxisLabelDummy);
            xAxisLabelSize.x *= 1.5f;
            Vector2 plotOffsetLeftTop = new(yAxisLabelSize.x, lineWithSpacing);
            Vector2 plotOffsetRightBottom = new(lineHalfHeight, lineWithSpacing);
            plotScreenSize = intensityPlotAreaRect.size - plotOffsetLeftTop - plotOffsetRightBottom;
            plotScreenSize.y -= lineSpacing;
            Rect intensityPlotRect = new(intensityPlotAreaRect.position + plotOffsetLeftTop, plotScreenSize);
            intensityPlotRect.y += lineSpacing;
            Rect sharpnessPlotRect = new(sharpnessPlotAreaRect.position + plotOffsetLeftTop, plotScreenSize);
            plotHeightOffset = sharpnessPlotRect.y - intensityPlotRect.y;

            Rect scrollRect = new(intensityPlotRect.x, intensityPlotRect.y,
                plotAreaRect.width - plotOffsetLeftTop.x - plotOffsetRightBottom.x,
                plotAreaRect.height - plotOffsetLeftTop.y - lineDoubleSpacing);
            plotScrollSize = new Vector2(scrollRect.width * zoom, intensityPlotRect.height);
            Rect scrollPlotRect = new(Vector2.zero, new Vector2(plotScrollSize.x, plotScreenSize.y));
            Rect scrollContentRect = new(0, 0, plotScrollSize.x, scrollRect.height);

            int xAxisLabelCount = (int)(plotScrollSize.x / xAxisLabelSize.x);
            float xAxisLabelWidthInterval = plotScrollSize.x / xAxisLabelCount;
            Rect xAxisLabelRect = new(0, plotScreenSize.y + lineDoubleSpacing, xAxisLabelSize.x, xAxisLabelSize.y);
            float xAxisLabelInterval = time / xAxisLabelCount;

            int yAxisLabelCount = Mathf.RoundToInt(Mathf.Clamp(intensityPlotRect.height / yAxisLabelSize.y, 2, 11));
            if (yAxisLabelCount < 11)
            {
                if (yAxisLabelCount >= 6) yAxisLabelCount = 6;
                else if (yAxisLabelCount >= 4) yAxisLabelCount = 5;
            }
            float yAxisLabelInterval = 1f / (yAxisLabelCount - 1);
            float yAxisLabelHeightInterval = plotScreenSize.y / (yAxisLabelCount - 1);
            Rect yAxisLabelRect = new(MARGIN.x, intensityPlotRect.y - lineHalfHeight - lineDoubleSpacing,
                plotOffsetLeftTop.x - CUSTOM_LABEL_WIDTH_OFFSET, lineHeight);

            // Debug
            if (drawRects)
            {
                EditorGUI.DrawRect(topBarRect, Color.blue);
                EditorGUI.DrawRect(bottomPartRect, Color.black);
                EditorGUI.DrawRect(plotAreaRect, Color.yellow);
                EditorGUI.DrawRect(pointEditAreaRect, Color.green);
                EditorGUI.DrawRect(intensityPlotAreaRect, Color.magenta);
                EditorGUI.DrawRect(sharpnessPlotAreaRect, Color.cyan);
                EditorGUI.DrawRect(intensityPlotRect, Color.red);
                EditorGUI.DrawRect(sharpnessPlotRect, Color.blue);
                EditorGUI.DrawRect(scrollRect, new Color(1, 1, 1, 0.6f));
                EditorGUI.DrawRect(yAxisLabelRect, Color.white);
                EditorGUI.DrawRect(new Rect(xAxisLabelRect.position + intensityPlotRect.position, xAxisLabelRect.size), Color.white);
            }

            #endregion

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
                    scrollPosition.x += plotScrollSize.x * (currentEvent.delta.y > 0 ? SCROLL_INCREMENT : -SCROLL_INCREMENT);
                    scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0, plotScrollSize.x - plotScreenSize.x);
                }
                currentEvent.Use();
            }

            #endregion

            #region Top Bar

            GUILayout.BeginArea(topBarRect, EditorStyles.helpBox);
            GUILayout.BeginHorizontal();

            // Debug
            GUILayout.BeginVertical(GUI.skin.box, topBarContainerThirdOption);
            EditorGUILayout.LabelField("GUI Debug", EditorStyles.boldLabel, topBarContainerThirdOption);
            if (GUILayout.Button("Log rects", topBarContainerThirdOption))
            {
                StringBuilder sb = new("Rects:\n");
                sb.AppendLine($"Top bar: {topBarRect} (blue)");
                sb.AppendLine($"Bottom part: {bottomPartRect} (black)");
                sb.AppendLine($"Plot area: {plotAreaRect} (yellow)");
                sb.AppendLine($"Point edit area: {pointEditAreaRect} (green)");
                sb.AppendLine($"Intensity area: {intensityPlotAreaRect} (magenta)");
                sb.AppendLine($"Intensity plot: {intensityPlotRect} (red)");
                sb.AppendLine($"Sharpness area: {sharpnessPlotAreaRect} (cyan)");
                sb.AppendLine($"Sharpness plot: {sharpnessPlotRect} (blue)");
                sb.AppendLine($"Scroll rect: {scrollRect} (translucent white)");
                sb.AppendLine($"Scroll rect: {yAxisLabelRect} (white)");
                sb.AppendLine($"Scroll rect: {new Rect(xAxisLabelRect.position + intensityPlotRect.position, xAxisLabelRect.size)} (white)");
                Debug.Log(sb.ToString());
            }
            GUIContent drawRectsLabel = new("Draw Rects");
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(drawRectsLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            drawRects = EditorGUILayout.Toggle(drawRectsLabel, drawRects, topBarContainerThirdOption);
            GUILayout.EndVertical();

            // File
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("File", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            ahapFile = EditorGUILayout.ObjectField(GUIContent.none, ahapFile, typeof(TextAsset), false, topBarContainerThirdOption) as TextAsset;
            GUI.enabled = ahapFile != null;
            if (GUILayout.Button("Import", topBarContainerThirdOption))
                HandleImport();
            GUI.enabled = true;
            if (GUILayout.Button("Save", topBarContainerThirdOption))
                HandleSaving();
            GUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.projectNameLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            projectName = EditorGUILayout.TextField(Content.projectNameLabel, projectName);
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
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"), EditorStyles.iconButton))
                AudioClipUtils.PlayClip(audioClip);
            GUILayout.FlexibleSpace();
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.waveformVisibleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            EditorGUI.BeginChangeCheck();
            audioWaveformVisible = EditorGUILayout.Toggle(Content.waveformVisibleLabel, audioWaveformVisible);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (!audioWaveformVisible)
                lastAudioClipPaintedZoom = 0;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.normalizeLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            normalizeWaveform = EditorGUILayout.Toggle(Content.normalizeLabel, normalizeWaveform);
            if (EditorGUI.EndChangeCheck() && audioWaveformVisible)
                shouldRepaintWaveform = true;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.renderScaleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            renderScale = Mathf.Clamp(EditorGUILayout.FloatField(Content.renderScaleLabel, renderScale),
                MIN_WAVEFORM_RENDER_SCALE, MAX_WAVEFORM_RENDER_SCALE);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Plot
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("Plot", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset zoom", topBarContainerThirdOption))
                zoom = 1;
            zoom = (float)Math.Round(EditorGUILayout.Slider("Zoom", zoom, 1, MAX_ZOOM), 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", topBarContainerThirdOption))
                Clear();
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.timeLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            time = Mathf.Clamp(Mathf.Max(EditorGUILayout.FloatField(Content.timeLabel, time), GetLastPointTime()), MIN_TIME, MAX_TIME);
            EditorGUIUtility.labelWidth = 0;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Point editing
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            EditorGUILayout.LabelField("Point editing", EditorStyles.boldLabel);            
            pointDragMode = (PointDragMode)GUILayout.SelectionGrid((int)pointDragMode, pointDragModes, pointDragModes.Length);
            snapMode = (SnapMode)EditorGUILayout.EnumPopup("Snapping", snapMode);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            #endregion

            #region Plot Area

            // Plot titles
            GUILayout.BeginArea(plotAreaRect, EditorStyles.helpBox);
            GUILayout.EndArea();
            GUILayout.BeginArea(intensityPlotAreaRect);
            GUILayout.Space(lineDoubleSpacing);
            GUILayout.Label("Intensity", Content.plotTitleStyle);
            GUILayout.EndArea();
            GUILayout.BeginArea(sharpnessPlotAreaRect);
            GUILayout.Space(lineSpacing);
            GUILayout.Label("Sharpness", Content.plotTitleStyle);
            GUILayout.EndArea();

            // Scroll view
            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, scrollContentRect,
                true, false, GUI.skin.horizontalScrollbar, GUIStyle.none);

            // Audio waveform
            if (audioClip != null && audioWaveformVisible)
            {
                if (Mathf.Abs(zoom - lastAudioClipPaintedZoom) > 0.5f || shouldRepaintWaveform)
                {
                    audioClipTexture = AudioClipUtils.PaintAudioWaveform(audioClip, (int)(plotScrollSize.x * renderScale),
                        (int)(plotScrollSize.y * renderScale), COLOR_WAVEFORM_BG, COLOR_WAVEFORM, normalizeWaveform);
                    lastAudioClipPaintedZoom = zoom;
                    shouldRepaintWaveform = false;
                }
                GUI.DrawTexture(scrollPlotRect, audioClipTexture, ScaleMode.StretchToFill);
                scrollPlotRect.y += plotHeightOffset;
                GUI.DrawTexture(scrollPlotRect, audioClipTexture, ScaleMode.StretchToFill);
                scrollPlotRect.y -= plotHeightOffset;
            }

            // X axis labels and vertical grid
            Vector3 gridPoint1 = Vector2.zero;
            Vector3 gridPoint2 = new(0, plotScreenSize.y);
            Handles.color = COLOR_PLOT_GRID;
            float timeLabelValue = 0;
            string timeLabel = "0";
            DrawTimeLabels(GUI.skin.label);
            xAxisLabelRect.x -= xAxisLabelRect.width * 0.5f;
            for (int i = 1; i < xAxisLabelCount; i++)
            {
                timeLabelValue += xAxisLabelInterval;
                timeLabel = timeLabelValue.ToString("#0.###");
                xAxisLabelRect.x += xAxisLabelWidthInterval;
                DrawTimeLabels(Content.xAxisLabelStyle);
                gridPoint1.x = gridPoint2.x = gridPoint1.x + xAxisLabelWidthInterval;
                Handles.DrawLine(gridPoint1, gridPoint2);
                gridPoint1.y += plotHeightOffset;
                gridPoint2.y += plotHeightOffset;
                Handles.DrawLine(gridPoint1, gridPoint2);
                gridPoint1.y -= plotHeightOffset;
                gridPoint2.y -= plotHeightOffset;
            }
            xAxisLabelRect.x += xAxisLabelWidthInterval - (xAxisLabelSize.x / 2);
            timeLabel = time.ToString("#0.###");
            DrawTimeLabels(Content.yAxisLabelStyle);

            void DrawTimeLabels(GUIStyle style)
            {
                GUI.Label(xAxisLabelRect, timeLabel, style);
                xAxisLabelRect.y += plotHeightOffset;
                GUI.Label(xAxisLabelRect, timeLabel, style);
                xAxisLabelRect.y -= plotHeightOffset;
            }

            foreach (var vibrationEvent in events)
            {
                if (vibrationEvent is TransientEvent transientEvent)
                {
                    Handles.color = COLOR_EVENT_TRANSIENT;

                    Vector3 intensityPoint = PointToPlotCoords(transientEvent.Time, transientEvent.Intensity.Value, MouseLocation.IntensityPlot);
                    Handles.DrawSolidDisc(intensityPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, intensityPoint, new Vector3(intensityPoint.x, plotScreenSize.y));

                    Vector3 sharpnessPoint = PointToPlotCoords(transientEvent.Time, transientEvent.Sharpness.Value, MouseLocation.SharpnessPlot);
                    Handles.DrawSolidDisc(sharpnessPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, sharpnessPoint, new Vector3(sharpnessPoint.x, plotHeightOffset + plotScreenSize.y));
                }
                else if (vibrationEvent is ContinuousEvent continuousEvent)
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

            // Y axis labels and horizontal grid
            gridPoint1 = new(intensityPlotRect.x, intensityPlotRect.y);
            gridPoint2 = new(intensityPlotRect.x + intensityPlotRect.width, intensityPlotRect.y);
            Handles.color = COLOR_PLOT_GRID;
            for (int i = 0; i < yAxisLabelCount; i++)
            {
                string valueLabel = (1 - i * yAxisLabelInterval).ToString("0.##");
                GUI.Label(yAxisLabelRect, valueLabel, Content.yAxisLabelStyle);
                Handles.DrawLine(gridPoint1, gridPoint2);
                yAxisLabelRect.y += plotHeightOffset;
                gridPoint1.y = gridPoint2.y = gridPoint1.y + plotHeightOffset;
                GUI.Label(yAxisLabelRect, valueLabel, Content.yAxisLabelStyle);
                Handles.DrawLine(gridPoint1, gridPoint2);
                yAxisLabelRect.y += yAxisLabelHeightInterval - plotHeightOffset;
                gridPoint1.y = gridPoint2.y = gridPoint1.y - plotHeightOffset + yAxisLabelHeightInterval;
            }

            Handles.color = COLOR_PLOT_BORDER;
            DrawBorderForRect(intensityPlotRect);
            DrawBorderForRect(sharpnessPlotRect);
            
            void DrawBorderForRect(Rect rect)
            {
                Handles.DrawAAPolyLine(PLOT_BORDER_WIDTH,
                    rect.position,
                    new Vector3(rect.x + rect.width, rect.y),
                    rect.position + rect.size,
                    new Vector3(rect.x, rect.y + rect.height),
                    rect.position);
            }

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
                float x = (scrollPosition.x + plotRectMousePosition.x) / plotScrollSize.x * time;
                float y = (intensityPlotRect.height - plotRectMousePosition.y) / intensityPlotRect.height;
                realPlotPosition = new Vector2(x, y);
                if (snapMode != SnapMode.None)
                {
                    x = (float)Math.Round(x, (int)snapMode);
                    y = (float)Math.Round(y, (int)snapMode);
                }
                plotPosition = new Vector2(x, y);
                //GUI.Label(bottomLine, $"{(mouseLocation == MouseLocation.IntensityPlot ? "Intensity" : "Sharpness")}: x={x}, y={y}");

                // Highlight hover point
                hoverPoint = draggedPoint ?? GetEventPointOnPosition(realPlotPosition, mouseLocation, out hoverPointEvent);
                if (hoverPoint != null)
                {
                    Vector3 windowSpaceHoverPoint;
                    if (mouseLocation == MouseLocation.IntensityPlot)
                    {
                        windowSpaceHoverPoint = new(intensityPlotRect.x + hoverPoint.Time / time * plotScrollSize.x - scrollPosition.x, 
                            intensityPlotRect.y + intensityPlotRect.height - hoverPoint.Value * intensityPlotRect.height, 0);
                    }
                    else //if (mouseLocation == MouseLocation.SharpnessPlot)
                    {
                        windowSpaceHoverPoint = new(sharpnessPlotRect.x + hoverPoint.Time / time * plotScrollSize.x - scrollPosition.x,
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
                    
                    dragMin = scrollPosition.x / plotScrollSize.x * time + NEIGHBOURING_POINT_OFFSET;
                    dragMax = (scrollPosition.x + plotScreenSize.x) / plotScrollSize.x * time - NEIGHBOURING_POINT_OFFSET;
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
            return new Vector3(time / this.time * plotScrollSize.x, 
                plotScreenSize.y - value * plotScreenSize.y + (plot == MouseLocation.SharpnessPlot ? plotHeightOffset : 0), 0);
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
            Vector2 pointOffset = new(HOVER_OFFSET * time / plotScrollSize.x, HOVER_OFFSET / plotScreenSize.y);
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
            string json = JsonConvert.SerializeObject(ahapFile, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

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
