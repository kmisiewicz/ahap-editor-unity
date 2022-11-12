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

        enum SnapMode 
        { 
            None = 0,
            [InspectorName("0.1")] Tenth = 1,
            [InspectorName("0.01")] Hundredth = 2,
            [InspectorName("0.001")] Thousandth = 3 
        }

        enum PointDragMode { FreeMove = 0, LockTime = 1, LockValue = 2 }

        enum MouseButton { Left = 0, Right = 1, Middle = 2 }

        // Const settings
        const float MIN_TIME = 0.1f;
        const float MAX_TIME = 30f;
        const float HOVER_OFFSET = 5f;
        const float ZOOM_INCREMENT = 0.1f;
        const float MAX_ZOOM = 7f;
        const float SCROLL_INCREMENT = 0.05f;
        const float HOVER_HIGHLIGHT_SIZE = 9f;
        const float DRAG_HIGHLIGHT_SIZE = 13f;
        const float SELECT_HIGHLIGHT_SIZE = 12f;
        const float HOVER_DOT_SIZE = 3f;
        const float PLOT_EVENT_POINT_SIZE = 5f;
        const float PLOT_EVENT_LINE_WIDTH = 4f;
        const float PLOT_BORDER_WIDTH = 5f;
        const float NEIGHBOURING_POINT_OFFSET = 0.001f;
        const float MIN_WAVEFORM_RENDER_SCALE = 0.1f;
        const float MAX_WAVEFORM_RENDER_SCALE = 2f;
        const float CUSTOM_LABEL_WIDTH_OFFSET = 3;
        const float PLOT_AREA_BASE_WIDTH = 0.85f;
        const float PLOT_AREA_MIN_WIDTH = 0.5f;
        const float PLOT_AREA_MAX_WIDTH = 0.9f;
        const float TOP_BAR_OPTIONS_SIZE_FACTOR = 0.12f * 16 / 9;

        static readonly Vector3 POINT_NORMAL = new(0, 0, 1);
        static readonly Vector2 CONTENT_MARGIN = new(3, 2);

        class Colors
        {
            public static readonly Color plotBorder = Color.white;
            public static readonly Color plotGrid = Color.gray;
            public static readonly Color waveform = new(0.42f, 1f, 0f, 0.2f);
            public static readonly Color waveformBg = Color.clear;
            public static readonly Color eventTransient = new(0.22f, 0.6f, 1f);
            public static readonly Color eventContinuous = new(1f, 0.6f, 0.2f);
            public static readonly Color eventContinuousCreation = new(1f, 0.6f, 0.2f, 0.5f);
            public static readonly Color hoverPoint = new(0.8f, 0.8f, 0.8f, 0.2f);
            public static readonly Color draggedPoint = new(0.7f, 1f, 1f, 0.3f);
            public static readonly Color selectedPoint = new(1f, 1f, 0f, 0.3f);
            public static readonly Color hoverGuides = new(0.7f, 0f, 0f);
        }

        class Content
        {
            public static readonly GUIContent waveformVisibleLabel = EditorGUIUtility.TrTextContent("Visible");
            public static readonly GUIContent normalizeLabel = EditorGUIUtility.TrTextContent("Normalize");
            public static readonly GUIContent renderScaleLabel = EditorGUIUtility.TrTextContent("Render scale",
                "Lower scale will reduce quality but improve render time.");
            public static readonly GUIContent projectNameLabel = EditorGUIUtility.TrTextContent("Project name",
                "Name that will be save in project's metadata.");
            public static readonly GUIContent timeLabel = EditorGUIUtility.TrTextContent("Time");
            public static readonly GUIContent zoomLabel = EditorGUIUtility.TrTextContent("Zoom");
            public static readonly GUIContent drawRectsLabel = EditorGUIUtility.TrTextContent("Draw Rects");
            public static readonly GUIContent snappingLabel = EditorGUIUtility.TrTextContent("Snapping");
            public static readonly GUIContent yAxisLabelDummy = EditorGUIUtility.TrTextContent("#.##");
            public static readonly GUIContent xAxisLabelDummy = EditorGUIUtility.TrTextContent("##.###");
        }

        class Styles
        {
            public static readonly GUIStyle plotTitleStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            public static readonly GUIStyle yAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            public static readonly GUIStyle xAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }

        // Data
        TextAsset _ahapFile;
        string _projectName = "";
        List<VibrationEvent> _events = new();

        // Drawing
        float _time, _zoom;
        Vector2 _plotScreenSize, _plotScrollSize, _scrollPosition;
        float _plotHeightOffset; // Height difference between plots
        EventType _previousMouseState = EventType.MouseUp;
        MouseLocation _mouseLocation, _mouseClickLocation, _selectedPointLocation;
        Vector2 _mouseClickPosition, _mouseClickPlotPosition;
        EventPoint _hoverPoint, _draggedPoint;
        VibrationEvent _hoverPointEvent, _draggedPointEvent;
        List<EventPoint> _selectedPoints;
        float _dragMin, _dragMax;
        string[] _pointDragModes;
        PointDragMode _pointDragMode = PointDragMode.FreeMove;
        SnapMode _snapMode = SnapMode.None;
        bool _pointEditAreaVisible, _pointEditAreaResize; 
        float _plotAreaWidthFactor, _plotAreaWidthFactorOffset;
                
        // Audio waveform
        AudioClip _audioClip;
        Texture2D _audioClipTexture;
        bool _audioWaveformVisible, _shouldRepaintWaveform, _normalizeWaveform;
        float _lastAudioClipPaintedZoom, _renderScale;

        // Debug
        bool _debugMode, _drawRects;

        #endregion

        bool DebugMode
        {
            get => _debugMode;
            set
            {
                _debugMode = value;
                if (!_debugMode)
                    _drawRects = false;
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
            _ahapFile = null;
            _projectName = "";
            _audioClip = null;
            _renderScale = _lastAudioClipPaintedZoom = 1f;
            _audioWaveformVisible = _normalizeWaveform = _shouldRepaintWaveform = false;
            _pointDragModes = Enum.GetNames(typeof(PointDragMode));
            for (int i = 0; i < _pointDragModes.Length; i++)
                _pointDragModes[i] = string.Concat(_pointDragModes[i].Select(x => char.IsUpper(x) ? $" {x}" : x.ToString())).TrimStart(' ');
            _plotAreaWidthFactor = PLOT_AREA_BASE_WIDTH;
            _pointEditAreaVisible = false;

            DebugMode = false;
        }

        private void Clear()
        {
            _events ??= new List<VibrationEvent>();
            _events.Clear();
            _selectedPoints ??= new List<EventPoint>();
            _selectedPoints.Clear();
            _time = _zoom = 1f;
        }

        private void OnGUI()
		{
            #region Size and positions calculations

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float lineSpacing = EditorGUIUtility.standardVerticalSpacing;
            float lineWithSpacing = lineHeight + lineSpacing;
            float lineHalfHeight = lineHeight * 0.5f;
            float lineDoubleSpacing = lineSpacing * 2;

            float topBarHeight = lineWithSpacing * 4 + lineHalfHeight + lineDoubleSpacing;
            Rect topBarRect = new(CONTENT_MARGIN, new Vector2(position.width - CONTENT_MARGIN.x * 2, topBarHeight));
            float topBarOptionsContainerWidth = Screen.currentResolution.height * TOP_BAR_OPTIONS_SIZE_FACTOR;
            var topBarMaxWidthOption = GUILayout.MaxWidth(topBarOptionsContainerWidth);
            var topBarContainerHalfOption = GUILayout.MaxWidth(topBarOptionsContainerWidth * 0.5f);
            var topBarContainerThirdOption = GUILayout.MaxWidth(topBarOptionsContainerWidth * 0.33f);

            float bottomPartHeight = position.height - CONTENT_MARGIN.y * 2 - topBarHeight - lineDoubleSpacing * 2;
            Rect bottomPartRect = new(CONTENT_MARGIN.x, CONTENT_MARGIN.y + topBarHeight + lineDoubleSpacing, topBarRect.width, bottomPartHeight);

            float plotAreaWidth = bottomPartRect.width * (_pointEditAreaVisible ? _plotAreaWidthFactor : 1);
            Rect plotAreaRect = new(bottomPartRect.position, new Vector2(plotAreaWidth, bottomPartRect.height));

            float pointEditAreaWidth = Mathf.Max(bottomPartRect.width - plotAreaWidth - lineDoubleSpacing, 0);
            Rect pointEditAreaRect = new(bottomPartRect.position + new Vector2(plotAreaWidth + lineDoubleSpacing, 0),
                new Vector2(pointEditAreaWidth, bottomPartHeight));

            Rect resizeBarRect = new(plotAreaRect.xMax, plotAreaRect.y + PLOT_BORDER_WIDTH,
                lineDoubleSpacing * 2f, plotAreaRect.height - 2 * PLOT_BORDER_WIDTH);

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
            _plotScreenSize = intensityPlotAreaRect.size - plotOffsetLeftTop - plotOffsetRightBottom;
            _plotScreenSize.y -= lineSpacing;
            Rect intensityPlotRect = new(intensityPlotAreaRect.position + plotOffsetLeftTop, _plotScreenSize);
            intensityPlotRect.y += lineSpacing;
            Rect sharpnessPlotRect = new(sharpnessPlotAreaRect.position + plotOffsetLeftTop, _plotScreenSize);
            _plotHeightOffset = sharpnessPlotRect.y - intensityPlotRect.y;

            Rect scrollRect = new(intensityPlotRect.x, intensityPlotRect.y,
                plotAreaRect.width - plotOffsetLeftTop.x - plotOffsetRightBottom.x,
                plotAreaRect.height - plotOffsetLeftTop.y - lineDoubleSpacing);
            _plotScrollSize = new Vector2(scrollRect.width * _zoom, intensityPlotRect.height);
            Rect scrollPlotRect = new(0, 0, _plotScrollSize.x, _plotScreenSize.y);
            Rect scrollContentRect = new(0, 0, _plotScrollSize.x, scrollRect.height);

            int xAxisLabelCount = (int)(_plotScrollSize.x / xAxisLabelSize.x);
            float xAxisLabelWidthInterval = _plotScrollSize.x / xAxisLabelCount;
            Rect xAxisLabelRect = new(0, _plotScreenSize.y + lineDoubleSpacing, xAxisLabelSize.x, xAxisLabelSize.y);
            float xAxisLabelInterval = _time / xAxisLabelCount;

            int yAxisLabelCount = Mathf.RoundToInt(Mathf.Clamp(intensityPlotRect.height / yAxisLabelSize.y, 2, 11));
            if (yAxisLabelCount < 11)
            {
                if (yAxisLabelCount >= 6) yAxisLabelCount = 6;
                else if (yAxisLabelCount >= 4) yAxisLabelCount = 5;
            }
            float yAxisLabelInterval = 1f / (yAxisLabelCount - 1);
            float yAxisLabelHeightInterval = _plotScreenSize.y / (yAxisLabelCount - 1);
            Rect yAxisLabelRect = new(CONTENT_MARGIN.x, intensityPlotRect.y - lineHalfHeight - lineSpacing,
                plotOffsetLeftTop.x - CUSTOM_LABEL_WIDTH_OFFSET, lineHeight);

            // Debug
            if (_drawRects)
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
                EditorGUI.DrawRect(resizeBarRect, Color.white);
            }

            #endregion

            #region Mouse handling

            var currentEvent = UnityEngine.Event.current;

            // Zoom and scroll (mouse wheel)
            if (currentEvent.type == EventType.ScrollWheel)
            {
                if (currentEvent.control)
                {
                    _zoom += -Mathf.Sign(currentEvent.delta.y) * ZOOM_INCREMENT;
                    _zoom = Mathf.Clamp(_zoom, 1, MAX_ZOOM);
                }
                else if (_zoom > 1f)
                {
                    _scrollPosition.x += _plotScrollSize.x * (Mathf.Sign(currentEvent.delta.y) * SCROLL_INCREMENT);
                    _scrollPosition.x = Mathf.Clamp(_scrollPosition.x, 0, _plotScrollSize.x - _plotScreenSize.x);
                }
                currentEvent.Use();
            }

            bool mouseOnWindow = mouseOverWindow == this;
            Vector2 plotRectMousePosition = Vector2.zero; // Mouse position inside plot rect mouse is over
            Vector2 plotPosition = Vector2.zero; // Mouse position in plot space with snapping
            Rect mousePlotRect = intensityPlotRect; // Plot rect mouse is on
            Rect otherPlotRect = sharpnessPlotRect; // The other plot rect
            _mouseLocation = MouseLocation.Outside;
            if (mouseOnWindow)
            {
                if (intensityPlotRect.Contains(currentEvent.mousePosition))
                {
                    _mouseLocation = MouseLocation.IntensityPlot;
                }
                else if (sharpnessPlotRect.Contains(currentEvent.mousePosition))
                {
                    _mouseLocation = MouseLocation.SharpnessPlot;
                    mousePlotRect = sharpnessPlotRect;
                    otherPlotRect = intensityPlotRect;
                }
            }

            if (_mouseLocation != MouseLocation.Outside)
            {
                plotRectMousePosition = currentEvent.mousePosition - mousePlotRect.position;
                float x = (_scrollPosition.x + plotRectMousePosition.x) / _plotScrollSize.x * _time;
                float y = (_plotScreenSize.y - plotRectMousePosition.y) / _plotScreenSize.y;
                if (_snapMode != SnapMode.None)
                {
                    x = (float)Math.Round(x, (int)_snapMode);
                    y = (float)Math.Round(y, (int)_snapMode);
                }
                plotPosition = new Vector2(x, y);

                _hoverPoint = _draggedPoint ?? GetEventPointOnPosition(plotPosition, _mouseLocation, out _hoverPointEvent);

                if (currentEvent.button == (int)MouseButton.Left) // LMB event
                {
                    if (_hoverPoint == null) // Not hovering over point
                    {
                        if (_previousMouseState == EventType.MouseUp && currentEvent.type == EventType.MouseDown) // LMB down
                        {
                            _selectedPoints.Clear();
                            _mouseClickPosition = currentEvent.mousePosition;
                            _mouseClickLocation = _mouseLocation;
                            _mouseClickPlotPosition = plotPosition;
                            _previousMouseState = EventType.MouseDown;
                        }
                        else if (_previousMouseState == EventType.MouseDown && currentEvent.type == EventType.MouseDrag) // Start dragging if not between continuous event points
                        {
                            _previousMouseState = TryGetContinuousEventOnTime(plotPosition.x, out _) ? EventType.MouseUp : EventType.MouseDrag;
                        }
                        else if (currentEvent.type == EventType.MouseUp && _mouseClickLocation == _mouseLocation) // LMB up
                        {
                            if (_previousMouseState == EventType.MouseDown) // Just clicked
                            {
                                if (TryGetContinuousEventOnTime(plotPosition.x, out ContinuousEvent ce)) // Add point to continuous event if clicked between start and end
                                    ce.AddPointToCurve(plotPosition, _mouseLocation);
                                else // Add transient event
                                    _events.Add(new TransientEvent(plotPosition, _mouseLocation));
                            }
                            else if (_previousMouseState == EventType.MouseDrag && !TryGetContinuousEventOnTime(plotPosition.x, out _))
                            {
                                Vector2 endPoint = currentEvent.shift ? new Vector2(plotPosition.x, _mouseClickPlotPosition.y) : plotPosition;
                                _events.Add(new ContinuousEvent(_mouseClickPlotPosition, endPoint, _mouseLocation));
                            }
                            _previousMouseState = EventType.MouseUp;
                        }
                    }
                    else if (_draggedPoint == null && currentEvent.type == EventType.MouseDown) // Hovering over point - start dragging it
                    {
                        _selectedPoints.Clear();
                        _previousMouseState = EventType.MouseDown;
                        _draggedPoint = _hoverPoint;
                        _draggedPointEvent = _hoverPointEvent;
                        _dragMin = _scrollPosition.x / _plotScrollSize.x * _time + NEIGHBOURING_POINT_OFFSET;
                        _dragMax = (_scrollPosition.x + _plotScreenSize.x) / _plotScrollSize.x * _time - NEIGHBOURING_POINT_OFFSET;
                        _mouseClickLocation = _mouseLocation;

                        if (_draggedPointEvent is ContinuousEvent continuousEvent)
                        {
                            var curve = _mouseLocation == MouseLocation.IntensityPlot ? continuousEvent.IntensityCurve : continuousEvent.SharpnessCurve;
                            if (_draggedPoint == curve[0])
                            {
                                var previousEvent = _events.FindLast(ev => ev.Time < _draggedPoint.Time && ev is ContinuousEvent);
                                if (previousEvent != null)
                                    _dragMin = ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + NEIGHBOURING_POINT_OFFSET;
                                _dragMax = curve.Find(point => point.Time > _draggedPoint.Time).Time - NEIGHBOURING_POINT_OFFSET;
                            }
                            else if (_draggedPoint == curve.Last())
                            {
                                var nextEvent = _events.Find(ev => ev.Time > _draggedPoint.Time && ev is ContinuousEvent);
                                if (nextEvent != null)
                                    _dragMax = nextEvent.Time - NEIGHBOURING_POINT_OFFSET;
                                _dragMin = curve.FindLast(point => point.Time < _draggedPoint.Time).Time + NEIGHBOURING_POINT_OFFSET;
                            }
                            else
                            {
                                _dragMin = curve.FindLast(point => point.Time < _draggedPoint.Time).Time + NEIGHBOURING_POINT_OFFSET;
                                _dragMax = curve.Find(point => point.Time > _draggedPoint.Time).Time - NEIGHBOURING_POINT_OFFSET;
                            }
                        }
                    }
                    else if (_draggedPoint != null) // Dragging point
                    {
                        if (currentEvent.type == EventType.MouseDrag && _mouseLocation == _mouseClickLocation) // Handle dragging
                        {
                            if (_pointDragMode != PointDragMode.LockTime && !currentEvent.alt)
                                _draggedPoint.Time = Mathf.Clamp(plotPosition.x, _dragMin, _dragMax);
                            if (_pointDragMode != PointDragMode.LockValue && !currentEvent.shift)
                                _draggedPoint.Value = Mathf.Clamp(plotPosition.y, 0, 1);
                            if (_draggedPointEvent is TransientEvent te)
                            {
                                te.Intensity.Time = te.Sharpness.Time = _draggedPoint.Time;
                            }
                            else if (_draggedPointEvent is ContinuousEvent ce)
                            {
                                if (_draggedPoint == ce.IntensityCurve.First() || _draggedPoint == ce.SharpnessCurve.First())
                                    ce.IntensityCurve.First().Time = ce.SharpnessCurve.First().Time = _draggedPoint.Time;
                                else if (_draggedPoint == ce.IntensityCurve.Last() || _draggedPoint == ce.SharpnessCurve.Last())
                                    ce.IntensityCurve.Last().Time = ce.SharpnessCurve.Last().Time = _draggedPoint.Time;
                            }
                            _previousMouseState = EventType.MouseDrag;
                        }
                    }
                }
                else if (currentEvent.type == EventType.MouseUp && _hoverPoint != null &&
                    ((currentEvent.button == (int)MouseButton.Right && _hoverPointEvent.ShouldRemoveEventAfterRemovingPoint(_hoverPoint, _mouseLocation)) ||
                    currentEvent.button == (int)MouseButton.Middle)) // Delete point
                {
                    if (_selectedPoints.Count > 0 && _selectedPoints[0] == _hoverPoint)
                        _selectedPoints.Remove(_hoverPoint);
                    _events.Remove(_hoverPointEvent);
                    _hoverPoint = null;
                    _hoverPointEvent = null;
                }
            }
            if (currentEvent.button == (int)MouseButton.Left && currentEvent.type == EventType.MouseUp)
            {
                if (_draggedPoint != null)
                {
                    _selectedPoints.Insert(0, _draggedPoint);
                    _selectedPointLocation = _mouseLocation;
                }
                _draggedPoint = null;
                _draggedPointEvent = null;
                _previousMouseState = EventType.MouseUp;
                if (_mouseLocation == MouseLocation.Outside)
                {
                    _hoverPoint = null;
                    _hoverPointEvent = null;
                }
                _pointEditAreaResize = false;
            }

            // Handle point edit area resizing
            if (_pointEditAreaVisible)
            {
                if (!_pointEditAreaResize && resizeBarRect.Contains(currentEvent.mousePosition))
                {
                    EditorGUIUtility.AddCursorRect(resizeBarRect, MouseCursor.ResizeHorizontal);
                    if (currentEvent.button == (int)MouseButton.Left && currentEvent.type == EventType.MouseDown)
                    {
                        _pointEditAreaResize = true;
                        _plotAreaWidthFactorOffset = _plotAreaWidthFactor * bottomPartRect.width - currentEvent.mousePosition.x;
                    }
                }

                if (_pointEditAreaResize)
                {
                    EditorGUIUtility.AddCursorRect(new Rect(Vector2.zero, position.size), MouseCursor.ResizeHorizontal);
                    _plotAreaWidthFactor = Mathf.Clamp((currentEvent.mousePosition.x + _plotAreaWidthFactorOffset) / bottomPartRect.width,
                        PLOT_AREA_MIN_WIDTH, PLOT_AREA_MAX_WIDTH);
                }
            }
            
            #endregion

            #region Draw Top Bar

            GUILayout.BeginArea(topBarRect, EditorStyles.helpBox);
            GUILayout.BeginHorizontal();

            // GUI Debug
            if (_debugMode)
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
                    sb.AppendLine($"Resize bar: {resizeBarRect} (white)");
                    Debug.Log(sb.ToString());
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label(Content.drawRectsLabel);
                _drawRects = GUILayout.Toggle(_drawRects, GUIContent.none);
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();

                GUILayout.EndVertical();
            }

            // File
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField("File", EditorStyles.boldLabel);

                _ahapFile = EditorGUILayout.ObjectField(GUIContent.none, _ahapFile, typeof(TextAsset), false) as TextAsset;

                GUILayout.BeginHorizontal();
                GUI.enabled = _ahapFile != null;
                if (GUILayout.Button("Import"))
                    HandleImport();
                GUI.enabled = true;
                if (GUILayout.Button("Save"))
                    HandleSaving();
                GUILayout.EndHorizontal();

                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.projectNameLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _projectName = EditorGUILayout.TextField(Content.projectNameLabel, _projectName);
            }
            GUILayout.EndVertical();

            // Audio waveform
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField("Reference waveform", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _audioClip = EditorGUILayout.ObjectField(GUIContent.none, _audioClip, typeof(AudioClip), false) as AudioClip;
                if (EditorGUI.EndChangeCheck())
                {
                    AudioClipUtils.StopAllClips();
                    _audioWaveformVisible = false;
                    if (_audioClip != null) 
                    {
                        if (_audioClip.length > MAX_TIME)
                        {
                            _audioClip = null;
                            EditorUtility.DisplayDialog("Clip too long", $"Selected audio clip is longer than max allowed {MAX_TIME}s.", "OK");
                        }
                    }
                }
                GUI.enabled = _audioClip != null;
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"), EditorStyles.miniButton, GUILayout.MaxWidth(30)))
                    AudioClipUtils.PlayClip(_audioClip);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _audioWaveformVisible = GUILayout.Toggle(_audioWaveformVisible, Content.waveformVisibleLabel, GUI.skin.button, topBarContainerHalfOption);
                _normalizeWaveform = GUILayout.Toggle(_normalizeWaveform, Content.normalizeLabel, GUI.skin.button, topBarContainerHalfOption);
                if (EditorGUI.EndChangeCheck())
                {
                    if (_audioWaveformVisible) _shouldRepaintWaveform = true;
                    else _lastAudioClipPaintedZoom = 0;
                }                    
                GUILayout.EndHorizontal();

                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.renderScaleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _renderScale = Mathf.Clamp(EditorGUILayout.FloatField(Content.renderScaleLabel, _renderScale),
                    MIN_WAVEFORM_RENDER_SCALE, MAX_WAVEFORM_RENDER_SCALE);
                GUI.enabled = true;
            }
            GUILayout.EndVertical();

            // Plot view
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField("Plot view", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset zoom", topBarContainerThirdOption))
                    _zoom = 1;
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.zoomLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _zoom = (float)Math.Round(EditorGUILayout.Slider(Content.zoomLabel, _zoom, 1, MAX_ZOOM), 1);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                //if (GUILayout.Button("Clear", topBarContainerThirdOption))
                //    Clear();
                if (GUILayout.Button("Trim", topBarContainerThirdOption))
                    _time = (_audioClip != null && _audioWaveformVisible) ? _audioClip.length : GetLastPointTime();
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.timeLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _time = Mathf.Clamp(Mathf.Max(EditorGUILayout.FloatField(Content.timeLabel, _time), GetLastPointTime()), MIN_TIME, MAX_TIME);
                if (_audioClip != null)
                    _time = Mathf.Max(_time, _audioClip.length);
                EditorGUIUtility.labelWidth = 0;
                GUILayout.EndHorizontal();

                EditorGUILayout.GetControlRect();
            }
            GUILayout.EndVertical();

            // Point editing
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField("Point editing", EditorStyles.boldLabel);

                _pointDragMode = (PointDragMode)GUILayout.Toolbar((int)_pointDragMode, _pointDragModes);

                _pointEditAreaVisible = GUILayout.Toggle(_pointEditAreaVisible, "Advanced panel", GUI.skin.button);

                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.snappingLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _snapMode = (SnapMode)EditorGUILayout.EnumPopup("Snapping", _snapMode);
                EditorGUIUtility.labelWidth = 0;
            }
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
            GUILayout.Label("Intensity", Styles.plotTitleStyle);
            GUILayout.EndArea();
            GUILayout.BeginArea(sharpnessPlotAreaRect);
            GUILayout.Space(lineSpacing);
            GUILayout.Label("Sharpness", Styles.plotTitleStyle);
            GUILayout.EndArea();

            // Y axis labels and horizontal grid
            Vector3 gridPoint1 = new(intensityPlotRect.x, intensityPlotRect.y);
            Vector3 gridPoint2 = new(intensityPlotRect.xMax, intensityPlotRect.y);
            Handles.color = Colors.plotGrid;
            for (int i = 0; i < yAxisLabelCount; i++)
            {
                string valueLabel = (1 - i * yAxisLabelInterval).ToString("0.##");
                GUI.Label(yAxisLabelRect, valueLabel, Styles.yAxisLabelStyle);
                Handles.DrawLine(gridPoint1, gridPoint2);
                yAxisLabelRect.y += _plotHeightOffset;
                gridPoint1.y = gridPoint2.y = gridPoint1.y + _plotHeightOffset;
                GUI.Label(yAxisLabelRect, valueLabel, Styles.yAxisLabelStyle);
                Handles.DrawLine(gridPoint1, gridPoint2);
                yAxisLabelRect.y += yAxisLabelHeightInterval - _plotHeightOffset;
                gridPoint1.y = gridPoint2.y = gridPoint1.y - _plotHeightOffset + yAxisLabelHeightInterval;
            }

            // Scroll view
            _scrollPosition = GUI.BeginScrollView(scrollRect, _scrollPosition, scrollContentRect,
                true, false, GUI.skin.horizontalScrollbar, GUIStyle.none);

            // Audio waveform
            if (_audioClip != null && _audioWaveformVisible)
            {
                if (Mathf.Abs(_zoom - _lastAudioClipPaintedZoom) > 0.5f || _shouldRepaintWaveform)
                {
                    _audioClipTexture = AudioClipUtils.PaintAudioWaveform(_audioClip, (int)(_plotScrollSize.x * _renderScale),
                        (int)(_plotScrollSize.y * _renderScale), Colors.waveformBg, Colors.waveform, _normalizeWaveform);
                    _lastAudioClipPaintedZoom = _zoom;
                    _shouldRepaintWaveform = false;
                }
                Rect audioTextureRect = new(scrollPlotRect);
                audioTextureRect.width *= _audioClip.length / _time;
                GUI.DrawTexture(audioTextureRect, _audioClipTexture, ScaleMode.StretchToFill);
                audioTextureRect.y += _plotHeightOffset;
                GUI.DrawTexture(audioTextureRect, _audioClipTexture, ScaleMode.StretchToFill);
            }

            // X axis labels and vertical grid
            gridPoint1 = Vector2.zero;
            gridPoint2 = new(0, _plotScreenSize.y);
            Handles.color = Colors.plotGrid;
            float timeLabelValue = 0;
            string timeLabel = "0";
            DrawTimeLabels(GUI.skin.label);
            xAxisLabelRect.x -= xAxisLabelRect.width * 0.5f;
            for (int i = 1; i < xAxisLabelCount; i++)
            {
                timeLabelValue += xAxisLabelInterval;
                timeLabel = timeLabelValue.ToString("#0.###");
                xAxisLabelRect.x += xAxisLabelWidthInterval;
                DrawTimeLabels(Styles.xAxisLabelStyle);
                gridPoint1.x = gridPoint2.x = gridPoint1.x + xAxisLabelWidthInterval;
                Handles.DrawLine(gridPoint1, gridPoint2);
                gridPoint1.y += _plotHeightOffset;
                gridPoint2.y += _plotHeightOffset;
                Handles.DrawLine(gridPoint1, gridPoint2);
                gridPoint1.y -= _plotHeightOffset;
                gridPoint2.y -= _plotHeightOffset;
            }
            xAxisLabelRect.x += xAxisLabelWidthInterval - (xAxisLabelSize.x / 2);
            timeLabel = _time.ToString("#0.###");
            DrawTimeLabels(Styles.yAxisLabelStyle);

            void DrawTimeLabels(GUIStyle style)
            {
                GUI.Label(xAxisLabelRect, timeLabel, style);
                xAxisLabelRect.y += _plotHeightOffset;
                GUI.Label(xAxisLabelRect, timeLabel, style);
                xAxisLabelRect.y -= _plotHeightOffset;
            }

            // Events
            foreach (var vibrationEvent in _events)
            {
                if (vibrationEvent is TransientEvent transientEvent)
                {
                    if (IsTimeInView(transientEvent.Time))
                    {
                        Handles.color = Colors.eventTransient;

                        Vector3 intensityPoint = PointToScrollCoords(transientEvent.Time, transientEvent.Intensity.Value);
                        Handles.DrawSolidDisc(intensityPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                        Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, intensityPoint, new Vector3(intensityPoint.x, _plotScreenSize.y));

                        Vector3 sharpnessPoint = PointToScrollCoords(transientEvent.Time, transientEvent.Sharpness.Value, _plotHeightOffset);
                        Handles.DrawSolidDisc(sharpnessPoint, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                        Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, sharpnessPoint, new Vector3(sharpnessPoint.x, _plotHeightOffset + _plotScreenSize.y));
                    }
                }
                else if (vibrationEvent is ContinuousEvent continuousEvent)
                {
                    Handles.color = Colors.eventContinuous;
                    DrawContinuousEvent(continuousEvent.IntensityCurve);
                    DrawContinuousEvent(continuousEvent.SharpnessCurve, _plotHeightOffset);

                    void DrawContinuousEvent(List<EventPoint> curve, float heightOffset = 0)
                    {
                        List<Vector3> points = new();
                        int pointCount = curve.Count;
                        int startIndex = 0, endIndex = pointCount - 1;
                        for (int i = 0; i < pointCount; i++)
                        {
                            if (IsTimeInView(curve[i].Time))
                            {
                                startIndex = Mathf.Max(i - 1, 0);
                                break;
                            }
                        }
                        for (int j = pointCount - 1; j >= 0; j--)
                        {
                            if (IsTimeInView(curve[j].Time))
                            {
                                endIndex = Mathf.Min(j + 1, pointCount - 1);
                                break;
                            }
                        }
                        if (startIndex == 0)
                            points.Add(PointToScrollCoords(curve[0].Time, 0, heightOffset));
                        for (int k = startIndex; k <= endIndex; k++)
                        {
                            EventPoint point = curve[k];
                            Vector3 pointScrollCoords = PointToScrollCoords(point.Time, point.Value, heightOffset);
                            points.Add(pointScrollCoords);
                            Handles.DrawSolidDisc(pointScrollCoords, POINT_NORMAL, PLOT_EVENT_POINT_SIZE);
                        }
                        if (endIndex == pointCount - 1)
                            points.Add(PointToScrollCoords(curve[pointCount - 1].Time, 0, heightOffset));

                        Handles.DrawAAPolyLine(PLOT_EVENT_LINE_WIDTH, points.ToArray());
                    }
                }
            }

            GUI.EndScrollView();

            if (_mouseLocation != MouseLocation.Outside)
            {
                // Hover helper lines
                Vector3 windowMousePositionProcessed = PointToWindowCoords(plotPosition, mousePlotRect);
                Handles.color = Colors.hoverGuides;
                Handles.DrawLine(new Vector3(windowMousePositionProcessed.x, otherPlotRect.y),
                    new Vector3(windowMousePositionProcessed.x, otherPlotRect.yMax));
                Handles.DrawLine(new Vector3(windowMousePositionProcessed.x, mousePlotRect.y),
                    new Vector3(windowMousePositionProcessed.x, mousePlotRect.yMax));
                Handles.DrawLine(new Vector3(mousePlotRect.x, windowMousePositionProcessed.y),
                    new Vector3(mousePlotRect.xMax, windowMousePositionProcessed.y));
                Handles.DrawSolidDisc(windowMousePositionProcessed, POINT_NORMAL, HOVER_DOT_SIZE);

                // Continuous event creation
                if (_draggedPoint == null && currentEvent.button == (int)MouseButton.Left && _previousMouseState == EventType.MouseDrag && _mouseLocation == _mouseClickLocation)
                {
                    Vector3 leftPoint = _mouseClickPosition;
                    Vector3 rightPoint = currentEvent.shift ? new Vector3(windowMousePositionProcessed.x, _mouseClickPosition.y) : windowMousePositionProcessed;
                    if (leftPoint.x > rightPoint.x)
                        (leftPoint, rightPoint) = (rightPoint, leftPoint);
                    Handles.color = Colors.eventContinuousCreation;
                    Handles.DrawAAConvexPolygon(leftPoint, rightPoint, new Vector3(rightPoint.x, mousePlotRect.yMax),
                        new Vector3(leftPoint.x, mousePlotRect.yMax), leftPoint);
                }
            }

            // Highlighted points
            EventPoint highlightedPoint = null;
            float highlightSize = 0f;
            Rect highlightRect = mousePlotRect;
            if (_draggedPoint != null)
            {
                Handles.color = Colors.draggedPoint;
                highlightedPoint = _draggedPoint;
                highlightSize = DRAG_HIGHLIGHT_SIZE;
                highlightRect = _mouseClickLocation == MouseLocation.IntensityPlot ? intensityPlotRect : sharpnessPlotRect;
            }
            else if (_hoverPoint != null && _mouseLocation != MouseLocation.Outside)
            {
                Handles.color = Colors.hoverPoint;
                highlightedPoint = _hoverPoint;
                highlightSize = HOVER_HIGHLIGHT_SIZE;
            }
            if (highlightedPoint != null)
            {
                Vector3 highlightedPointCoords = PointToWindowCoords(highlightedPoint, highlightRect);
                Handles.DrawSolidDisc(highlightedPointCoords, POINT_NORMAL, highlightSize);
            }

            if (_selectedPoints.Count > 0 && _selectedPoints[0] != null)
            {
                Handles.color = Colors.selectedPoint;
                highlightSize = SELECT_HIGHLIGHT_SIZE;
                highlightRect = _selectedPointLocation == MouseLocation.IntensityPlot ? intensityPlotRect : sharpnessPlotRect;
                for (int i = 0; i < _selectedPoints.Count; i++)
                {
                    Vector3 selectedPointCoords = PointToWindowCoords(_selectedPoints[i], highlightRect);
                    Handles.DrawSolidDisc(selectedPointCoords, POINT_NORMAL, highlightSize);
                }
            }

            // Plot borders
            Handles.color = Colors.plotBorder;
            DrawBorderForRect(intensityPlotRect);
            DrawBorderForRect(sharpnessPlotRect);
            
            void DrawBorderForRect(Rect rect)
            {
                Handles.DrawAAPolyLine(PLOT_BORDER_WIDTH,
                    rect.position, new Vector3(rect.xMax, rect.y), 
                    rect.max, new Vector3(rect.x, rect.yMax), rect.position);
            }

            #endregion

            #region Draw Side Panel

            if (_pointEditAreaVisible)
            {
                GUILayout.BeginArea(pointEditAreaRect, EditorStyles.helpBox);
                GUILayout.BeginVertical();

                // Selected point
                GUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("Selected point", EditorStyles.boldLabel);
                if (_selectedPoints.Count == 1 && _selectedPoints[0] != null)
                {
                    var point = _selectedPoints[0];
                    EditorGUI.BeginChangeCheck();
                    float newTime = Mathf.Clamp(EditorGUILayout.FloatField("Time", point.Time), _dragMin, _dragMax);
                    if (EditorGUI.EndChangeCheck())
                    {
                        GetEventPointOnPosition(point, _selectedPointLocation, out VibrationEvent vibrationEvent);
                        if (vibrationEvent is TransientEvent transientEvent)
                        {
                            transientEvent.Sharpness.Time = transientEvent.Intensity.Time = newTime;
                        }
                        else if (vibrationEvent is ContinuousEvent continuousEvent)
                        {
                            if (point.Time == continuousEvent.IntensityCurve.First().Time || point.Time == continuousEvent.SharpnessCurve.First().Time)
                                continuousEvent.IntensityCurve.First().Time = continuousEvent.SharpnessCurve.First().Time = newTime;
                            else if (point.Time == continuousEvent.IntensityCurve.Last().Time || point.Time == continuousEvent.SharpnessCurve.Last().Time)
                                continuousEvent.IntensityCurve.Last().Time = continuousEvent.SharpnessCurve.Last().Time = newTime;
                            else
                                point.Time = newTime;
                        }
                    }
                    string parameter = _selectedPointLocation == MouseLocation.IntensityPlot ? "Intensity" : "Sharpness";
                    point.Value = Mathf.Clamp01(EditorGUILayout.FloatField(parameter, point.Value));
                }
                else if (_selectedPoints.Count > 1)
                {
                    EditorGUILayout.LabelField("Multiple points", Styles.xAxisLabelStyle);
                    EditorGUILayout.LabelField("selected", Styles.xAxisLabelStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("No", Styles.xAxisLabelStyle);
                    EditorGUILayout.LabelField("selection", Styles.xAxisLabelStyle);
                }
                GUILayout.Space(3);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // Hover info
                GUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("Hover info", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Plot");
                GUILayout.Label(_mouseLocation == MouseLocation.Outside ? "-" : _mouseLocation.ToString(), Styles.yAxisLabelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Time");
                GUILayout.Label(_mouseLocation == MouseLocation.Outside ? "-" : plotPosition.x.ToString(), Styles.yAxisLabelStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Value");
                GUILayout.Label(_mouseLocation == MouseLocation.Outside ? "-" : plotPosition.y.ToString(), Styles.yAxisLabelStyle);
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            #endregion

            if (mouseOnWindow)
                Repaint();
        }

        #region Helper functions

        private bool IsTimeInView(float time)
        {
            float scrollTime = time / this._time * _plotScrollSize.x;
            return scrollTime >= _scrollPosition.x && scrollTime <= _scrollPosition.x + _plotScreenSize.x;
        }

        private Vector3 PointToScrollCoords(float time, float value, float heightOffset = 0)
        {
            return new Vector3(time / this._time * _plotScrollSize.x, 
                _plotScreenSize.y - value * _plotScreenSize.y + heightOffset);
        }

        private Vector3 PointToWindowCoords(EventPoint point, Rect plotRect)
        {
            return new Vector3(plotRect.x + point.Time / this._time * _plotScrollSize.x - _scrollPosition.x,
                plotRect.y + plotRect.height - point.Value * plotRect.height);
        }

        private bool TryGetContinuousEventOnTime(float time, out ContinuousEvent continuousEvent)
        {
            continuousEvent = null;
            foreach (var ev in _events)
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
            Vector2 pointOffset = new(HOVER_OFFSET * _time / _plotScrollSize.x, HOVER_OFFSET / _plotScreenSize.y);
            foreach (var ev in _events)
            {
                if (ev.IsOnPointInEvent(plotPosition, pointOffset, plot, out EventPoint eventPoint))
                {
                    vibrationEvent = ev;
                    return eventPoint;
                }
            }            
            return null;
        }

        private float GetFirstPointTime() => _events.Min(e => e.Time);
        
        private float GetLastPointTime()
        {
            float lastPointTime = 0, t;
            foreach (var ev in _events)
            {
                t = ev is TransientEvent ? ev.Time : ((ContinuousEvent)ev).IntensityCurve.Last().Time;
                lastPointTime = Mathf.Max(t, lastPointTime);
            }
            return lastPointTime;
        }

        private AHAPFile ConvertEventsToAHAPFile()
        {
            List<Pattern> patternList = new();
            foreach (var ev in _events)
                patternList.AddRange(ev.ToPatterns());
            patternList.Sort();
            return new AHAPFile(1, new Metadata(_projectName), patternList);
        }

        private void HandleSaving()
        {
            if (_events.Count == 0)
            {
                EditorUtility.DisplayDialog("No events", "Create some events to save it in file.", "OK");
                return;
            }

            var ahapFile = ConvertEventsToAHAPFile();
            string json = JsonConvert.SerializeObject(ahapFile, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (this._ahapFile != null && EditorUtility.DisplayDialog("Overwrite file?", "Do you want to overwrite selected file?",
                "Yes, overwrite", "No, create new"))
            {                
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, AssetDatabase.GetAssetPath(this._ahapFile)), json);
                EditorUtility.SetDirty(this._ahapFile);
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("Save AHAP JSON", "ahap", "json", "Enter file name");
            if (path.Length != 0)
            {
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, path), json);
                AssetDatabase.ImportAsset(path);
                this._ahapFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                EditorUtility.SetDirty(this._ahapFile);
            }
        }

        private void HandleImport()
        {
            if (_ahapFile != null)
            {
                AHAPFile ahap;
                try
                {
                    ahap = JsonConvert.DeserializeObject<AHAPFile>(_ahapFile.text);

                    _events ??= new List<VibrationEvent>();
                    _events.Clear();

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
                                _events.Add(new TransientEvent((float)e.Time, intensity, sharpness));
                            }
                            else if (e.EventType == AHAPFile.EVENT_CONTINUOUS)
                            {
                                List<EventPoint> intensityPoints = new();
                                float t = (float)e.Time;
                                Pattern curve = ahap.FindCurveOnTime(AHAPFile.CURVE_INTENSITY, t);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                        intensityPoints.Add(new EventPoint((float)point.Time, (float)point.ParameterValue));

                                    t = intensityPoints.Last().Time;
                                    curve = ahap.FindCurveOnTime(AHAPFile.CURVE_INTENSITY, t, curve);
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
                                curve = ahap.FindCurveOnTime(AHAPFile.CURVE_SHARPNESS, t);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                        sharpnessPoints.Add(new EventPoint((float)point.Time, (float)point.ParameterValue));

                                    t = sharpnessPoints.Last().Time;
                                    curve = ahap.FindCurveOnTime(AHAPFile.CURVE_SHARPNESS, t, curve);
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

                                ContinuousEvent ce = new()
                                {
                                    IntensityCurve = intensityPoints,
                                    SharpnessCurve = sharpnessPoints
                                };
                                _events.Add(ce);
                            }
                        }
                    }
                    _time = GetLastPointTime();
                    _projectName = ahap.Metadata.Project;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while importing file {_ahapFile.name}{Environment.NewLine}{ex.Message}");
                }
            }
        }

        #endregion
    }
}
