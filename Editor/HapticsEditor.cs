using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using PopupWindow = UnityEditor.PopupWindow;
using Object = UnityEngine.Object;
using Chroma.UIToolkit.Utility;

//TODO: remove AHAPEditor from name and namespace
//TODO: fold queries with 1 change to one line
namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class HapticsEditor : EditorWindow
    {
        internal class USS
        {
            public const string RadioButtonStripButtonLeft = "radio-button-strip-button-left";
            public const string RadioButtonStripButtonMiddle = "radio-button-strip-button-middle";
            public const string RadioButtonStripButtonRight = "radio-button-strip-button-right";
        }

        internal class UnityUSS
        {
            public const string RadioButtonGroupContainer = "unity-radio-button-group__container";
        }

        internal class Controls
        {
            public const string ImportButton = "importButton";
            public const string SaveButton = "saveButton";

            public const string WaveformField = "waveformField";
            public const string WaveformVisibility = "waveVisibility";
            public const string PlayButton = "playButton";
            public const string StopButton = "stopButton";
            public const string WaveformVisible = "waveVisibleToggle";
            public const string WaveformNormalize = "waveNormalizeToggle";
            public const string WaveformRenderScale = "waveRenderScale";
            public const string WaveformHeightMultiplier = "waveHeightMultiplier";

            public const string ZoomField = "zoom";
            public const string ZoomResetButton = "zoomResetButton";
            public const string TimeField = "timeValue";
            public const string TrimButton = "trimButton";

            public const string MouseModeRadioGroup = "mouseModeRadioGroup";
            public const string ClearButton = "clearButton";

            public const string PointDragRadioGroup = "pointDragRadioGroup";
            public const string SnapModeEnumField = "snappingEnum";
            
            public const string AudioAnalysisPanel = "audioAnalysisPanel";

            public const string BottomPartSplitView = "bottomPart";
            public const string PlotScroll = "plotScroll";
            public const string AmplitudePlot = "amplitudePlot";
            public const string AmplitudePlotWaveform = "amplitudePlotWaveform";
            public const string AmplitudePlotPoints = "amplitudePlotPoints";
            public const string AmplitudePlotYAxisLabels = "amplitudePlotYLabels";
            public const string AmplitudePlotXAxisLabels = "amplitudePlotXAxisLabels";
            public const string FrequencyPlot = "frequencyPlot";
            public const string FrequencyPlotWaveform = "frequencyPlotWaveform";
            public const string FrequencyPlotPoints = "frequencyPlotPoints";
            public const string FrequencyPlotYAxisLabels = "frequencyPlotYLabels";
            public const string FrequencyPlotXAxisLabels = "frequencyPlotXAxisLabels";
            
            public const string HoverPlotName = "hoverPlotName";
            public const string HoverTime = "hoverTimeNumber";
            public const string HoverValue = "hoverValueNumber";
        }

        internal class Settings
        {
            public const string UxmlPath = "Packages/com.chroma.ahapeditor/Editor/HapticsEditor.uxml";
            public const float ZoomDelta = 0.1f;
            public const float ZoomMax = 7f;
            public const float ScrollDelta = 0.05f;
            public const float WaveformRepaintZoomThreshold = 0.5f;
            public const float WaveformRepaintTimeThreshold = 0.2f;
            public const float WaveformRenderScaleMin = 0.1f;
            public const float WaveformRenderScaleMax = 2f;
            public const float WaveformHeightMultiplierMin = 0.1f;
            public const float WaveformHeightMultiplierMax = 3f;
            public const float TimeMin = 0.1f;
            public const float TimeMax = 30f;
            public const float TimeDefault = 1f;
            public const float EventLineWidth = 3f;
            public const float EventPointRadius = 5f;
            public const float GridLineWidth = 1f;
            public const float HoverGuideLineWidth = 1f;
            public const float HoverGuideCircleRadius = 4f;
            public const int MinBottomPanelWidth = 200;
            public const float HoverOffset = 5f;
            public const float HoverHighlightSize = 9f;
        }

        //TODO: move to stylesheet?
        internal class Colors
        {
            public static readonly Color plotBorder = Color.white;
            public static readonly Color waveform = new(0.42f, 1f, 0f, 0.2f);
            public static readonly Color waveformBg = Color.clear;
            public static readonly Color eventTransient = new(0.22f, 0.6f, 1f);
            public static readonly Color eventContinuous = new(1f, 0.6f, 0.2f);
            public static readonly Color eventContinuousCreation = new(1f, 0.6f, 0.2f, 0.5f);
            public static readonly Color hovered = new(0.8f, 0.8f, 0.8f, 0.2f);
            public static readonly Color dragged = new(1f, 1f, 0f, 0.3f);
            public static readonly Color dragBounds = new(0.85f, 1f, 0f);
            public static readonly Color selectedPoint = new(0.5f, 1f, 1f, 0.3f);
            public static readonly Color hoverGuides = new(0.7f, 0f, 0f);
            public static readonly Color selectionRectBorder = new(0, 0.9f, 0.9f);
            public static readonly Color invalid = new(0.5f, 0.5f, 0.5f, 0.2f);
        }

        internal enum MouseState { Unclicked = 0, Clicked = 1, Dragging = 2 }

        [SerializeField] List<HapticEvent> _Events;
        [SerializeField] AudioClip _WaveformClip;
        [SerializeField] bool _WaveformVisible;
        [SerializeField] bool _WaveformNormalize;
        [SerializeField] float _WaveformRenderScale;
        [SerializeField] float _WaveformHeightMultiplier;
        [SerializeField] float _Time;

        Texture2D _waveformTexture;
        float _zoom, _lastPaintedZoom;
        float _scrollOffset;
        int _firstRepaintCounter;
        Vector2? _amplitudeMousePosition, _frequencyMousePosition;
        MouseState _mouseState;
        Vector2? _dragStartPosition;
        EventPoint _hoverPoint;

        [MenuItem("Window/Haptics Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<HapticsEditor>("Haptics Editor", typeof(SceneView));
            var content = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ?
                "d_HoloLensInputModule Icon" : "HoloLensInputModule Icon");
            content.text = "Haptics Editor";
            window.titleContent = content;
        }

        void Initialize()
        {
            _Events ??= new List<HapticEvent>();
            _zoom = _lastPaintedZoom = 1;
            _scrollOffset = 0;
            float lastPointTime = GetLastPointTime();
            _Time = lastPointTime > 0 ? lastPointTime : Settings.TimeDefault;
            if (_WaveformClip != null)
                _Time = Mathf.Max(_Time, _WaveformClip.length);
            else
                _WaveformVisible = _WaveformNormalize = false;
            _Time = Mathf.Clamp(_Time, Settings.TimeMin, Settings.TimeMax);
            if (_WaveformRenderScale < Settings.WaveformRenderScaleMin || 
                _WaveformRenderScale > Settings.WaveformRenderScaleMax)
                _WaveformRenderScale = 1;
            if (_WaveformHeightMultiplier < Settings.WaveformHeightMultiplierMin || 
                _WaveformHeightMultiplier > Settings.WaveformHeightMultiplierMax)
                _WaveformHeightMultiplier = 1;
            _amplitudeMousePosition = _frequencyMousePosition = null;
            _dragStartPosition = null;
            _mouseState = MouseState.Unclicked;
            _hoverPoint = null;
        }

        void Clear()
        {
            _Events ??= new List<HapticEvent>();
            _Events.Clear();

            rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints).MarkDirtyRepaint();
            rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotPoints).MarkDirtyRepaint();
        }

        //TODO: Separate into multiple methods
        //TODO: Cache some frequently used controls
        public void CreateGUI()
        {
            Initialize();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Settings.UxmlPath);
            VisualElement windowUxml = visualTree.Instantiate();
            windowUxml.style.flexGrow = 1; // Fix for TemplateContainer not expanding
            rootVisualElement.Add(windowUxml);
            _firstRepaintCounter = 0;

            // Save button
            var saveButton = rootVisualElement.Q<Button>(Controls.SaveButton);
            saveButton.clicked += () => PopupWindow.Show(saveButton.worldBound, new SavePopup());

            // Waveform panel
            var waveformField = rootVisualElement.Q<ObjectField>(Controls.WaveformField);
            waveformField.RegisterValueChangedCallback(OnWaveformAssetChanged);
            waveformField.value = _WaveformClip;
            var playButton = rootVisualElement.Q<Button>(Controls.PlayButton);
            playButton.clicked += () => AudioClipUtils.PlayClip(_WaveformClip);
            var stopButton = rootVisualElement.Q<Button>(Controls.StopButton);
            stopButton.clicked += AudioClipUtils.StopAllClips;
            var waveformVisibleToggle = rootVisualElement.Q<Toggle>(Controls.WaveformVisible);
            waveformVisibleToggle.SetValueWithoutNotify(_WaveformVisible);
            waveformVisibleToggle.RegisterValueChangedCallback(ToggleWaveformVisible);
            var waveformNormalizeToggle = rootVisualElement.Q<Toggle>(Controls.WaveformNormalize);
            waveformNormalizeToggle.SetValueWithoutNotify(_WaveformNormalize);
            waveformNormalizeToggle.RegisterValueChangedCallback(ToggleWaveformNormalize);
            var waveformRenderScaleField = rootVisualElement.Q<FloatField>(Controls.WaveformRenderScale);
            waveformRenderScaleField.SetValueWithoutNotify(_WaveformRenderScale);
            waveformRenderScaleField.RegisterValueChangedCallback(OnWaveformRenderScaleChanged);
            var waveformHeightMultiplierField = rootVisualElement.Q<Slider>(Controls.WaveformHeightMultiplier);
            waveformHeightMultiplierField.SetValueWithoutNotify(_WaveformHeightMultiplier);
            waveformHeightMultiplierField.RegisterValueChangedCallback(OnWaveformHeightMultiplierChanged);
            var amplitudePlotWaveform = rootVisualElement.Q<Image>(Controls.AmplitudePlotWaveform);
            var frequencyPlotWaveform = rootVisualElement.Q<Image>(Controls.FrequencyPlotWaveform);
            amplitudePlotWaveform.scaleMode = frequencyPlotWaveform.scaleMode = ScaleMode.StretchToFill;
            amplitudePlotWaveform.tintColor = frequencyPlotWaveform.tintColor = Colors.waveform;
            
            // Zoom field/slider + reset button
            var zoomSlider = rootVisualElement.Q<Slider>(Controls.ZoomField);
            zoomSlider.lowValue = 1;
            zoomSlider.highValue = Settings.ZoomMax;
            zoomSlider.SetValueWithoutNotify(_zoom);
            zoomSlider.RegisterValueChangedCallback(OnZoomValueChanged);
            var resetZoomButton = rootVisualElement.Q<Button>(Controls.ZoomResetButton);
            resetZoomButton.clicked += () => UpdateZoom(1);

            // Time field + trim button
            var timeField = rootVisualElement.Q<FloatField>(Controls.TimeField);
            timeField.value = _Time;
            timeField.RegisterValueChangedCallback(OnTimeChanged);
            var trimButton = rootVisualElement.Q<Button>(Controls.TrimButton);
            trimButton.clicked += TrimTime;

            // Mouse mode button strip
            var mouseModeRadioGroup = rootVisualElement.Q<RadioButtonGroup>(Controls.MouseModeRadioGroup);
            SetupRadioButtonStripStyle(mouseModeRadioGroup);

            // Clear button
            var clearButton = rootVisualElement.Q<Button>(Controls.ClearButton);
            clearButton.clicked += Clear;

            // Point drag mode button strip
            var pointDragRadioGroup = rootVisualElement.Q<RadioButtonGroup>(Controls.PointDragRadioGroup);
            SetupRadioButtonStripStyle(pointDragRadioGroup);

            // Plot scroll
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            plotScroll.RegisterCallback<GeometryChangedEvent>(PlotScroll_GeometryChanged);
            plotScroll.contentViewport.RegisterCallback<WheelEvent>(OnWheelOverPlotScroll);
            var amplitudePlotYAxisLabels = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotYAxisLabels);
            amplitudePlotYAxisLabels.RegisterCallback<GeometryChangedEvent, string>(YAxisLabels_GeometryChanged, Controls.AmplitudePlot);
            var amplitudePlotXLabels = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotXAxisLabels);
            amplitudePlotXLabels.RegisterCallback<GeometryChangedEvent>(XAxisLabels_GeometryChanged);
            var frequencyPlotYAxisLabels = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotYAxisLabels);
            frequencyPlotYAxisLabels.RegisterCallback<GeometryChangedEvent, string>(YAxisLabels_GeometryChanged, Controls.FrequencyPlot);
            var frequencyPlotXLabels = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotXAxisLabels);
            frequencyPlotXLabels.RegisterCallback<GeometryChangedEvent>(XAxisLabels_GeometryChanged);
            var amplitudePlotPoints = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints);
            amplitudePlotPoints.RegisterCallback<PointerEnterEvent>(AmplitudePlot_PointerEntered);
            amplitudePlotPoints.RegisterCallback<PointerMoveEvent>(AmplitudePlot_PointerMoved);
            amplitudePlotPoints.RegisterCallback<PointerOutEvent>(AmplitudePlot_PointerOut);
            amplitudePlotPoints.RegisterCallback<PointerUpEvent>(AmplitudePlot_PointerUp);
            amplitudePlotPoints.RegisterCallback<PointerDownEvent>(AmplitudePlot_PointerDown);
            amplitudePlotPoints.generateVisualContent += AmplitudePlot_GenerateVisualContent;
            var frequencyPlotPoints = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotPoints);
            frequencyPlotPoints.RegisterCallback<PointerEnterEvent>(FrequencyPlot_PointerEntered);
            frequencyPlotPoints.RegisterCallback<PointerMoveEvent>(FrequencyPlot_PointerMoved);
            frequencyPlotPoints.RegisterCallback<PointerOutEvent>(FrequencyPlot_PointerOut);
            frequencyPlotPoints.RegisterCallback<PointerUpEvent>(FrequencyPlot_PointerUp);
            frequencyPlotPoints.RegisterCallback<PointerDownEvent>(FrequencyPlot_PointerDown);
            frequencyPlotPoints.generateVisualContent += FrequencyPlot_GenerateVisualContent;
        }

        void PlotScroll_GeometryChanged(GeometryChangedEvent geometryChangedEvent)
        {
            if (++_firstRepaintCounter > 2)
                ((VisualElement)geometryChangedEvent.target)
                    .UnregisterCallback<GeometryChangedEvent>(PlotScroll_GeometryChanged);
            else
                ((VisualElement)geometryChangedEvent.target).MarkDirtyRepaint();

            RepaintAudioWaveform();
        }

        void YAxisLabels_GeometryChanged(GeometryChangedEvent geometryChangedEvent, string plot)
        {
            DrawYAxisLabels((VisualElement)geometryChangedEvent.target, plot);
        }
        
        void XAxisLabels_GeometryChanged(GeometryChangedEvent geometryChangedEvent)
        {
            DrawXAxisLabels((VisualElement)geometryChangedEvent.target);
        }

        void DrawYAxisLabels(VisualElement labelsParent, string plotElementName)
        {
            labelsParent.Clear();
            var plot = rootVisualElement.Q<VisualElement>(plotElementName);
            float plotHeight = plot.worldBound.height;
            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            if (plotHeight < singleLineHeight)
                return;

            int yAxisLabelCount = Mathf.RoundToInt(Mathf.Clamp(plotHeight / singleLineHeight, 2, 11));
            if (yAxisLabelCount < 11)
            {
                if (yAxisLabelCount > 6) 
                    yAxisLabelCount = 6;
                else if (yAxisLabelCount == 4) 
                    yAxisLabelCount = 5;
            }
            float yAxisLabelInterval = 1f / (yAxisLabelCount - 1);
            float yAxisLabelHeightInterval = plotHeight / (yAxisLabelCount - 1);

            float yAxisLabelPosition = plot.ChangeCoordinatesTo(labelsParent, Vector2.zero).y;
            yAxisLabelPosition -= singleLineHeight * 0.5f;

            for (int i = 0; i < yAxisLabelCount; i++)
            {
                string valueLabel = (1 - i * yAxisLabelInterval).ToString("0.##");
                Label yAxisLabel = new(valueLabel);
                yAxisLabel.style.position = Position.Absolute;
                yAxisLabel.style.top = yAxisLabelPosition;
                yAxisLabel.style.right = 3;
                labelsParent.Add(yAxisLabel);
                yAxisLabelPosition += yAxisLabelHeightInterval;
            }
        }

        void DrawXAxisLabels(VisualElement labelsParent)
        {            
            labelsParent.Clear();
            float plotWidth = labelsParent.worldBound.width;
            Vector2 xAxisLabelSize = EditorStyles.label.CalcSize(new GUIContent("##.###"));
            xAxisLabelSize.x *= 1.5f;
            int xAxisLabelCount = (int)(plotWidth / xAxisLabelSize.x);
            float xAxisLabelWidthInterval = plotWidth / xAxisLabelCount;
            float xAxisLabelInterval = _Time / xAxisLabelCount;

            Label xAxisLabel = new("0");
            AddLabel(xAxisLabel);

            float xLabelOffset = xAxisLabelWidthInterval - xAxisLabelSize.x * 0.5f;
            for (int i = 1; i < xAxisLabelCount; i++) 
            {
                xAxisLabel = new((i * xAxisLabelInterval).ToString("#0.###"));
                xAxisLabel.style.left = xLabelOffset;
                xAxisLabel.style.width = xAxisLabelSize.x;
                xAxisLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                AddLabel(xAxisLabel);
                xLabelOffset += xAxisLabelWidthInterval;
            }

            xAxisLabel = new(_Time.ToString("#0.###"));
            xAxisLabel.style.right = 0;
            AddLabel(xAxisLabel);

            void AddLabel(Label label)
            {
                xAxisLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                xAxisLabel.style.position = Position.Absolute;
                xAxisLabel.style.top = 2;
                labelsParent.Add(label);
            }
        }

        void SetupRadioButtonStripStyle(RadioButtonGroup radioButtonGroup)
        {
            var radioButtonGroupContainer = radioButtonGroup.Q<VisualElement>(
                className: UnityUSS.RadioButtonGroupContainer);

            if (radioButtonGroupContainer.childCount >= 2)
            {
                List<VisualElement> radioButtons = radioButtonGroupContainer.Children().ToList();
                radioButtons[0].AddToClassList(USS.RadioButtonStripButtonLeft);
                radioButtons[^1].AddToClassList(USS.RadioButtonStripButtonRight);
                for (int i = 1; i < radioButtons.Count - 1; i++)
                    radioButtons[i].AddToClassList(USS.RadioButtonStripButtonMiddle);
            }
        }

        void SetupRadioButtonStripWithEnum(RadioButtonGroup radioButtonGroup, Enum sourceEnum)
        {
            string[] enumNames = sourceEnum.GetInspectorNames();
            radioButtonGroup.choices = enumNames;
            SetupRadioButtonStripStyle(radioButtonGroup);
        }

        void OnWheelOverPlotScroll(WheelEvent wheelEvent)
        {
            wheelEvent.PreventDefault();
            wheelEvent.StopPropagation();
            wheelEvent.StopImmediatePropagation();

            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            VisualElement contentContainer = plotScroll.contentContainer;
            float contentWidth = contentContainer.worldBound.width;
            float viewportWidth = plotScroll.contentViewport.worldBound.width;
            if (wheelEvent.ctrlKey)
            {
                float scrollMouseX = _scrollOffset + wheelEvent.localMousePosition.x;
                float posX = scrollMouseX / contentWidth;
                float newZoom = _zoom - Mathf.Sign(wheelEvent.delta.y) * Settings.ZoomDelta;
                newZoom = Mathf.Clamp(newZoom, 1, Settings.ZoomMax);
                contentWidth = viewportWidth * newZoom;
                _scrollOffset = posX * contentWidth - wheelEvent.localMousePosition.x;
                if (_scrollOffset == 0)
                    contentContainer.RegisterCallback<GeometryChangedEvent>(SetScrollOffset);
                UpdateZoom(newZoom);
            }
            else if (_zoom > 1f)
                _scrollOffset += contentWidth * Mathf.Sign(wheelEvent.delta.y) * Settings.ScrollDelta;
            else
                return;

            _scrollOffset = Mathf.Clamp(_scrollOffset, 0, contentWidth - viewportWidth);
            Vector2 scrollOffset = plotScroll.scrollOffset;
            scrollOffset.x = _scrollOffset;
            plotScroll.scrollOffset = scrollOffset;
        }

        void SetScrollOffset(GeometryChangedEvent geometryChangedEvent)
        {
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            Vector2 scrollOffset = plotScroll.scrollOffset;
            scrollOffset.x = _scrollOffset;
            plotScroll.scrollOffset = scrollOffset;
            plotScroll.contentContainer.UnregisterCallback<GeometryChangedEvent>(SetScrollOffset);
        }

        void OnZoomValueChanged(ChangeEvent<float> zoomChange) => UpdateZoom(zoomChange.newValue);

        void UpdateZoom(float newZoom)
        {
            var zoomSlider = rootVisualElement.Q<Slider>(Controls.ZoomField);
            _zoom = (float)Math.Round(newZoom, 1);
            zoomSlider.SetValueWithoutNotify(_zoom);
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            plotScroll.contentContainer.style.width = plotScroll.worldBound.width * newZoom;
            if (_WaveformClip != null && Mathf.Abs(newZoom - _lastPaintedZoom) >= Settings.WaveformRepaintZoomThreshold)
            {
                RepaintAudioWaveform();
                _lastPaintedZoom = newZoom;
            }
        }

        void OnTimeChanged(ChangeEvent<float> changeEvent)
        {
            _Time = Mathf.Clamp(changeEvent.newValue, GetMinTime(), Settings.TimeMax);
            ((FloatField)changeEvent.target).SetValueWithoutNotify(_Time);
            RepaintAudioWaveform();
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            plotScroll.MarkDirtyRepaint();
            DrawXAxisLabels(rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotXAxisLabels));
            DrawXAxisLabels(rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotXAxisLabels));
        }

        void TrimTime()
        {
            _Time = GetMinTime();
            var timeField = rootVisualElement.Q<FloatField>(Controls.TimeField);
            timeField.value = _Time;

            rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints).MarkDirtyRepaint();
            rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotPoints).MarkDirtyRepaint();
        }

        void OnWaveformAssetChanged(ChangeEvent<Object> changeEvent)
        {
            _WaveformClip = changeEvent.newValue as AudioClip;
            bool clipExists = _WaveformClip != null;
            var waveformVisibility = rootVisualElement.Q<VisualElement>(Controls.WaveformVisibility);
            waveformVisibility.SetEnabled(clipExists);
            var waveformScale = rootVisualElement.Q<FloatField>(Controls.WaveformRenderScale);
            waveformScale.SetEnabled(clipExists);
            var playButton = rootVisualElement.Q<Button>(Controls.PlayButton);
            playButton.SetEnabled(clipExists);
            var audioAnalysisPanel = rootVisualElement.Q<VisualElement>(Controls.AudioAnalysisPanel);
            audioAnalysisPanel.SetEnabled(clipExists);
            if (clipExists)
                TrimTime();
            RepaintAudioWaveform();
        }

        void ToggleWaveformVisible(ChangeEvent<bool> changeEvent) 
        {
            _WaveformVisible = changeEvent.newValue;
            RepaintAudioWaveform();
        }
        
        void ToggleWaveformNormalize(ChangeEvent<bool> changeEvent) 
        {
            _WaveformNormalize = changeEvent.newValue;
            RepaintAudioWaveform();
        }

        void OnWaveformRenderScaleChanged(ChangeEvent<float> changeEvent)
        {
            _WaveformRenderScale = Mathf.Clamp(changeEvent.newValue,
                Settings.WaveformRenderScaleMin, Settings.WaveformRenderScaleMax);
            ((FloatField)changeEvent.target).SetValueWithoutNotify(_WaveformRenderScale);
            RepaintAudioWaveform();
        }

        void OnWaveformHeightMultiplierChanged(ChangeEvent<float> changeEvent)
        {
            _WaveformHeightMultiplier = Mathf.Clamp(changeEvent.newValue,
                Settings.WaveformHeightMultiplierMin, Settings.WaveformHeightMultiplierMax);
            ((Slider)changeEvent.target).SetValueWithoutNotify(_WaveformHeightMultiplier);
            RepaintAudioWaveform();
        }

        void AmplitudePlot_GenerateVisualContent(MeshGenerationContext context)
        {
            DrawGridLines(context, Controls.AmplitudePlotYAxisLabels, true);
            DrawGridLines(context, Controls.AmplitudePlotXAxisLabels, false);

            var painter = context.painter2D;
            Vector2 plotSize = context.visualElement.worldBound.size;

            if (_amplitudeMousePosition != null && _hoverPoint != null)
            {
                Vector2 plotPoint = RealToPlotPoint(_hoverPoint, plotSize);
                painter.DrawFilledCircle(plotPoint, Settings.HoverHighlightSize, Colors.hovered);
            }

            foreach (var hapticEvent in _Events)
            {
                if (hapticEvent is TransientEvent transientEvent)
                {
                    Vector2 plotPoint = RealToPlotPoint(transientEvent.Intensity, plotSize);
                    painter.DrawFilledCircle(plotPoint, Settings.EventPointRadius, Colors.eventTransient);
                    painter.DrawLine(Settings.EventLineWidth, Colors.eventTransient, false, null, plotPoint,
                        new Vector2(plotPoint.x, plotSize.y));
                }
                else if (hapticEvent is ContinuousEvent continuousEvent)
                {
                    List<Vector2> points = new();
                    Vector2 plotPoint = RealToPlotPoint(new Vector2(continuousEvent.IntensityCurve[0].Time, 0), plotSize);
                    points.Add(plotPoint);
                    foreach (var realPoint in continuousEvent.IntensityCurve)
                    {
                        plotPoint = RealToPlotPoint(realPoint, plotSize);
                        points.Add(plotPoint);
                        painter.DrawFilledCircle(plotPoint, Settings.EventPointRadius, Colors.eventContinuous);
                    }
                    plotPoint = RealToPlotPoint(new Vector2(continuousEvent.IntensityCurve[^1].Time, 0), plotSize);
                    points.Add(plotPoint);
                    painter.DrawLine(Settings.EventLineWidth, Colors.eventContinuous, false, LineJoin.Bevel, points.ToArray());
                }
            }

            if (_amplitudeMousePosition != null)
            {
                Vector2 amplitudeMousePosition = _amplitudeMousePosition.Value;
                if (context.visualElement.ContainsPointWithBorders(amplitudeMousePosition))
                {
                    DrawHoverGuides(context, amplitudeMousePosition);
                    if (_mouseState == MouseState.Dragging)
                    {
                        Vector2 firstPoint = _dragStartPosition.Value;
                        Vector2 secondPoint = amplitudeMousePosition;
                        if (secondPoint.x < firstPoint.x)
                            (firstPoint, secondPoint) = (secondPoint, firstPoint);

                        Vector2 leftPoint = PlotToRealPoint(firstPoint, plotSize);
                        Vector2 rightPoint = PlotToRealPoint(secondPoint, plotSize);
                        Color c = AnyContinuousEventsExist(leftPoint.x, rightPoint.x) ? 
                            Colors.invalid : Colors.eventContinuousCreation;
                        painter.DrawPolygon(c, true, new Vector2(firstPoint.x, plotSize.y),
                            firstPoint, secondPoint, new Vector2(secondPoint.x, plotSize.y));
                    }
                }
                else if (amplitudeMousePosition.x > 0)
                    DrawVerticalHoverGuide(context, amplitudeMousePosition);
            }
            else if (_frequencyMousePosition != null)
                DrawVerticalHoverGuide(context, _frequencyMousePosition.Value);
        }
        
        void FrequencyPlot_GenerateVisualContent(MeshGenerationContext context)
        {
            DrawGridLines(context, Controls.FrequencyPlotYAxisLabels, true);
            DrawGridLines(context, Controls.FrequencyPlotXAxisLabels, false);

            var painter = context.painter2D;
            Vector2 plotSize = context.visualElement.worldBound.size;

            if (_frequencyMousePosition != null && _hoverPoint != null)
            {
                Vector2 plotPoint = RealToPlotPoint(_hoverPoint, plotSize);
                painter.DrawFilledCircle(plotPoint, Settings.HoverHighlightSize, Colors.hovered);
            }

            foreach (var hapticEvent in _Events)
            {
                if (hapticEvent is TransientEvent transientEvent)
                {
                    Vector2 plotPoint = RealToPlotPoint(transientEvent.Sharpness, plotSize);
                    painter.DrawFilledCircle(plotPoint, Settings.EventPointRadius, Colors.eventTransient);
                    painter.DrawLine(Settings.EventLineWidth, Colors.eventTransient, false, null, plotPoint,
                        new Vector2(plotPoint.x, plotSize.y));
                }
                else if (hapticEvent is ContinuousEvent continuousEvent)
                {
                    List<Vector2> points = new();
                    Vector2 plotPoint = RealToPlotPoint(new Vector2(continuousEvent.SharpnessCurve[0].Time, 0), plotSize);
                    points.Add(plotPoint);
                    foreach (var realPoint in continuousEvent.SharpnessCurve)
                    {
                        plotPoint = RealToPlotPoint(realPoint, plotSize);
                        points.Add(plotPoint);
                        painter.DrawFilledCircle(plotPoint, Settings.EventPointRadius, Colors.eventContinuous);
                    }
                    plotPoint = RealToPlotPoint(new Vector2(continuousEvent.SharpnessCurve[^1].Time, 0), plotSize);
                    points.Add(plotPoint);
                    painter.DrawLine(Settings.EventLineWidth, Colors.eventContinuous, false, LineJoin.Bevel, points.ToArray());
                }
            }

            if (_frequencyMousePosition != null) 
            {
                Vector2 frequencyMousePosition = _frequencyMousePosition.Value;
                if (context.visualElement.ContainsPointWithBorders(frequencyMousePosition))
                {
                    DrawHoverGuides(context, frequencyMousePosition);
                    if (_mouseState == MouseState.Dragging)
                    {
                        Vector2 firstPoint = _dragStartPosition.Value;
                        Vector2 secondPoint = frequencyMousePosition;
                        if (secondPoint.x < firstPoint.x)
                            (firstPoint, secondPoint) = (secondPoint, firstPoint);

                        Vector2 leftPoint = PlotToRealPoint(firstPoint, plotSize);
                        Vector2 rightPoint = PlotToRealPoint(secondPoint, plotSize);
                        Color c = AnyContinuousEventsExist(leftPoint.x, rightPoint.x) ?
                            Colors.invalid : Colors.eventContinuousCreation;
                        painter.DrawPolygon(c, true, new Vector2(firstPoint.x, plotSize.y),
                            firstPoint, secondPoint, new Vector2(secondPoint.x, plotSize.y));
                    }
                }
                else if (frequencyMousePosition.x >= 0)
                    DrawVerticalHoverGuide(context, frequencyMousePosition);
            }
            else if (_amplitudeMousePosition != null)
                DrawVerticalHoverGuide(context, _amplitudeMousePosition.Value);
        }

        void DrawGridLines(MeshGenerationContext context, string labelsParentName, bool horizontal)
        {
            int labelCount = rootVisualElement.Q<VisualElement>(labelsParentName).childCount;
            if (labelCount <= 2)
                return;

            Vector2 firstPoint = Vector2.zero;
            Vector2 secondPoint = context.visualElement.worldBound.size;
            float interval = (horizontal ? secondPoint.y : secondPoint.x) / (labelCount - 1);
            if (horizontal)
                firstPoint.y = secondPoint.y = interval;
            else
                firstPoint.x = secondPoint.x = interval;

            context.painter2D.DrawLinesWithInterval(firstPoint, secondPoint, interval, labelCount - 2,
                horizontal, Settings.GridLineWidth, context.visualElement.parent.resolvedStyle.borderTopColor);
        }

        void DrawHoverGuides(MeshGenerationContext context, Vector2 mousePosition)
        {
            var painter = context.painter2D;
            Vector2[] points = { new(mousePosition.x, 0),
                new(mousePosition.x, context.visualElement.worldBound.height) };
            painter.DrawLine(Settings.HoverGuideLineWidth, Colors.hoverGuides, points: points);
            points[0].Set(0, mousePosition.y);
            points[1].Set(context.visualElement.worldBound.width, mousePosition.y);
            painter.DrawLine(points: points);
            if (_mouseState == MouseState.Unclicked)
                painter.DrawFilledCircle(mousePosition, Settings.HoverGuideCircleRadius, Colors.hoverGuides);
        }

        void DrawVerticalHoverGuide(MeshGenerationContext context, Vector2 mousePosition)
        {
            context.painter2D.DrawLine(Settings.HoverGuideLineWidth, Colors.hoverGuides, false, null,
                new(mousePosition.x, 0), new(mousePosition.x, context.visualElement.worldBound.height));
        }

        void AmplitudePlot_PointerEntered(PointerEnterEvent pointerEnterEvent)
        {
            if (_frequencyMousePosition == null)
                HandlePlotPointerHover(pointerEnterEvent.target, pointerEnterEvent.localPosition,
                    ref _amplitudeMousePosition, "Amplitude", Controls.FrequencyPlotPoints);
        }

        void AmplitudePlot_PointerMoved(PointerMoveEvent pointerMoveEvent)
        {
            if (_mouseState == MouseState.Clicked)
            {
                Vector2 point = PlotToRealPoint(_dragStartPosition.Value, ((VisualElement)pointerMoveEvent.target).worldBound.size);
                _mouseState = TryGetContinuousEvent(point.x, out _) ? MouseState.Unclicked : MouseState.Dragging;
            }

            if (_frequencyMousePosition == null)
                HandlePlotPointerHover(pointerMoveEvent.target, pointerMoveEvent.localPosition,
                    ref _amplitudeMousePosition, "Amplitude", Controls.FrequencyPlotPoints);
        }

        void AmplitudePlot_PointerOut(PointerOutEvent pointerOutEvent)
        {
            if (_frequencyMousePosition == null)
                HandlePlotPointerHover(pointerOutEvent.target, null,
                    ref _amplitudeMousePosition, "Amplitude", Controls.FrequencyPlotPoints);
        }

        void AmplitudePlot_PointerDown(PointerDownEvent pointerDownEvent)
        {
            var amplitudePlot = (VisualElement)pointerDownEvent.target;
            amplitudePlot.CapturePointer(0);
            _mouseState = MouseState.Clicked;
            _dragStartPosition = SnapPointerPosition(pointerDownEvent.localPosition, amplitudePlot.worldBound.size);
        }

        void AmplitudePlot_PointerUp(PointerUpEvent pointerUpEvent)
        {
            var amplitudePlot = (VisualElement)pointerUpEvent.target;
            if (_amplitudeMousePosition != null && !amplitudePlot.ContainsPointWithBorders(_amplitudeMousePosition.Value))
            {
                HandlePlotPointerHover(amplitudePlot, null,
                    ref _amplitudeMousePosition, "Amplitude", Controls.FrequencyPlotPoints);

                var frequencyPlot = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotPoints);
                Vector2 frequencyPlotLocalPointerPosition = frequencyPlot.WorldToLocal(pointerUpEvent.position);
                if (frequencyPlot.ContainsPoint(frequencyPlotLocalPointerPosition))
                {
                    HandlePlotPointerHover(frequencyPlot, frequencyPlotLocalPointerPosition,
                        ref _frequencyMousePosition, "Frequency", Controls.AmplitudePlotPoints);
                }
            }
            else if (pointerUpEvent.button == (int)MouseButton.LeftMouse)
            {
                Vector2 plotSize = amplitudePlot.worldBound.size;
                if (_mouseState == MouseState.Clicked)
                {
                    Vector2 point = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                    if (TryGetContinuousEvent(point.x, out ContinuousEvent ce))
                        ce.AddPointToCurve(point, MouseLocation.IntensityPlot);
                    else
                        _Events.Add(new TransientEvent(point, MouseLocation.IntensityPlot));
                }
                else if (_mouseState == MouseState.Dragging)
                {
                    Vector2 firstPoint = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                    Vector2 secondPoint = SnapPointerPosition(pointerUpEvent.localPosition, plotSize);
                    secondPoint = PlotToRealPoint(secondPoint, plotSize);
                    if (secondPoint.x < firstPoint.x)
                        (firstPoint, secondPoint) = (secondPoint, firstPoint);

                    if (!AnyContinuousEventsExist(firstPoint.x, secondPoint.x))
                        _Events.Add(new ContinuousEvent(firstPoint, secondPoint, MouseLocation.IntensityPlot));
                }
                HandlePlotPointerHover(amplitudePlot, pointerUpEvent.localPosition,
                    ref _amplitudeMousePosition, "Amplitude", Controls.FrequencyPlotPoints);
            }

            amplitudePlot.ReleasePointer(0);
            _dragStartPosition = null;
            _mouseState = MouseState.Unclicked;
        }

        void FrequencyPlot_PointerEntered(PointerEnterEvent pointerEnterEvent)
        {
            if (_amplitudeMousePosition == null)
                HandlePlotPointerHover(pointerEnterEvent.target, pointerEnterEvent.localPosition,
                    ref _frequencyMousePosition, "Frequency", Controls.AmplitudePlotPoints);
        }
        
        void FrequencyPlot_PointerMoved(PointerMoveEvent pointerMoveEvent)
        {
            if (_mouseState == MouseState.Clicked)
            {
                Vector2 point = PlotToRealPoint(_dragStartPosition.Value, ((VisualElement)pointerMoveEvent.target).worldBound.size);
                _mouseState = TryGetContinuousEvent(point.x, out _) ? MouseState.Unclicked : MouseState.Dragging;
            }

            if (_amplitudeMousePosition == null)
                HandlePlotPointerHover(pointerMoveEvent.target, pointerMoveEvent.localPosition,
                    ref _frequencyMousePosition, "Frequency", Controls.AmplitudePlotPoints);
        }

        void FrequencyPlot_PointerOut(PointerOutEvent pointerOutEvent)
        {
            if (_amplitudeMousePosition == null)
                HandlePlotPointerHover(pointerOutEvent.target, null,
                    ref _frequencyMousePosition, "Frequency", Controls.AmplitudePlotPoints);
        }

        void FrequencyPlot_PointerDown(PointerDownEvent pointerDownEvent)
        {
            var frequencyPlot = (VisualElement)pointerDownEvent.target;
            frequencyPlot.CapturePointer(0);
            _mouseState = MouseState.Clicked;
            _dragStartPosition = SnapPointerPosition(pointerDownEvent.localPosition, frequencyPlot.worldBound.size);
        }

        void FrequencyPlot_PointerUp(PointerUpEvent pointerUpEvent) 
        {
            var frequencyPlot = (VisualElement)pointerUpEvent.target;
            if (_frequencyMousePosition != null && !frequencyPlot.ContainsPointWithBorders(_frequencyMousePosition.Value))
            {
                HandlePlotPointerHover(frequencyPlot, null,
                    ref _frequencyMousePosition, "Frequency", Controls.AmplitudePlotPoints);

                var amplitudePlot = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints);
                Vector2 amplitudePlotLocalPointerPosition = amplitudePlot.WorldToLocal(pointerUpEvent.position);
                if (amplitudePlot.ContainsPoint(amplitudePlotLocalPointerPosition))
                {
                    HandlePlotPointerHover(amplitudePlot, amplitudePlotLocalPointerPosition,
                        ref _amplitudeMousePosition, "Amplitude", Controls.FrequencyPlotPoints);
                }
            }
            else if (pointerUpEvent.button == (int)MouseButton.LeftMouse)
            {
                Vector2 plotSize = frequencyPlot.worldBound.size;
                if (_mouseState == MouseState.Clicked)
                {
                    Vector2 point = PlotToRealPoint(pointerUpEvent.localPosition, plotSize);
                    if (TryGetContinuousEvent(point.x, out ContinuousEvent ce))
                        ce.AddPointToCurve(point, MouseLocation.SharpnessPlot);
                    else
                        _Events.Add(new TransientEvent(point, MouseLocation.SharpnessPlot));
                }
                else if (_mouseState == MouseState.Dragging)
                {
                    Vector2 firstPoint = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                    Vector2 secondPoint = PlotToRealPoint(pointerUpEvent.localPosition, plotSize);
                    if (secondPoint.x < firstPoint.x)
                        (firstPoint, secondPoint) = (secondPoint, firstPoint);

                    if (!AnyContinuousEventsExist(firstPoint.x, secondPoint.x))
                        _Events.Add(new ContinuousEvent(firstPoint, secondPoint, MouseLocation.SharpnessPlot));
                }
                HandlePlotPointerHover(frequencyPlot, pointerUpEvent.localPosition,
                    ref _frequencyMousePosition, "Frequency", Controls.AmplitudePlotPoints);
            }

            frequencyPlot.ReleasePointer(0);
            _dragStartPosition = null;
            _mouseState = MouseState.Unclicked;
        }

        void HandlePlotPointerHover(IEventHandler target, Vector2? pointerPosition, 
            ref Vector2? positionCache, string plotName, string otherPlotElement)
        {
            VisualElement plot = (VisualElement)target;
            plot.MarkDirtyRepaint();
            rootVisualElement.Q<VisualElement>(otherPlotElement).MarkDirtyRepaint();
            var hoverPlotName = rootVisualElement.Q<Label>(Controls.HoverPlotName);
            var hoverTime = rootVisualElement.Q<Label>(Controls.HoverTime);
            var hoverValue = rootVisualElement.Q<Label>(Controls.HoverValue);
            if (pointerPosition != null)
            {
                _hoverPoint = GetPointOnPosition(PlotToRealPoint(pointerPosition.Value, plot.worldBound.size),
                    plotName == "Amplitude" ? MouseLocation.IntensityPlot : MouseLocation.SharpnessPlot);
                positionCache = SnapPointerPosition(pointerPosition.Value, plot.worldBound.size);
                hoverPlotName.text = plotName;
                Vector2 clampedPointerPosition = PlotToRealPoint(positionCache.Value, plot.worldBound.size);
                clampedPointerPosition.x = Mathf.Clamp(clampedPointerPosition.x, 0, _Time);
                clampedPointerPosition.y = Mathf.Clamp01(clampedPointerPosition.y);
                hoverTime.text = clampedPointerPosition.x.ToString();
                hoverValue.text = clampedPointerPosition.y.ToString();
            }
            else
            {
                positionCache = null;
                hoverPlotName.text = hoverTime.text = hoverValue.text = "-";
                _hoverPoint = null;
            }
        }

        void RepaintAudioWaveform()
        {
            var amplitudePlot = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlot);
            var amplitudePlotWaveform = rootVisualElement.Q<Image>(Controls.AmplitudePlotWaveform);
            var frequencyPlotWaveform = rootVisualElement.Q<Image>(Controls.FrequencyPlotWaveform);
            Rect plotRect = amplitudePlot.worldBound;

            if (_WaveformClip == null || !_WaveformVisible || float.IsNaN(plotRect.width) || plotRect.width == 0)
                _waveformTexture = null;
            else
            {
                float waveformSize = _WaveformClip.length / _Time;
                float waveformScaledSize = waveformSize * _WaveformRenderScale;
                _waveformTexture = AudioClipUtils.PaintAudioWaveform(_WaveformClip, (int)(plotRect.width * waveformScaledSize),
                    (int)(plotRect.height * waveformScaledSize), Color.clear, Color.white, _WaveformNormalize, _WaveformHeightMultiplier);
                amplitudePlotWaveform.style.width = frequencyPlotWaveform.style.width = Length.Percent(waveformSize * 100);
            }

            amplitudePlotWaveform.image = frequencyPlotWaveform.image = _waveformTexture;
        }

        float GetMinTime()
        {
            return Mathf.Max(GetLastPointTime(), Settings.TimeMin,
                _WaveformClip != null ? _WaveformClip.length : 0);
        }

        float GetLastPointTime()
        {
            float lastPointTime = 0, t;
            foreach (var ev in _Events)
            {
                t = ev is TransientEvent ? ev.Time : ((ContinuousEvent)ev).IntensityCurve[^1].Time;
                lastPointTime = Mathf.Max(t, lastPointTime);
            }
            return lastPointTime;
        }

        Vector2 SnapPointerPosition(Vector2 pointerPosition, Vector2 plotSize)
        {
            var snapModeField = rootVisualElement.Q<EnumField>(Controls.SnapModeEnumField);
            var snapMode = (SnapMode)snapModeField.value;
            if (snapMode != SnapMode.None)
            {
                pointerPosition = PlotToRealPoint(pointerPosition, plotSize);
                pointerPosition.x = (float)Math.Round(pointerPosition.x, (int)snapMode);
                pointerPosition.y = (float)Math.Round(pointerPosition.y, (int)snapMode);
                pointerPosition = RealToPlotPoint(pointerPosition, plotSize);
            }
            return pointerPosition;
        }

        Vector2 PlotToRealPoint(Vector2 plotPosition, Vector2 plotSize)
        {
            return new Vector2(plotPosition.x / plotSize.x * _Time,
                (plotSize.y - plotPosition.y) / plotSize.y);
        }

        Vector2 RealToPlotPoint(Vector2 realPosition, Vector2 plotSize)
        {
            return new Vector2(realPosition.x / _Time * plotSize.x,
                plotSize.y - (realPosition.y * plotSize.y));
        }

        bool TryGetContinuousEvent(float time, out ContinuousEvent continuousEvent)
        {
            foreach (var ev in _Events)
            {
                if (time >= ev.Time && ev is ContinuousEvent ce && time <= ce.IntensityCurve[^1].Time)
                {
                    continuousEvent = ce;
                    return true;
                }
            }
            continuousEvent = null;
            return false;
        }

        bool TryGetContinuousEvents(float time1, float time2, out List<ContinuousEvent> continuousEvents)
        {
            continuousEvents = new List<ContinuousEvent>();
            foreach (var ev in _Events) 
            {
                if (ev is ContinuousEvent ce && (time1 <= ce.IntensityCurve[^1].Time && time2 >= ce.Time))
                    continuousEvents.Add(ce);
            }
            return continuousEvents.Count > 0;
        }

        bool AnyContinuousEventsExist(float time1, float time2)
        {
            foreach (var ev in _Events)
            {
                if (ev is ContinuousEvent ce && (time2 >= ce.Time && time1 <= ce.IntensityCurve[^1].Time))
                    return true;
            }
            return false;
        }

        EventPoint GetPointOnPosition(Vector2 plotPosition, MouseLocation plot)
        {
            if (plot == MouseLocation.Outside) 
                return null;

            var plotElement = rootVisualElement.Q<VisualElement>(plot == MouseLocation.IntensityPlot ? 
                Controls.AmplitudePlot : Controls.FrequencyPlot);
            Vector2 plotSize = plotElement.worldBound.size;
            Vector2 pointOffset = new(Settings.HoverOffset * _Time / plotSize.x, Settings.HoverOffset / plotSize.y);
            Rect offsetRect = new(plotPosition - pointOffset, pointOffset * 2);
            foreach (var ev in _Events)
            {
                if (ev.IsOnPointInEvent(in offsetRect, plot, out EventPoint eventPoint))
                    return eventPoint;
            }
            return null;
        }
    }

    internal enum SnapMode
    {
        None = 0,
        [InspectorName("0.1")] Tenth = 1,
        [InspectorName("0.01")] Hundredth = 2,
        [InspectorName("0.001")] Thousandth = 3
    }

    internal enum PointDragMode { FreeMove = 0, LockTime = 1, LockValue = 2 }
}
