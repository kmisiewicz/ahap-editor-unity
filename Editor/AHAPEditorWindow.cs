using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow : EditorWindow
    {
        #region Fields

        // Data
        UnityEngine.Object _vibrationAsset;
        string _projectName = "";
        List<VibrationEvent> _events = new();

        // Drawing
        float _time, _zoom, _plotHeightOffset;
        Vector2 _plotScreenSize, _plotScrollSize, _scrollPosition;
        bool _pointEditAreaVisible;
        float _plotAreaWidthFactor, _plotAreaWidthFactorOffset;
        string[] _pointDragModes, _mouseModes;

        // Mouse handling
        UnityEngine.Event _currentEvent;
        EventType _previousMouseState = EventType.MouseUp;
        MouseLocation _mouseLocation, _mouseClickLocation, _selectedPointsLocation;
        Vector2 _mousePlotPosition, _mouseClickPosition, _mouseClickPlotPosition;
        PointDragMode _pointDragMode = PointDragMode.FreeMove;
        SnapMode _snapMode = SnapMode.None;
        MouseMode _mouseMode;
        EventPoint _hoverPoint, _draggedPoint;
        List<EventPoint> _selectedPoints;
        bool _selectingPoints, _pointEditAreaResize;
        float _dragMin, _dragMax, _dragValueMin, _dragValueMax, _dragMinBound, _dragMaxBound;
                
        // Audio waveform
        AudioClip _waveformClip;
        Texture2D _waveformTexture;
        bool _waveformVisible, _waveformNormalize, _waveformShouldRepaint;
        float _waveformLastPaintedZoom, _waveformRenderScale;

        // Debug
        bool _debugMode, _drawRects;

        #endregion

        [MenuItem("Window/AHAP Editor")]
        public static void OpenWindow()
        {
            AHAPEditorWindow window = GetWindow<AHAPEditorWindow>(Content.WINDOW_NAME, typeof(SceneView));
            var content = EditorGUIUtility.IconContent(Content.WINDOW_ICON_NAME);
            content.text = Content.WINDOW_NAME;
            window.titleContent = content;
        }
                
        private void OnEnable()
        {
            Clear();
            ResetState();
        }
        
        private void OnGUI()
		{
            _currentEvent = UnityEngine.Event.current;

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
            Rect bottomPartRect = new(CONTENT_MARGIN.x, CONTENT_MARGIN.y + topBarHeight + lineDoubleSpacing,
                topBarRect.width, bottomPartHeight);

            float plotAreaWidth = bottomPartRect.width * (_pointEditAreaVisible ? _plotAreaWidthFactor : 1);
            Rect plotAreaRect = new(bottomPartRect.position, new Vector2(plotAreaWidth, bottomPartRect.height));

            float pointEditAreaWidth = Mathf.Max(bottomPartRect.width - plotAreaWidth - lineDoubleSpacing, 0);
            Rect pointEditAreaRect = new(bottomPartRect.x + plotAreaWidth + lineDoubleSpacing,
                bottomPartRect.y, pointEditAreaWidth, bottomPartHeight);

            Rect resizeBarRect = new(plotAreaRect.xMax, plotAreaRect.y + PLOT_BORDER_WIDTH,
                lineDoubleSpacing * 2f, plotAreaRect.height - 2 * PLOT_BORDER_WIDTH);

            float singlePlotAreaHeight = (plotAreaRect.height - lineHeight) * 0.5f - lineSpacing;
            Rect intensityPlotAreaRect = new(plotAreaRect.position, new Vector2(plotAreaWidth, singlePlotAreaHeight));
            Rect sharpnessPlotAreaRect = new(intensityPlotAreaRect);
            sharpnessPlotAreaRect.y += intensityPlotAreaRect.height + lineDoubleSpacing;

            Vector2 yAxisLabelSize = EditorStyles.label.CalcSize(Content.yAxisDummyLabel);
            yAxisLabelSize.x += CUSTOM_LABEL_WIDTH_OFFSET;
            yAxisLabelSize.y *= 1.5f;
            Vector2 xAxisLabelSize = EditorStyles.label.CalcSize(Content.xAxisDummyLabel);
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

            #region Zoom and scroll (mouse wheel)

            if (_currentEvent.type == EventType.ScrollWheel && plotAreaRect.Contains(_currentEvent.mousePosition))
            {
                if (_currentEvent.control)
                {
                    float plotMouseX = _currentEvent.mousePosition.x - intensityPlotRect.x;
                    float xOld = (_scrollPosition.x + plotMouseX) / _plotScrollSize.x;
                    _zoom -= Mathf.Sign(_currentEvent.delta.y) * ZOOM_INCREMENT;
                    _zoom = Mathf.Clamp(_zoom, 1, MAX_ZOOM);
                    _plotScrollSize = new Vector2(scrollRect.width * _zoom, intensityPlotRect.height);
                    float xNew = (_scrollPosition.x + plotMouseX) / _plotScrollSize.x;
                    float xDiff = (xOld - xNew) * _plotScrollSize.x;
                    _scrollPosition.x += xDiff;
                    _scrollPosition.x = Mathf.Clamp(_scrollPosition.x, 0, _plotScrollSize.x - _plotScreenSize.x);
                }
                else if (_zoom > 1f)
                {
                    _scrollPosition.x += _plotScrollSize.x * Mathf.Sign(_currentEvent.delta.y) * SCROLL_INCREMENT;
                    _scrollPosition.x = Mathf.Clamp(_scrollPosition.x, 0, _plotScrollSize.x - _plotScreenSize.x);
                }
                _currentEvent.Use();
            }

            #endregion

            Rect scrollPlotRect = new(Vector2.zero, _plotScrollSize);
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
                EditorGUI.DrawRect(new Rect(intensityPlotRect.position + xAxisLabelRect.position,
                    xAxisLabelRect.size), Color.white);
                EditorGUI.DrawRect(resizeBarRect, Color.white);
            }

            #endregion

            #region Mouse clicks handling

            // Mouse location
            _mousePlotPosition = Vector2.positiveInfinity; // Mouse position in plot space with snapping
            Rect mousePlotRect = intensityPlotRect; // Plot rect mouse is on
            Rect otherPlotRect = sharpnessPlotRect; // The other plot rect
            _mouseLocation = MouseLocation.Outside;
            if (mouseOverWindow == this)
            {
                if (intensityPlotRect.Contains(_currentEvent.mousePosition))
                {
                    _mouseLocation = MouseLocation.IntensityPlot;
                }
                else if (sharpnessPlotRect.Contains(_currentEvent.mousePosition))
                {
                    _mouseLocation = MouseLocation.SharpnessPlot;
                    mousePlotRect = sharpnessPlotRect;
                    otherPlotRect = intensityPlotRect;
                }
            }
            // Process mouse position if over plot
            if (_mouseLocation != MouseLocation.Outside)
            {
                Vector2 plotRectMousePosition = _currentEvent.mousePosition - mousePlotRect.position; // Position inside plot rect mouse is over
                _mousePlotPosition.x = (_scrollPosition.x + plotRectMousePosition.x) / _plotScrollSize.x * _time;
                _mousePlotPosition.y = (_plotScreenSize.y - plotRectMousePosition.y) / _plotScreenSize.y;
                if (_snapMode != SnapMode.None)
                {
                    _mousePlotPosition.x = (float)Math.Round(_mousePlotPosition.x, (int)_snapMode);
                    _mousePlotPosition.y = (float)Math.Round(_mousePlotPosition.y, (int)_snapMode);
                }
            }

            MouseMode actualMouseMode = _mouseMode;
            if (_pointEditAreaResize) 
                actualMouseMode = MouseMode.None;
            else if (actualMouseMode == MouseMode.AddRemove && (_selectingPoints || 
                _previousMouseState != EventType.MouseDrag && _currentEvent.control))
                actualMouseMode = MouseMode.Select;
            else if (actualMouseMode == MouseMode.AddRemove && _selectingPoints)
                actualMouseMode = MouseMode.Select;

            _hoverPoint = null;
            if (actualMouseMode == MouseMode.AddRemove)
            {
                if (_mouseLocation != MouseLocation.Outside)
                {
                    if (_currentEvent.button == (int)MouseButton.Left) // LMB event
                    {
                        if (_draggedPoint == null)
                        {
                            if (_previousMouseState == EventType.MouseUp)
                                _hoverPoint = GetPointOnPosition(_mousePlotPosition, _mouseLocation);
                            if (_hoverPoint == null)
                            {
                                if (_previousMouseState == EventType.MouseUp && _currentEvent.type == EventType.MouseDown) // LMB down
                                    HandleNonHoverClick();
                                else if (_previousMouseState == EventType.MouseDown && _currentEvent.type == EventType.MouseDrag)
                                    _previousMouseState = TryGetContinuousEvent(_mousePlotPosition.x, out _) ? EventType.MouseUp : EventType.MouseDrag;
                                else if (_currentEvent.type == EventType.MouseUp && _mouseClickLocation == _mouseLocation) // LMB up
                                    HandleAddFinish();
                            }
                            else if (_currentEvent.type == EventType.MouseDown && _previousMouseState == EventType.MouseUp)
                            {
                                if (!_selectedPoints.Contains(_hoverPoint))
                                {
                                    _selectedPoints.Clear();
                                    _selectedPoints.Add(_hoverPoint);
                                    _selectedPointsLocation = _mouseLocation;
                                }
                                HandleDragStart();
                            }
                        }
                        else if (_currentEvent.type == EventType.MouseDrag && _mouseLocation == _mouseClickLocation)
                        {
                            _hoverPoint = _draggedPoint;
                            HandleDrag();
                        }
                    }
                    else if (_currentEvent.type == EventType.MouseUp)
                    {
                        _hoverPoint = GetPointOnPosition(_mousePlotPosition, _mouseLocation);
                        if (_hoverPoint != null)
                            HandlePointRemoval();
                    }
                }
                if (_currentEvent.button == (int)MouseButton.Left && _currentEvent.type == EventType.MouseUp)
                {
                    if (_draggedPoint != null)
                    {
                        ContinuousEvent lastSortedEvent = null;
                        foreach (var point in _selectedPoints)
                        {
                            if (point.ParentEvent is ContinuousEvent ce && ce != lastSortedEvent)
                            {
                                lastSortedEvent = ce;
                                lastSortedEvent.IntensityCurve.Sort();
                                lastSortedEvent.SharpnessCurve.Sort();
                            }
                        }
                    }
                    _draggedPoint = null;
                    _previousMouseState = EventType.MouseUp;
                    if (_mouseLocation == MouseLocation.Outside)
                        _hoverPoint = null;
                }
            }
            else if (actualMouseMode == MouseMode.Select)
            {
                if (_mouseLocation != MouseLocation.Outside)
                {
                    if (_previousMouseState == EventType.MouseUp)
                        _hoverPoint = GetPointOnPosition(_mousePlotPosition, _mouseLocation);
                    if (_currentEvent.button == (int)MouseButton.Left)
                    {
                        if (_previousMouseState == EventType.MouseUp && _currentEvent.type == EventType.MouseDown)
                        {
                            HandleNonHoverClick();
                            _selectingPoints = true;
                        }
                        else if (_previousMouseState == EventType.MouseDown && _currentEvent.type == EventType.MouseDrag)
                            _previousMouseState = EventType.MouseDrag;
                        else if (_currentEvent.type == EventType.MouseUp)
                        {
                            _selectingPoints = false;
                            _previousMouseState = EventType.MouseUp;
                            if (_mouseClickLocation == _mouseLocation)
                                HandleSelection();
                        }
                    }
                    else if (_hoverPoint != null && _currentEvent.button == (int)MouseButton.Middle && _currentEvent.type == EventType.MouseUp)
                    {
                        SelectHoverEvent();
                    }
                }
            }
            
            // Handle point edit area resizing
            if (_pointEditAreaVisible)
            {
                if (!_pointEditAreaResize && resizeBarRect.Contains(_currentEvent.mousePosition))
                {
                    EditorGUIUtility.AddCursorRect(resizeBarRect, MouseCursor.ResizeHorizontal);
                    if (_currentEvent.button == (int)MouseButton.Left && _currentEvent.type == EventType.MouseDown)
                    {
                        _pointEditAreaResize = true;
                        _plotAreaWidthFactorOffset = _plotAreaWidthFactor * bottomPartRect.width - _currentEvent.mousePosition.x;
                        _previousMouseState = EventType.MouseDown;
                    }
                }

                if (_pointEditAreaResize)
                {
                    EditorGUIUtility.AddCursorRect(new Rect(Vector2.zero, position.size), MouseCursor.ResizeHorizontal);
                    _plotAreaWidthFactor = Mathf.Clamp((_currentEvent.mousePosition.x + _plotAreaWidthFactorOffset) / bottomPartRect.width,
                        PLOT_AREA_MIN_WIDTH, PLOT_AREA_MAX_WIDTH);
                    if (_currentEvent.button == (int)MouseButton.Left && _currentEvent.type == EventType.MouseUp)
                    {
                        _pointEditAreaResize = false;
                        _previousMouseState = EventType.MouseUp;
                    }
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
                {
                    EditorGUILayout.LabelField(Content.debugLabel, EditorStyles.boldLabel, topBarContainerThirdOption);

                    if (GUILayout.Button(Content.logRectsLabel, topBarContainerThirdOption))
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
                        sb.AppendLine($"X axis label rect: {new Rect(intensityPlotRect.position + xAxisLabelRect.position, xAxisLabelRect.size)} (white)");
                        sb.AppendLine($"Resize bar: {resizeBarRect} (white)");
                        Debug.Log(sb.ToString());
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Content.drawRectsLabel);
                    _drawRects = GUILayout.Toggle(_drawRects, GUIContent.none);
                    GUILayout.EndHorizontal();

                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndVertical();
            }

            // File
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField(Content.fileLabel, EditorStyles.boldLabel);

                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.assetLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _vibrationAsset = EditorGUILayout.ObjectField(Content.assetLabel, _vibrationAsset, typeof(UnityEngine.Object), false);
                EditorGUIUtility.labelWidth = 0;

                GUILayout.BeginHorizontal();
                GUI.enabled = _vibrationAsset != null;
                if (GUILayout.Button(Content.importLabel)) HandleImport();
                GUI.enabled = true;
                if (GUILayout.Button(Content.saveLabel)) HandleSaving();
                GUILayout.EndHorizontal();

                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.projectNameLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _projectName = EditorGUILayout.TextField(Content.projectNameLabel, _projectName);
            }
            GUILayout.EndVertical();

            // Audio waveform
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField(Content.waveformSectionLabel, EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _waveformClip = EditorGUILayout.ObjectField(GUIContent.none, _waveformClip, typeof(AudioClip), false) as AudioClip;
                if (EditorGUI.EndChangeCheck())
                {
                    AudioClipUtils.StopAllClips();
                    _waveformVisible = false;
                    if (_waveformClip != null) 
                    {
                        if (_waveformClip.length > MAX_TIME)
                        {
                            _waveformClip = null;
                            EditorUtility.DisplayDialog("Clip too long", $"Selected audio clip is longer than max allowed {MAX_TIME}s.", "OK");
                        }
                    }
                }
                GUI.enabled = _waveformClip != null;
                if (GUILayout.Button(EditorGUIUtility.IconContent(Content.PLAY_ICON_NAME), EditorStyles.miniButton, GUILayout.MaxWidth(30)))
                    AudioClipUtils.PlayClip(_waveformClip);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _waveformVisible = GUILayout.Toggle(_waveformVisible, Content.waveformVisibleLabel, GUI.skin.button, topBarContainerHalfOption);
                _waveformNormalize = GUILayout.Toggle(_waveformNormalize, Content.normalizeLabel, GUI.skin.button, topBarContainerHalfOption);
                if (EditorGUI.EndChangeCheck())
                {
                    if (_waveformVisible) _waveformShouldRepaint = true;
                    else _waveformLastPaintedZoom = 0;
                }                    
                GUILayout.EndHorizontal();

                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.renderScaleLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _waveformRenderScale = Mathf.Clamp(EditorGUILayout.FloatField(Content.renderScaleLabel, _waveformRenderScale),
                    MIN_WAVEFORM_RENDER_SCALE, MAX_WAVEFORM_RENDER_SCALE);
                GUI.enabled = true;
            }
            GUILayout.EndVertical();

            // Plot view
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField(Content.plotViewLabel, EditorStyles.boldLabel);
                GUILayout.Space(-1);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Content.zoomResetLabel, topBarContainerThirdOption)) _zoom = 1;
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.zoomLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _zoom = (float)Math.Round(EditorGUILayout.Slider(Content.zoomLabel, _zoom, 1, MAX_ZOOM), 1);
                if (Mathf.Abs(_zoom - _waveformLastPaintedZoom) > 0.5f) _waveformShouldRepaint = true;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Content.trimButtonLabel, topBarContainerThirdOption))
                    _time = _waveformClip != null ? _waveformClip.length : GetLastPointTime();
                EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.timeLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                _time = Mathf.Clamp(Mathf.Max(EditorGUILayout.FloatField(Content.timeLabel, _time), GetLastPointTime()), MIN_TIME, MAX_TIME);
                if (_waveformClip != null) _time = Mathf.Max(_time, _waveformClip.length);
                EditorGUIUtility.labelWidth = 0;
                GUILayout.EndHorizontal();

                if (GUILayout.Button(Content.clearLabel)) Clear();

                GUILayout.Space(-1);
            }
            GUILayout.EndVertical();

            // Point editing
            GUILayout.BeginVertical(GUI.skin.box, topBarMaxWidthOption);
            {
                EditorGUILayout.LabelField(Content.pointEditingLabel, EditorStyles.boldLabel);
                GUILayout.Space(-1);

                _mouseMode = (MouseMode)(GUILayout.Toolbar((int)_mouseMode, _mouseModes[..^1]));

                _pointEditAreaVisible = GUILayout.Toggle(_pointEditAreaVisible, Content.advancedPanelLabel, GUI.skin.button, GUILayout.ExpandHeight(true));
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
            GUILayout.Label(Content.intensityLabel, Styles.plotTitleStyle);
            GUILayout.EndArea();
            GUILayout.BeginArea(sharpnessPlotAreaRect);
            GUILayout.Space(lineSpacing);
            GUILayout.Label(Content.sharpnessLabel, Styles.plotTitleStyle);
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
            if (_waveformClip != null && _waveformVisible)
            {
                if (_waveformShouldRepaint)
                {
                    _waveformTexture = AudioClipUtils.PaintAudioWaveform(_waveformClip, (int)(_plotScrollSize.x * _waveformRenderScale),
                        (int)(_plotScrollSize.y * _waveformRenderScale), Colors.waveformBg, Colors.waveform, _waveformNormalize);
                    _waveformLastPaintedZoom = _zoom;
                    _waveformShouldRepaint = false;
                }
                Rect audioTextureRect = new(scrollPlotRect);
                audioTextureRect.width *= _waveformClip.length / _time;
                GUI.DrawTexture(audioTextureRect, _waveformTexture, ScaleMode.StretchToFill);
                audioTextureRect.y += _plotHeightOffset;
                GUI.DrawTexture(audioTextureRect, _waveformTexture, ScaleMode.StretchToFill);
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
                Vector3 windowMousePositionProcessed = PointToWindowCoords(_mousePlotPosition, mousePlotRect);
                Handles.color = Colors.hoverGuides;
                Handles.DrawLine(new Vector3(windowMousePositionProcessed.x, otherPlotRect.y),
                    new Vector3(windowMousePositionProcessed.x, otherPlotRect.yMax));
                Handles.DrawLine(new Vector3(windowMousePositionProcessed.x, mousePlotRect.y),
                    new Vector3(windowMousePositionProcessed.x, mousePlotRect.yMax));
                Handles.DrawLine(new Vector3(mousePlotRect.x, windowMousePositionProcessed.y),
                    new Vector3(mousePlotRect.xMax, windowMousePositionProcessed.y));
                Handles.DrawSolidDisc(windowMousePositionProcessed, POINT_NORMAL, HOVER_DOT_SIZE);

                // Drag - continuous event creation or selecting multiple points
                if (_draggedPoint == null && _currentEvent.button == (int)MouseButton.Left && _previousMouseState == EventType.MouseDrag && _mouseLocation == _mouseClickLocation)
                {
                    if (_selectingPoints)
                    {
                        Rect selectionRect = new(Mathf.Min(_mouseClickPosition.x, windowMousePositionProcessed.x),
                            Mathf.Min(_mouseClickPosition.y, windowMousePositionProcessed.y),
                            Mathf.Abs(_mouseClickPosition.x - windowMousePositionProcessed.x),
                            Mathf.Abs(_mouseClickPosition.y - windowMousePositionProcessed.y));
                        EditorUtils.DrawRectWithBorder(selectionRect, lineDoubleSpacing, Colors.selectedPoint, Colors.selectionRectBorder);
                    }
                    else
                    {
                        Vector3 leftPoint = _mouseClickPosition;
                        Vector3 rightPoint = _currentEvent.shift ? new Vector3(windowMousePositionProcessed.x, _mouseClickPosition.y) : windowMousePositionProcessed;
                        if (leftPoint.x > rightPoint.x)
                            (leftPoint, rightPoint) = (rightPoint, leftPoint);
                        Handles.color = Colors.eventContinuousCreation;
                        Handles.DrawAAConvexPolygon(leftPoint, rightPoint, new Vector3(rightPoint.x, mousePlotRect.yMax),
                            new Vector3(leftPoint.x, mousePlotRect.yMax), leftPoint);
                    }
                }
            }

            // Highlighted points
            if (_draggedPoint != null)
            {
                Handles.color = Colors.draggedPoint;
                Rect dragRect = _mouseClickLocation == MouseLocation.IntensityPlot ? intensityPlotRect : sharpnessPlotRect;
                Handles.DrawSolidDisc(PointToWindowCoords(_draggedPoint, dragRect), POINT_NORMAL, DRAG_HIGHLIGHT_SIZE);
                Handles.color = Colors.dragBounds;
                Vector3 minBound = PointToWindowCoords(new Vector2(_dragMinBound, 1), intensityPlotRect);
                Vector3 minBound2 = PointToWindowCoords(new Vector2(_dragMinBound, 0), intensityPlotRect);
                Vector3 maxBound = PointToWindowCoords(new Vector2(_dragMaxBound, 1), intensityPlotRect);
                Vector3 maxBound2 = PointToWindowCoords(new Vector2(_dragMaxBound, 0), intensityPlotRect);
                Handles.DrawDottedLine(minBound, minBound2, 1);
                Handles.DrawDottedLine(maxBound, maxBound2, 1);
                minBound.y += _plotHeightOffset;
                minBound2.y += _plotHeightOffset;
                maxBound.y += _plotHeightOffset;
                maxBound2.y += _plotHeightOffset;
                Handles.DrawDottedLine(minBound, minBound2, 1);
                Handles.DrawDottedLine(maxBound, maxBound2, 1);
            }
            else if (_hoverPoint != null && _mouseLocation != MouseLocation.Outside)
            {
                Handles.color = Colors.hoverPoint;
                Handles.DrawSolidDisc(PointToWindowCoords(_hoverPoint, mousePlotRect), POINT_NORMAL, HOVER_HIGHLIGHT_SIZE);
            }

            if (_selectedPoints.Count > 0)
            {
                Handles.color = Colors.selectedPoint;
                Rect selectRect = _selectedPointsLocation == MouseLocation.IntensityPlot ? intensityPlotRect : sharpnessPlotRect;
                for (int i = 0; i < _selectedPoints.Count; i++)
                {
                    Vector3 selectedPointCoords = PointToWindowCoords(_selectedPoints[i], selectRect);
                    Handles.DrawSolidDisc(selectedPointCoords, POINT_NORMAL, SELECT_HIGHLIGHT_SIZE);
                }
            }

            // Plot borders
            EditorUtils.DrawRectBorder(intensityPlotRect, PLOT_BORDER_WIDTH, Colors.plotBorder);
            EditorUtils.DrawRectBorder(sharpnessPlotRect, PLOT_BORDER_WIDTH, Colors.plotBorder);

            #endregion

            #region Draw Side Panel

            if (_pointEditAreaVisible)
            {
                GUILayout.BeginArea(pointEditAreaRect, EditorStyles.helpBox);
                GUILayout.BeginVertical();

                // Selection
                GUILayout.BeginVertical(GUI.skin.box);
                {
                    EditorGUILayout.LabelField(Content.selectionLabel, EditorStyles.boldLabel);
                    if (_selectedPoints.Count == 1 && _selectedPoints[0] != null)
                    {
                        var point = _selectedPoints[0];
                        EditorGUI.BeginChangeCheck();
                        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(Content.sharpnessLabel).x + CUSTOM_LABEL_WIDTH_OFFSET;
                        float newTime = Mathf.Clamp(EditorGUILayout.FloatField(Content.timeLabel, point.Time), _dragMin, _dragMax);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (point.ParentEvent is TransientEvent transientEvent)
                            {
                                transientEvent.Sharpness.Time = transientEvent.Intensity.Time = newTime;
                            }
                            else if (point.ParentEvent is ContinuousEvent continuousEvent)
                            {
                                if (point.Time == continuousEvent.IntensityCurve.First().Time || point.Time == continuousEvent.SharpnessCurve.First().Time)
                                    continuousEvent.IntensityCurve.First().Time = continuousEvent.SharpnessCurve.First().Time = newTime;
                                else if (point.Time == continuousEvent.IntensityCurve.Last().Time || point.Time == continuousEvent.SharpnessCurve.Last().Time)
                                    continuousEvent.IntensityCurve.Last().Time = continuousEvent.SharpnessCurve.Last().Time = newTime;
                                else
                                    point.Time = newTime;
                            }
                        }
                        point.Value = Mathf.Clamp01(EditorGUILayout.FloatField(
                            _selectedPointsLocation == MouseLocation.IntensityPlot ? Content.intensityLabel : Content.sharpnessLabel,
                            point.Value));
                        EditorGUIUtility.labelWidth = 0;
                    }
                    else
                    {
                        GUILayout.Space(6);
                        if (_selectedPoints.Count > 1) GUILayout.Label("Multiple points\nselected", Styles.xAxisLabelStyle);
                        else GUILayout.Label("No\nselection", Styles.xAxisLabelStyle);
                    }
                    GUILayout.Space(3);
                }
                GUILayout.EndVertical();

                // Point drag mode
                GUILayout.BeginVertical(GUI.skin.box);
                {
                    EditorGUILayout.LabelField(Content.pointDragLabel, EditorStyles.boldLabel);
                    _pointDragMode = (PointDragMode)GUILayout.Toolbar((int)_pointDragMode, _pointDragModes);
                    GUILayout.Space(3);
                }
                GUILayout.EndVertical();
                
                // Snapping
                GUILayout.BeginVertical(GUI.skin.box);
                {
                    EditorGUILayout.LabelField(Content.snappingLabel, EditorStyles.boldLabel);
                    _snapMode = (SnapMode)EditorGUILayout.EnumPopup(GUIContent.none, _snapMode);
                    GUILayout.Space(3);
                }
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // Hover info
                GUILayout.BeginVertical(GUI.skin.box);
                {
                    EditorGUILayout.LabelField(Content.hoverInfoLabel, EditorStyles.boldLabel);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Content.plotLabel);
                    GUILayout.Label(_mouseLocation == MouseLocation.Outside ? new GUIContent("-") :
                        (_mouseLocation == MouseLocation.IntensityPlot ? Content.intensityLabel : Content.sharpnessLabel),
                        Styles.yAxisLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Content.timeLabel);
                    GUILayout.Label(_mouseLocation == MouseLocation.Outside ? "-" : _mousePlotPosition.x.ToString(), Styles.yAxisLabelStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Content.valueLabel);
                    GUILayout.Label(_mouseLocation == MouseLocation.Outside ? "-" : _mousePlotPosition.y.ToString(), Styles.yAxisLabelStyle);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            #endregion

            if (mouseOverWindow == this)
                Repaint();
        }
    }
}
