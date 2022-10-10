using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public class AHAPEditorWindow : EditorWindow, IHasCustomMenu
    {
        #region Types, defines and variables

        private enum SnapMode 
        { 
            None = 0,
            [InspectorName("0.1")] Tenth = 1,
            [InspectorName("0.01")] Hundredth = 2,
            [InspectorName("0.001")] Thousandth = 3 
        }

        private enum PointDragMode { FreeMove = 0, LockTime = 1, LockValue = 2 }

        private enum MouseButton { Left = 0, Right = 1, Middle = 2 }

        // Const settings
        const float MIN_TIME = 0.1f;
        const float MAX_TIME = 30f;
        const float HOVER_OFFSET = 5f;
        const float ZOOM_INCREMENT = 0.1f;
        const float MAX_ZOOM = 7f;
        const float SCROLL_INCREMENT = 0.05f;
        const float HOVER_HIGHLIGHT_SIZE = 10f;
        const float DRAG_HIGHLIGHT_SIZE = 13f;
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
        static readonly Color COLOR_DRAG_POINT = new(0.7f, 1f, 1f, 0.3f);
        static readonly Color COLOR_HOVER_GUIDES = new(0.7f, 0f, 0f);

        class Content
        {
            public static GUIContent waveformVisibleLabel = EditorGUIUtility.TrTextContent("Visible");
            public static GUIContent normalizeLabel = EditorGUIUtility.TrTextContent("Normalize");
            public static GUIContent renderScaleLabel = EditorGUIUtility.TrTextContent("Render scale");
            public static GUIContent projectNameLabel = EditorGUIUtility.TrTextContent("Project", "Name that will be save in project's metadata.");
            public static GUIContent timeLabel = EditorGUIUtility.TrTextContent("Time");
            public static GUIContent zoomLabel = EditorGUIUtility.TrTextContent("Zoom");

            public static GUIStyle plotTitleStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            public static GUIStyle yAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            public static GUIStyle xAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            public static GUIContent yAxisLabelDummy = EditorGUIUtility.TrTextContent("#.##");
            public static GUIContent xAxisLabelDummy = EditorGUIUtility.TrTextContent("##.###");

        }

        // Data
        TextAsset ahapFile;
        float time = 1f;        
        string projectName = "";
        List<VibrationEvent> events = new();

        // Drawing
        float zoom = 1f;
        Vector2 plotScreenSize; // Size of single plot rect on screen
        Vector2 plotScrollSize; // Size of scroll view
        Vector2 scrollPosition;
        float plotHeightOffset; // Difference between plots top left corner
        EventType previousMouseState = EventType.MouseUp;
        MouseLocation mouseLocation = MouseLocation.Outside;
        MouseLocation mouseClickLocation = MouseLocation.Outside;
        Vector2 mouseClickPosition;
        Vector2 mouseClickPlotPosition;
        EventPoint hoverPoint = null;
        VibrationEvent hoverPointEvent = null;
        EventPoint draggedPoint = null;
        VibrationEvent draggedPointEvent = null;
        float dragMin, dragMax;
        string[] pointDragModes;
        PointDragMode pointDragMode = PointDragMode.FreeMove;
        SnapMode snapMode = SnapMode.None;
                
        // Audio waveform
        AudioClip audioClip;
        Texture2D audioClipTexture;
        bool shouldRepaintWaveform = false;
        bool audioWaveformVisible = false;
        float lastAudioClipPaintedZoom = 1f;
        bool normalizeWaveform;
        float renderScale = 1f;

        // Debug
        bool debugMode;
        bool drawRects;

        #endregion

        bool DebugMode
        {
            get => debugMode;
            set
            {
                debugMode = value;
                if (!debugMode)
                    drawRects = false;
            }
        }

        [MenuItem("Window/AHAP Editor")]
        public static void OpenWindow()
        {
            AHAPEditorWindow window = GetWindow<AHAPEditorWindow>("AHAP Editor");
            var content = EditorGUIUtility.IconContent("d_HoloLensInputModule Icon", "AHAP Editor");
            content.text = "AHAP Editor";
            window.titleContent = content;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Debug Mode"), DebugMode, () => DebugMode = !DebugMode);
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
                pointDragModes[i] = string.Concat(pointDragModes[i].Select(x => char.IsUpper(x) ? $" {x}" : x.ToString())).TrimStart(' ');
            DebugMode = false;
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

            float pointEditAreaWidth = Mathf.Max(bottomPartRect.width - plotAreaWidth - lineDoubleSpacing, 0);
            Rect pointEditAreaRect = new(bottomPartRect.position + new Vector2(plotAreaWidth + lineDoubleSpacing, 0),
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
            Rect yAxisLabelRect = new(MARGIN.x, intensityPlotRect.y - lineHalfHeight - lineSpacing,
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

            #region Mouse handling

            // Zoom and scroll (mouse wheel)
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

            bool mouseOnWindow = mouseOverWindow == this;
            Vector2 plotRectMousePosition = Vector2.zero; // Mouse position inside plot rect mouse is over
            Vector2 plotPosition = Vector2.zero; // Mouse position in plot space with snapping
            Rect mousePlotRect = intensityPlotRect; // Plot rect mouse is on
            Rect otherPlotRect = sharpnessPlotRect; // The other plot rect
            mouseLocation = MouseLocation.Outside;
            if (mouseOnWindow)
            {
                if (intensityPlotRect.Contains(currentEvent.mousePosition))
                {
                    mouseLocation = MouseLocation.IntensityPlot;
                }
                else if (sharpnessPlotRect.Contains(currentEvent.mousePosition))
                {
                    mouseLocation = MouseLocation.SharpnessPlot;
                    mousePlotRect = sharpnessPlotRect;
                    otherPlotRect = intensityPlotRect;
                }
            }

            if (mouseLocation != MouseLocation.Outside)
            {
                plotRectMousePosition = currentEvent.mousePosition - mousePlotRect.position;
                float x = (scrollPosition.x + plotRectMousePosition.x) / plotScrollSize.x * time;
                float y = (plotScreenSize.y - plotRectMousePosition.y) / plotScreenSize.y;
                if (snapMode != SnapMode.None)
                {
                    x = (float)Math.Round(x, (int)snapMode);
                    y = (float)Math.Round(y, (int)snapMode);
                }
                plotPosition = new Vector2(x, y);

                hoverPoint = draggedPoint ?? GetEventPointOnPosition(plotPosition, mouseLocation, out hoverPointEvent);

                if (currentEvent.button == (int)MouseButton.Left) // LMB event
                {
                    if (hoverPoint == null) // Not hovering over point
                    {
                        if (previousMouseState == EventType.MouseUp && currentEvent.type == EventType.MouseDown) // LMB down
                        {
                            mouseClickPosition = currentEvent.mousePosition;
                            mouseClickLocation = mouseLocation;
                            mouseClickPlotPosition = plotPosition;
                            previousMouseState = EventType.MouseDown;
                        }
                        else if (previousMouseState == EventType.MouseDown && currentEvent.type == EventType.MouseDrag) // Start dragging if not between continuous event points
                        {
                            previousMouseState = TryGetContinuousEventOnTime(plotPosition.x, out _) ? EventType.MouseUp : EventType.MouseDrag;
                        }
                        else if (currentEvent.type == EventType.MouseUp && mouseClickLocation == mouseLocation) // LMB up
                        {
                            if (previousMouseState == EventType.MouseDown) // Just clicked
                            {
                                if (TryGetContinuousEventOnTime(plotPosition.x, out ContinuousEvent ce)) // Add point to continuous event if clicked between start and end
                                {
                                    ce.AddPointToCurve(plotPosition, mouseLocation);
                                }
                                else // Add transient event
                                {
                                    events.Add(new TransientEvent(plotPosition.x,
                                        mouseLocation == MouseLocation.IntensityPlot ? plotPosition.y : 0.5f,
                                        mouseLocation == MouseLocation.SharpnessPlot ? plotPosition.y : 0.5f));
                                }
                            }
                            else if (previousMouseState == EventType.MouseDrag && !TryGetContinuousEventOnTime(plotPosition.x, out _))
                            {
                                Vector2 leftPoint = mouseClickPlotPosition;
                                Vector2 rightPoint = plotPosition;
                                if (leftPoint.x > rightPoint.x)
                                    (leftPoint, rightPoint) = (rightPoint, leftPoint);
                                events.Add(new ContinuousEvent(new Vector2(leftPoint.x, rightPoint.x),
                                    mouseLocation == MouseLocation.IntensityPlot ? new Vector2(leftPoint.y, rightPoint.y) : Vector2.one * 0.5f,
                                    mouseLocation == MouseLocation.SharpnessPlot ? new Vector2(leftPoint.y, rightPoint.y) : Vector2.one * 0.5f));
                            }
                            previousMouseState = EventType.MouseUp;
                        }
                    }
                    else if (draggedPoint == null && currentEvent.type == EventType.MouseDown) // Hovering over point - start dragging it
                    {
                        previousMouseState = EventType.MouseDown;
                        draggedPoint = hoverPoint;
                        draggedPointEvent = hoverPointEvent;
                        dragMin = scrollPosition.x / plotScrollSize.x * time + NEIGHBOURING_POINT_OFFSET;
                        dragMax = (scrollPosition.x + plotScreenSize.x) / plotScrollSize.x * time - NEIGHBOURING_POINT_OFFSET;
                        mouseClickLocation = mouseLocation;

                        if (draggedPointEvent is ContinuousEvent continuousEvent)
                        {
                            var curve = mouseLocation == MouseLocation.IntensityPlot ? continuousEvent.IntensityCurve : continuousEvent.SharpnessCurve;
                            if (draggedPoint == curve[0])
                            {
                                var previousEvent = events.FindLast(ev => ev.Time < draggedPoint.Time && ev is ContinuousEvent);
                                if (previousEvent != null)
                                    dragMin = ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + NEIGHBOURING_POINT_OFFSET;
                                dragMax = curve.Find(point => point.Time > draggedPoint.Time).Time - NEIGHBOURING_POINT_OFFSET;
                            }
                            else if (draggedPoint == curve.Last())
                            {
                                var nextEvent = events.Find(ev => ev.Time > draggedPoint.Time && ev is ContinuousEvent);
                                if (nextEvent != null)
                                    dragMax = nextEvent.Time - NEIGHBOURING_POINT_OFFSET;
                                dragMin = curve.FindLast(point => point.Time < draggedPoint.Time).Time + NEIGHBOURING_POINT_OFFSET;
                            }
                            else
                            {
                                dragMin = curve.FindLast(point => point.Time < draggedPoint.Time).Time + NEIGHBOURING_POINT_OFFSET;
                                dragMax = curve.Find(point => point.Time > draggedPoint.Time).Time - NEIGHBOURING_POINT_OFFSET;
                            }
                        }
                    }
                    else if (draggedPoint != null) // Dragging point
                    {
                        if (currentEvent.type == EventType.MouseDrag && mouseLocation == mouseClickLocation) // Handle dragging
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
                            previousMouseState = EventType.MouseDrag;
                        }
                    }
                }
                else if (currentEvent.type == EventType.MouseUp && hoverPoint != null &&
                    ((currentEvent.button == (int)MouseButton.Right && hoverPointEvent.ShouldRemoveEventAfterRemovingPoint(hoverPoint, mouseLocation)) ||
                    currentEvent.button == (int)MouseButton.Middle)) // Delete point
                {
                    events.Remove(hoverPointEvent);
                    hoverPoint = null;
                    hoverPointEvent = null;
                }
            }
            if (currentEvent.button == (int)MouseButton.Left && currentEvent.type == EventType.MouseUp)
            {
                draggedPoint = null;
                draggedPointEvent = null;
                previousMouseState = EventType.MouseUp;
                if (mouseLocation == MouseLocation.Outside)
                {
                    hoverPoint = null;
                    hoverPointEvent = null;
                }
            }
            
            #endregion

            #region Draw Top Bar

            GUILayout.BeginArea(topBarRect, EditorStyles.helpBox);
            GUILayout.BeginHorizontal();

            // GUI Debug
            if (debugMode)
            {
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
                    sb.AppendLine($"Y axis label rect: {yAxisLabelRect} (white)");
                    sb.AppendLine($"X axis label rect: {new Rect(xAxisLabelRect.position + intensityPlotRect.position, xAxisLabelRect.size)} (white)");
                    Debug.Log(sb.ToString());
                }
                GUIContent drawRectsLabel = new("Draw Rects");
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(drawRectsLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                drawRects = EditorGUILayout.Toggle(drawRectsLabel, drawRects, topBarContainerThirdOption);
                GUILayout.EndVertical();
            }

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
            audioClip = EditorGUILayout.ObjectField(GUIContent.none, audioClip, typeof(AudioClip), false) as AudioClip;
            if (EditorGUI.EndChangeCheck())
            {
                AudioClipUtils.StopAllClips();
                audioWaveformVisible = false;
            }
            GUI.enabled = audioClip != null;
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"), EditorStyles.iconButton))
                AudioClipUtils.PlayClip(audioClip);
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.waveformVisibleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            EditorGUI.BeginChangeCheck();
            audioWaveformVisible = EditorGUILayout.Toggle(Content.waveformVisibleLabel, audioWaveformVisible, topBarContainerThirdOption);
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
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.zoomLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
            zoom = (float)Math.Round(EditorGUILayout.Slider(Content.zoomLabel, zoom, 1, MAX_ZOOM), 1);
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

            #region Draw Plot Area

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

            // Y axis labels and horizontal grid
            Vector3 gridPoint1 = new(intensityPlotRect.x, intensityPlotRect.y);
            Vector3 gridPoint2 = new(intensityPlotRect.x + intensityPlotRect.width, intensityPlotRect.y);
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
            gridPoint1 = Vector2.zero;
            gridPoint2 = new(0, plotScreenSize.y);
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

            // Events
            foreach (var vibrationEvent in events)
            {
                if (vibrationEvent is TransientEvent transientEvent)
                {
                    Handles.color = COLOR_EVENT_TRANSIENT;

                    Vector3 intensityPoint = PointToPlotCoords(transientEvent.Time, transientEvent.Intensity.Value);
                    Handles.DrawSolidDisc(intensityPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, intensityPoint, new Vector3(intensityPoint.x, plotScreenSize.y));

                    Vector3 sharpnessPoint = PointToPlotCoords(transientEvent.Time, transientEvent.Sharpness.Value, plotHeightOffset);
                    Handles.DrawSolidDisc(sharpnessPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                    Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, sharpnessPoint, new Vector3(sharpnessPoint.x, plotHeightOffset + plotScreenSize.y));
                }
                else if (vibrationEvent is ContinuousEvent continuousEvent)
                {
                    Handles.color = COLOR_EVENT_CONTINUOUS;
                    DrawContinuousEvent(continuousEvent.IntensityCurve);
                    DrawContinuousEvent(continuousEvent.SharpnessCurve, plotHeightOffset);

                    void DrawContinuousEvent(List<EventPoint> curve, float heightOffset = 0)
                    {
                        List<Vector3> points = new() { PointToPlotCoords(curve[0].Time, 0, heightOffset) };
                        int pointCount = curve.Count;
                        for (int i = 0; i < pointCount; i++)
                        {
                            EventPoint point = curve[i];
                            points.Add(PointToPlotCoords(point.Time, point.Value, heightOffset));
                            Handles.DrawSolidDisc(points.Last(), POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                        }
                        points.Add(PointToPlotCoords(curve[pointCount - 1].Time, 0, heightOffset));
                        Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, points.ToArray());
                    }
                }
            }

            GUI.EndScrollView();

            if (mouseLocation != MouseLocation.Outside)
            {
                // Hover helper lines
                Handles.color = COLOR_HOVER_GUIDES;
                Handles.DrawLine(new Vector3(currentEvent.mousePosition.x, otherPlotRect.y),
                    new Vector3(currentEvent.mousePosition.x, otherPlotRect.y + otherPlotRect.height));
                Handles.DrawLine(new Vector3(currentEvent.mousePosition.x, mousePlotRect.y),
                    new Vector3(currentEvent.mousePosition.x, mousePlotRect.y + mousePlotRect.height));
                Handles.DrawLine(new Vector3(mousePlotRect.x, currentEvent.mousePosition.y),
                    new Vector3(mousePlotRect.x + mousePlotRect.width, currentEvent.mousePosition.y));
                Handles.DrawSolidDisc(new Vector3(currentEvent.mousePosition.x, currentEvent.mousePosition.y),
                    POINT_NORMAL, HOVER_DOT_SIZE);

                // Continuous event creation
                if (draggedPoint == null && currentEvent.button == (int)MouseButton.Left && previousMouseState == EventType.MouseDrag && mouseLocation == mouseClickLocation)
                {
                    Vector3 leftPoint = currentEvent.mousePosition;
                    Vector3 rightPoint = mouseClickPosition;
                    if (mouseClickPosition.x < currentEvent.mousePosition.x)
                    {
                        leftPoint = mouseClickPosition;
                        rightPoint = currentEvent.mousePosition;
                    }
                    Handles.color = COLOR_EVENT_CONTINUOUS_CREATION;
                    Handles.DrawAAConvexPolygon(leftPoint, rightPoint, new Vector3(rightPoint.x, mousePlotRect.y + mousePlotRect.height),
                        new Vector3(leftPoint.x, mousePlotRect.y + mousePlotRect.height), leftPoint);
                }
            }

            // Drag and hover point highlight
            EventPoint highlightedPoint = null;
            float highlightSize = 0f;
            Rect highlightRect = mousePlotRect;
            if (draggedPoint != null)
            {
                Handles.color = COLOR_DRAG_POINT;
                highlightedPoint = draggedPoint;
                highlightSize = DRAG_HIGHLIGHT_SIZE;
                highlightRect = mouseClickLocation == MouseLocation.IntensityPlot ? intensityPlotRect : sharpnessPlotRect;
            }
            else if (hoverPoint != null && mouseLocation != MouseLocation.Outside)
            {
                Handles.color = COLOR_HOVER_POINT;
                highlightedPoint = hoverPoint;
                highlightSize = HOVER_HIGHLIGHT_SIZE;
            }
            if (highlightedPoint != null)
            {
                Vector3 highlightedPointCoords = new(highlightRect.x + highlightedPoint.Time / time * plotScrollSize.x - scrollPosition.x,
                    highlightRect.y + highlightRect.height - highlightedPoint.Value * highlightRect.height);
                Handles.DrawSolidDisc(highlightedPointCoords, POINT_NORMAL, highlightSize);
            }

            // Plot borders
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

            #region Draw Side Panel

            GUILayout.BeginArea(pointEditAreaRect, EditorStyles.helpBox);
            GUILayout.BeginVertical();


            GUILayout.FlexibleSpace();

            // Hover info
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Hover info", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Plot");
            GUILayout.Label(mouseLocation == MouseLocation.Outside ? "-" : mouseLocation.ToString(), Content.yAxisLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Time");
            GUILayout.Label(mouseLocation == MouseLocation.Outside ? "-" : plotPosition.x.ToString(), Content.yAxisLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Value");
            GUILayout.Label(mouseLocation == MouseLocation.Outside ? "-" : plotPosition.y.ToString(), Content.yAxisLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndVertical();
            GUILayout.EndArea();

            #endregion

            if (mouseOnWindow)
                Repaint();
        }

        #region Helper functions

        private Vector3 PointToPlotCoords(float time, float value, float heightOffset = 0)
        {
            return new Vector3(time / this.time * plotScrollSize.x, 
                plotScreenSize.y - value * plotScreenSize.y + heightOffset);
        }

        private bool TryGetContinuousEventOnTime(float time, out ContinuousEvent continuousEvent)
        {
            continuousEvent = null;
            foreach (var ev in events)
            {
                if (time > ev.Time && ev is ContinuousEvent ce && time < ce.IntensityCurve.Last().Time)
                {
                    continuousEvent = ce;
                    return true;
                }                    
            }
            return false;
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
                t = Mathf.Max(t, lastPointTime);
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
