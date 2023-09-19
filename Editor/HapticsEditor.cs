using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using PopupWindow = UnityEditor.PopupWindow;
using Object = UnityEngine.Object;
using Chroma.UIToolkit.Utility;
using Chroma.Haptics.EditorWindow.Generation;

//TODO: fold queries with 1 change to one line
namespace Chroma.Haptics.EditorWindow
{
    public partial class HapticsEditor : UnityEditor.EditorWindow, IHasCustomMenu
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
            public const string SidePanelToggle = "sidePanelToggle";
            public const string BottomPart = "bottomPart";

            public const string PointDragRadioGroup = "pointDragRadioGroup";
            public const string SnapModeEnumField = "snappingEnum";
            
            public const string AudioAnalysisPanel = "audioAnalysisPanel";
            public const string TransientsOnsetsGenerator = "transientsOnsetsButton";
            public const string ContinuousEnvelopeGenerator = "continuousEnvelopeButton";

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

            public const string SelectionNone = "selectionNone";
            public const string SelectionSingle = "selectionSingle";
            public const string SelectionSingleTime = "selectionTimeField";
            public const string SelectionSingleValue = "selectionValueField";
            public const string SelectionMulti = "selectionMulti";
            public const string SelectionMultiData = "selectionMultiCount";

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
            public const float HoverHighlightSize = 11f;
            public const float NeighbourPointOffset = 0.001f;
            public const float SelectHighlightSize = 12f;
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
        [SerializeField] Object _VibrationAsset;
        [SerializeField] AudioClip _WaveformClip;
        [SerializeField] bool _WaveformVisible;
        [SerializeField] bool _WaveformNormalize;
        [SerializeField] float _WaveformRenderScale;
        [SerializeField] float _WaveformHeightMultiplier;
        [SerializeField] float _Time;
        [SerializeField] float _Zoom;
        [SerializeField] string _ProjectName;
        [SerializeField] string _ProjectVersion;

        Texture2D _waveformTexture;
        float _lastPaintedZoom;
        float _scrollOffset;
        int _firstRepaintCounter;
        MouseState _mouseState;
        Vector2? _dragStartPosition;
        EventPoint _hoverPoint;
        EventPoint _draggedPoint;
        List<EventPoint> _selectedPoints;
        MouseLocation _selectedPointsLocation;
        PlotInfo _amplitudePlotInfo;
        PlotInfo _frequencyPlotInfo;
        float _dragMin, _dragMax, _dragMinBound, _dragMaxBound;
        float _dragValueMin, _dragValueMax, _dragValueMinBound, _dragValueMaxBound;
        bool _selectingPoints;

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Log Events"), false, LogEvents);
        }

        void LogEvents()
        {
            StringBuilder sb = new("[HapticsEditor] Events: ");
            if (_Events == null || _Events.Count == 0) 
            {
                sb.Append("None");
            }
            else
            {
                sb.AppendLine(_Events.Count.ToString());
                foreach (var e in _Events)
                {
                    switch(e)
                    {
                        case TransientEvent:
                            sb.Append("Transient | ");
                            break;
                        case ContinuousEvent:
                            sb.Append("Continuous | ");
                            break;
                        default: 
                            break;
                    }
                    sb.Append(e.Time);
                    if (e is ContinuousEvent ce)
                    {
                        sb.AppendFormat("-{0}, {1} Intensity points, {2} Sharpness points",
                            ce.TimeMax, ce.IntensityCurve.Count, ce.SharpnessCurve.Count);
                    }
                    sb.AppendLine();
                }
            }
            Debug.Log(sb.ToString());
        }

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
            _Zoom = _lastPaintedZoom = 1;
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
            if (_amplitudePlotInfo != null)
                _amplitudePlotInfo.MousePosition = null;
            if (_frequencyPlotInfo != null)
                _frequencyPlotInfo.MousePosition = null;
            _dragStartPosition = null;
            _mouseState = MouseState.Unclicked;
            _hoverPoint = null;
            _draggedPoint = null;
            _selectedPoints ??= new List<EventPoint>();
        }

        void Clear()
        {
            _Events?.Clear();
            _Events ??= new List<HapticEvent>();
            _selectedPoints?.Clear();
            _selectedPoints ??= new List<EventPoint>();
            RefreshSelectionInfo();
            RepaintPoints();
        }

        private void RepaintPoints()
        {
            rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints).MarkDirtyRepaint();
            rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotPoints).MarkDirtyRepaint();
        }

        //TODO: Separate into multiple methods
        //TODO: Cache some frequently used controls
        public void CreateGUI()
        {
            // Instantiate UXML template
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Settings.UxmlPath);
            VisualElement windowUxml = visualTree.Instantiate();
            windowUxml.style.flexGrow = 1; // Fix for TemplateContainer not expanding
            rootVisualElement.Add(windowUxml);
            _firstRepaintCounter = 0;

            // Bind data
            SerializedObject so = new(this);
            rootVisualElement.Bind(so);

            Initialize();

            // Import button
            var importButton = rootVisualElement.Q<Button>(Controls.ImportButton);
            importButton.clicked += () => HapticsFileImporter.Import(_VibrationAsset, _Events, Clear, OnImportSuccess);

            // Save button
            var saveButton = rootVisualElement.Q<Button>(Controls.SaveButton);
            saveButton.clicked += () => PopupWindow.Show(saveButton.worldBound, new SavePopup(saveOptions =>
            {
                saveOptions.ProjectName = _ProjectName;
                saveOptions.ProjectVersion = _ProjectVersion;
                HapticsFileExporter.Export(_Events, saveOptions, ref _VibrationAsset);
            }));
            
            // Waveform panel
            var waveformField = rootVisualElement.Q<ObjectField>(Controls.WaveformField);
            waveformField.RegisterValueChangedCallback(OnWaveformAssetChanged);
            var playButton = rootVisualElement.Q<Button>(Controls.PlayButton);
            playButton.clicked += () => AudioClipUtils.PlayClip(_WaveformClip);
            var stopButton = rootVisualElement.Q<Button>(Controls.StopButton);
            stopButton.clicked += AudioClipUtils.StopAllClips;
            var waveformVisibleToggle = rootVisualElement.Q<Toggle>(Controls.WaveformVisible);
            waveformVisibleToggle.RegisterValueChangedCallback(OnWaveformBoolChanged);
            var waveformNormalizeToggle = rootVisualElement.Q<Toggle>(Controls.WaveformNormalize);
            waveformNormalizeToggle.RegisterValueChangedCallback(OnWaveformBoolChanged);
            var waveformRenderScaleField = rootVisualElement.Q<FloatField>(Controls.WaveformRenderScale);
            waveformRenderScaleField.RegisterValueChangedCallback(OnWaveformRenderScaleChanged);
            var waveformHeightMultiplierField = rootVisualElement.Q<Slider>(Controls.WaveformHeightMultiplier);
            waveformHeightMultiplierField.lowValue = Settings.WaveformHeightMultiplierMin;
            waveformHeightMultiplierField.highValue = Settings.WaveformHeightMultiplierMax;
            waveformHeightMultiplierField.RegisterValueChangedCallback(OnWaveformHeightMultiplierChanged);
            var amplitudePlotWaveform = rootVisualElement.Q<Image>(Controls.AmplitudePlotWaveform);
            var frequencyPlotWaveform = rootVisualElement.Q<Image>(Controls.FrequencyPlotWaveform);
            amplitudePlotWaveform.scaleMode = frequencyPlotWaveform.scaleMode = ScaleMode.StretchToFill;
            amplitudePlotWaveform.tintColor = frequencyPlotWaveform.tintColor = Colors.waveform;
            
            // Zoom field/slider
            var zoomSlider = rootVisualElement.Q<Slider>(Controls.ZoomField);
            zoomSlider.lowValue = 1;
            zoomSlider.highValue = Settings.ZoomMax;
            zoomSlider.RegisterValueChangedCallback(OnZoomValueChanged);

            // Zoom reset button
            var resetZoomButton = rootVisualElement.Q<Button>(Controls.ZoomResetButton);
            resetZoomButton.clicked += () => UpdateZoom(1);

            // Time field + trim button
            var timeField = rootVisualElement.Q<FloatField>(Controls.TimeField);
            timeField.RegisterValueChangedCallback(OnTimeChanged);
            var trimButton = rootVisualElement.Q<Button>(Controls.TrimButton);
            trimButton.clicked += TrimTime;

            // Mouse mode button strip
            var mouseModeRadioGroup = rootVisualElement.Q<RadioButtonGroup>(Controls.MouseModeRadioGroup);
            SetupRadioButtonStripStyle(mouseModeRadioGroup);

            // Clear button
            var clearButton = rootVisualElement.Q<Button>(Controls.ClearButton);
            clearButton.clicked += Clear;

            // Side panel toggle
            var sidePanelToggle = rootVisualElement.Q<Toggle>(Controls.SidePanelToggle);
            sidePanelToggle.SetValueWithoutNotify(true);
            sidePanelToggle.RegisterValueChangedCallback(ToggleSidePanel);

            // Point drag mode button strip
            var pointDragRadioGroup = rootVisualElement.Q<RadioButtonGroup>(Controls.PointDragRadioGroup);
            SetupRadioButtonStripStyle(pointDragRadioGroup);

            // Generators
            var transientsGeneratorButton = rootVisualElement.Q<Button>(Controls.TransientsOnsetsGenerator);
            transientsGeneratorButton.clicked += () => PopupWindow.Show(transientsGeneratorButton.worldBound,
                new TransientsOnsetsGenerator(_WaveformClip, events =>
                {
                    _Events.AddRange(events);
                    RepaintPoints();
                }));

            var continuousGeneratorButton = rootVisualElement.Q<Button>(Controls.ContinuousEnvelopeGenerator);
            continuousGeneratorButton.clicked += () => PopupWindow.Show(continuousGeneratorButton.worldBound,
                new ContinuousEnvelopeGenerator(_WaveformClip, events =>
                {
                    Clear();
                    _Events.AddRange(events);
                    RepaintPoints();
                }));

            // Plot scroll
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            var amplitudePlotYLabels = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotYAxisLabels);
            var amplitudePlotXLabels = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotXAxisLabels);
            var frequencyPlotYLabels = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotYAxisLabels);
            var frequencyPlotXLabels = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotXAxisLabels);
            var amplitudePlotPoints = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints);
            var frequencyPlotPoints = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotPoints);

            _amplitudePlotInfo = new PlotInfo()
            {
                Plot = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlot),
                PlotPoints = amplitudePlotPoints,
                PlotWaveform = amplitudePlotWaveform,
                XAxisLabelsParent = amplitudePlotXLabels,
                YAxisLabelsParent = amplitudePlotYLabels,
                Name = "Amplitude",
                MouseLocation = MouseLocation.IntensityPlot
            };
            _frequencyPlotInfo = new PlotInfo()
            {
                Plot = rootVisualElement.Q<VisualElement>(Controls.FrequencyPlot),
                PlotPoints = frequencyPlotPoints,
                PlotWaveform = frequencyPlotWaveform,
                XAxisLabelsParent = frequencyPlotXLabels,
                YAxisLabelsParent = frequencyPlotYLabels,
                Name = "Frequency",
                MouseLocation = MouseLocation.SharpnessPlot
            };

            plotScroll.RegisterCallback<GeometryChangedEvent>(PlotScroll_GeometryChanged);
            plotScroll.contentViewport.RegisterCallback<WheelEvent>(OnWheelOverPlotScroll);
            amplitudePlotYLabels.RegisterCallback<GeometryChangedEvent, VisualElement>(YAxisLabels_GeometryChanged, _amplitudePlotInfo.Plot);
            amplitudePlotXLabels.RegisterCallback<GeometryChangedEvent>(XAxisLabels_GeometryChanged);
            frequencyPlotYLabels.RegisterCallback<GeometryChangedEvent, VisualElement>(YAxisLabels_GeometryChanged, _frequencyPlotInfo.Plot);
            frequencyPlotXLabels.RegisterCallback<GeometryChangedEvent>(XAxisLabels_GeometryChanged);
            amplitudePlotPoints.RegisterCallback<PointerEnterEvent, (PlotInfo, PlotInfo)>(Plot_PointerEntered, (_amplitudePlotInfo, _frequencyPlotInfo));
            amplitudePlotPoints.RegisterCallback<PointerMoveEvent, (PlotInfo, PlotInfo)>(Plot_PointerMoved, (_amplitudePlotInfo, _frequencyPlotInfo));
            amplitudePlotPoints.RegisterCallback<PointerOutEvent, (PlotInfo, PlotInfo)>(Plot_PointerOut, (_amplitudePlotInfo, _frequencyPlotInfo));
            amplitudePlotPoints.RegisterCallback<PointerDownEvent, (PlotInfo, PlotInfo)>(Plot_PointerDown, (_amplitudePlotInfo, _frequencyPlotInfo));
            amplitudePlotPoints.RegisterCallback<PointerUpEvent, (PlotInfo, PlotInfo)>(Plot_PointerUp, (_amplitudePlotInfo, _frequencyPlotInfo));
            amplitudePlotPoints.generateVisualContent += AmplitudePlot_GenerateVisualContent;
            frequencyPlotPoints.RegisterCallback<PointerEnterEvent, (PlotInfo, PlotInfo)>(Plot_PointerEntered, (_frequencyPlotInfo, _amplitudePlotInfo));
            frequencyPlotPoints.RegisterCallback<PointerMoveEvent, (PlotInfo, PlotInfo)>(Plot_PointerMoved, (_frequencyPlotInfo, _amplitudePlotInfo));
            frequencyPlotPoints.RegisterCallback<PointerOutEvent, (PlotInfo, PlotInfo)>(Plot_PointerOut, (_frequencyPlotInfo, _amplitudePlotInfo));
            frequencyPlotPoints.RegisterCallback<PointerDownEvent, (PlotInfo, PlotInfo)>(Plot_PointerDown, (_frequencyPlotInfo, _amplitudePlotInfo));
            frequencyPlotPoints.RegisterCallback<PointerUpEvent, (PlotInfo, PlotInfo)>(Plot_PointerUp, (_frequencyPlotInfo, _amplitudePlotInfo));
            frequencyPlotPoints.generateVisualContent += FrequencyPlot_GenerateVisualContent;

            RefreshSelectionInfo();
        }

        void ToggleSidePanel(ChangeEvent<bool> change)
        {
            var bottomPart = rootVisualElement.Q<TwoPaneSplitView>(Controls.BottomPart);
            if (change.newValue)
                bottomPart.UnCollapse();
            else
                bottomPart.CollapseChild(1);
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

        void YAxisLabels_GeometryChanged(GeometryChangedEvent geometryChangedEvent, VisualElement plot)
        {
            DrawYAxisLabels((VisualElement)geometryChangedEvent.target, plot);
        }
        
        void XAxisLabels_GeometryChanged(GeometryChangedEvent geometryChangedEvent)
        {
            DrawXAxisLabels((VisualElement)geometryChangedEvent.target);
        }

        void DrawYAxisLabels(VisualElement labelsParent, VisualElement plot)
        {
            labelsParent.Clear();
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
                float newZoom = _Zoom - Mathf.Sign(wheelEvent.delta.y) * Settings.ZoomDelta;
                newZoom = Mathf.Clamp(newZoom, 1, Settings.ZoomMax);
                contentWidth = viewportWidth * newZoom;
                _scrollOffset = posX * contentWidth - wheelEvent.localMousePosition.x;
                if (_scrollOffset == 0)
                    contentContainer.RegisterCallback<GeometryChangedEvent>(SetScrollOffset);
                UpdateZoom(newZoom);
            }
            else if (_Zoom > 1f)
            {
                _scrollOffset += contentWidth * Mathf.Sign(wheelEvent.delta.y) * Settings.ScrollDelta;
            }
            else
            {
                return;
            }

            _scrollOffset = Mathf.Clamp(_scrollOffset, 0, contentWidth - viewportWidth);
            Vector2 scrollOffset = plotScroll.scrollOffset;
            scrollOffset.x = _scrollOffset;
            plotScroll.scrollOffset = scrollOffset;

            PlotInfo plotInfo = null;
            if (_amplitudePlotInfo.MousePosition != null)
                plotInfo = _amplitudePlotInfo;
            else if (_frequencyPlotInfo.MousePosition != null)
                plotInfo = _frequencyPlotInfo;
            if (plotInfo != null)
            {
                Vector2 position = plotInfo.MousePosition.Value;
                position.x = _scrollOffset + wheelEvent.localMousePosition.x;
                HandlePlotPointerHover(plotInfo, position);
            }
        }

        void SetScrollOffset(GeometryChangedEvent geometryChangedEvent)
        {
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            plotScroll.scrollOffset = new Vector2(_scrollOffset, 0);
            plotScroll.contentContainer.UnregisterCallback<GeometryChangedEvent>(SetScrollOffset);
        }

        void OnZoomValueChanged(ChangeEvent<float> zoomChange) => UpdateZoom(zoomChange.newValue);

        void UpdateZoom(float newZoom)
        {
            _Zoom = (float)Math.Round(newZoom, 1);
            var plotScroll = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            plotScroll.contentContainer.style.width = plotScroll.worldBound.width * _Zoom;

            if (_WaveformClip != null && Mathf.Abs(_Zoom - _lastPaintedZoom) >= Settings.WaveformRepaintZoomThreshold)
            {
                RepaintAudioWaveform();
                _lastPaintedZoom = _Zoom;
            }
        }

        void OnTimeChanged(ChangeEvent<float> changeEvent)
        {
            _Time = Mathf.Clamp(changeEvent.newValue, GetMinTime(), Settings.TimeMax);
            RepaintAudioWaveform();
            DrawXAxisLabels(rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotXAxisLabels));
            DrawXAxisLabels(rootVisualElement.Q<VisualElement>(Controls.FrequencyPlotXAxisLabels));
            RepaintPoints();
        }

        void TrimTime()
        {
            _Time = GetMinTime();
        }

        void OnWaveformAssetChanged(ChangeEvent<Object> changeEvent)
        {
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

        void OnWaveformBoolChanged(ChangeEvent<bool> changeEvent) 
        {
            RepaintAudioWaveform();
        }

        void OnWaveformRenderScaleChanged(ChangeEvent<float> changeEvent)
        {
            _WaveformRenderScale = Mathf.Clamp(changeEvent.newValue,
                Settings.WaveformRenderScaleMin, Settings.WaveformRenderScaleMax);
            RepaintAudioWaveform();
        }

        void OnWaveformHeightMultiplierChanged(ChangeEvent<float> changeEvent)
        {
            _WaveformHeightMultiplier = Mathf.Clamp(changeEvent.newValue,
                Settings.WaveformHeightMultiplierMin, Settings.WaveformHeightMultiplierMax);
            RepaintAudioWaveform();
        }

        void Plot_GenerateVisualContent(MeshGenerationContext context, PlotInfo plotInfo, PlotInfo otherPlotInfo)
        {
            DrawGridLines(context, plotInfo.YAxisLabelsParent.name, true);
            DrawGridLines(context, plotInfo.XAxisLabelsParent.name, false);

            var painter = context.painter2D;
            Vector2 plotSize = context.visualElement.worldBound.size;

            if (plotInfo.MousePosition != null && _mouseState != MouseState.Unclicked && _selectingPoints)
            {
                Vector2 firstPoint = _dragStartPosition.Value;
                Vector2 secondPoint = SnapPointerPosition(plotInfo.MousePosition.Value, plotSize);
                Vector2 min = Vector2.Min(firstPoint, secondPoint);
                Vector2 max = Vector2.Max(firstPoint, secondPoint);
                firstPoint.Set(min.x, max.y);
                secondPoint.Set(max.x, min.y);
                painter.DrawPolygon(Colors.selectedPoint, true, min, firstPoint, max, secondPoint);
                painter.DrawLine(Settings.EventLineWidth, Colors.selectionRectBorder, true, LineJoin.Round,
                    min, firstPoint, max, secondPoint);
            }

            if (plotInfo.MousePosition != null && _hoverPoint != null)
            {
                Vector2 plotPoint = RealToPlotPoint(_hoverPoint, plotSize);
                painter.DrawFilledCircle(plotPoint, Settings.HoverHighlightSize, Colors.hovered);
            }

            if (_selectedPointsLocation == plotInfo.MouseLocation)
            {
                foreach (var point in _selectedPoints)
                {
                    Vector2 plotPoint = RealToPlotPoint(point, plotSize);
                    painter.DrawFilledCircle(plotPoint, Settings.SelectHighlightSize, Colors.selectedPoint);
                }
            }

            foreach (var hapticEvent in _Events)
            {
                if (hapticEvent is TransientEvent transientEvent)
                {
                    EventPoint point = plotInfo.MouseLocation == MouseLocation.IntensityPlot ? 
                        transientEvent.Intensity : transientEvent.Sharpness;
                    Vector2 plotPoint = RealToPlotPoint(point, plotSize);
                    painter.DrawFilledCircle(plotPoint, Settings.EventPointRadius, Colors.eventTransient);
                    painter.DrawLine(Settings.EventLineWidth, Colors.eventTransient, false, null, plotPoint,
                        new Vector2(plotPoint.x, plotSize.y));
                }
                else if (hapticEvent is ContinuousEvent continuousEvent)
                {
                    List<EventPoint> curve = plotInfo.MouseLocation == MouseLocation.IntensityPlot ?
                        continuousEvent.IntensityCurve : continuousEvent.SharpnessCurve;
                    var points = new Vector2[curve.Count + 2];
                    Vector2 plotPoint = RealToPlotPoint(new Vector2(continuousEvent.Time, 0), plotSize);
                    points[0] = plotPoint;
                    for (int i = 0; i < curve.Count; i++)
                    {
                        plotPoint = RealToPlotPoint(curve[i], plotSize);
                        points[i + 1] = plotPoint;
                        painter.DrawFilledCircle(plotPoint, Settings.EventPointRadius, Colors.eventContinuous);
                    }
                    plotPoint = RealToPlotPoint(new Vector2(continuousEvent.TimeMax, 0), plotSize);
                    points[^1] = plotPoint;
                    painter.DrawLine(Settings.EventLineWidth, Colors.eventContinuous, false, LineJoin.Bevel, points);
                }
            }

            if (plotInfo.MousePosition != null)
            {
                Vector2 mousePosition = plotInfo.MousePosition.Value;
                if (context.visualElement.ContainsPointWithBorders(mousePosition))
                {
                    DrawHoverGuides(context, mousePosition);
                    if (_mouseState == MouseState.Dragging && _draggedPoint == null && !_selectingPoints)
                    {
                        Vector2 firstPoint = _dragStartPosition.Value;
                        Vector2 secondPoint = mousePosition;
                        Vector2 leftPoint = PlotToRealPoint(firstPoint, plotSize);
                        Vector2 rightPoint = PlotToRealPoint(secondPoint, plotSize);
                        Color gizmoColor = AnyContinuousEventsInRange(leftPoint.x, rightPoint.x) ?
                            Colors.invalid : Colors.eventContinuousCreation;
                        painter.DrawPolygon(gizmoColor, true, new Vector2(firstPoint.x, plotSize.y),
                            firstPoint, secondPoint, new Vector2(secondPoint.x, plotSize.y));
                    }
                }
                else if (mousePosition.x > 0)
                {
                    DrawVerticalHoverGuide(context, mousePosition);
                }
            }
            else if (otherPlotInfo.MousePosition != null)
            {
                DrawVerticalHoverGuide(context, otherPlotInfo.MousePosition.Value);
            }

            if (_draggedPoint != null && _selectedPoints.Any(x => x.ParentEvent is ContinuousEvent))
            {
                Vector2[] points = new Vector2[2] { RealToPlotPoint(new Vector2(_dragMinBound, _dragValueMinBound), plotSize),
                    RealToPlotPoint(new Vector2(_dragMinBound, _dragValueMaxBound), plotSize) };
                painter.DrawLine(Settings.HoverGuideLineWidth, Colors.dragBounds, points: points);
                points[0] = RealToPlotPoint(_dragMaxBound, _dragValueMaxBound, plotSize);
                painter.DrawLine(Settings.HoverGuideLineWidth, Colors.dragBounds, points: points);
                points[1] = RealToPlotPoint(_dragMaxBound, _dragValueMinBound, plotSize);
                painter.DrawLine(Settings.HoverGuideLineWidth, Colors.dragBounds, points: points);
                points[0] = RealToPlotPoint(_dragMinBound, _dragValueMinBound, plotSize);
                painter.DrawLine(Settings.HoverGuideLineWidth, Colors.dragBounds, points: points);
            }
        }

        void AmplitudePlot_GenerateVisualContent(MeshGenerationContext context)
        {
            Plot_GenerateVisualContent(context, _amplitudePlotInfo, _frequencyPlotInfo);
        }
        
        void FrequencyPlot_GenerateVisualContent(MeshGenerationContext context)
        {
            Plot_GenerateVisualContent(context, _frequencyPlotInfo, _amplitudePlotInfo);
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

        void Plot_PointerEntered(PointerEnterEvent pointerEnterEvent, (PlotInfo, PlotInfo) plotInfos)
        {
            if (plotInfos.Item2.MousePosition == null)
                HandlePlotPointerHover(plotInfos.Item1, pointerEnterEvent.localPosition);
        }

        void Plot_PointerMoved(PointerMoveEvent pointerMoveEvent, (PlotInfo, PlotInfo) plotInfos)
        {
            if (_mouseState == MouseState.Dragging && _draggedPoint != null)
            {
                Vector2 plotSize = plotInfos.Item1.Plot.worldBound.size;
                Vector2 dragStart = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                Vector2 offset = PlotToRealPoint(SnapPointerPosition(pointerMoveEvent.localPosition, plotSize), plotSize) - dragStart;
                offset.x = Mathf.Clamp(offset.x, _dragMin, _dragMax);
                offset.y = Mathf.Clamp(offset.y, _dragValueMin, _dragValueMax);
                Vector2 newDragPointPosition = dragStart + offset;
                offset = newDragPointPosition - _draggedPoint;

                PointDragMode pointDragMode = (PointDragMode)rootVisualElement.Q<RadioButtonGroup>(Controls.PointDragRadioGroup).value;
                if (pointDragMode == PointDragMode.LockTime || pointerMoveEvent.altKey)
                    offset.x = 0;
                if (pointDragMode == PointDragMode.LockValue || pointerMoveEvent.shiftKey)
                    offset.y = 0;

                foreach (var point in _selectedPoints)
                {
                    point.Time += offset.x;
                    point.Value += offset.y;

                    if (point.ParentEvent is TransientEvent te)
                    {
                        te.Intensity.Time = te.Sharpness.Time = point.Time;
                    }
                    else if (point.ParentEvent is ContinuousEvent ce)
                    {
                        if (point == ce.IntensityCurve.First() || point == ce.SharpnessCurve.First())
                            ce.IntensityCurve.First().Time = ce.SharpnessCurve.First().Time = point.Time;
                        else if (point == ce.IntensityCurve.Last() || point == ce.SharpnessCurve.Last())
                            ce.IntensityCurve.Last().Time = ce.SharpnessCurve.Last().Time = point.Time;
                    }
                }
            }
            else if (_mouseState == MouseState.Clicked)
            {
                Vector2 point = PlotToRealPoint(_dragStartPosition.Value, plotInfos.Item1.Plot.worldBound.size);
                _mouseState = TryGetContinuousEvent(point.x, out _) ? MouseState.Unclicked : MouseState.Dragging;
            }

            if (plotInfos.Item2.MousePosition == null)
                HandlePlotPointerHover(plotInfos.Item1, pointerMoveEvent.localPosition);
        }

        void Plot_PointerOut(PointerOutEvent pointerOutEvent, (PlotInfo, PlotInfo) plotInfos)
        {
            if (plotInfos.Item2.MousePosition == null)
                HandlePlotPointerHover(plotInfos.Item1, null);
        }

        void Plot_PointerDown(PointerDownEvent pointerDownEvent, (PlotInfo, PlotInfo) plotInfos)
        {
            var plot = (VisualElement)pointerDownEvent.target;
            plot.CapturePointer(0);
            _mouseState = MouseState.Clicked;
            _dragStartPosition = SnapPointerPosition(pointerDownEvent.localPosition, plot.worldBound.size);

            MouseMode mouseMode = (MouseMode)rootVisualElement.Q<RadioButtonGroup>(Controls.MouseModeRadioGroup).value;
            _selectingPoints = (mouseMode == MouseMode.Select && !pointerDownEvent.ctrlKey)
                || (mouseMode != MouseMode.Select && pointerDownEvent.ctrlKey);

            if (_selectingPoints)
                _mouseState = MouseState.Dragging;

            if (_hoverPoint != null && !_selectingPoints)
            {
                if (!_selectedPoints.Contains(_hoverPoint))
                {
                    _selectedPoints.Clear();
                    _selectedPoints.Add(_hoverPoint);
                    _selectedPointsLocation = plotInfos.Item1.MouseLocation;
                    RefreshSelectionInfo();
                }

                _mouseState = MouseState.Dragging;
                _draggedPoint = _hoverPoint;
                _dragStartPosition = RealToPlotPoint(_draggedPoint, plot.worldBound.size);
                var plotScrollContainter = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
                float plotScrollWidth = plotInfos.Item1.Plot.worldBound.width;
                _dragMin = _scrollOffset / plotScrollWidth * _Time - _selectedPoints[0].Time;
                _dragMax = (_scrollOffset + plotScrollContainter.worldBound.width) / plotScrollWidth * _Time - _selectedPoints[^1].Time;
                _dragValueMin = 1;
                _dragValueMax = 0;

                EventPoint cePoint = null;
                foreach (var point in _selectedPoints)
                {
                    if (point.ParentEvent is ContinuousEvent ce)
                    {
                        cePoint ??= point;

                        (List<EventPoint> dragCurve, List<EventPoint> otherCurve) = plotInfos.Item1.MouseLocation == MouseLocation.IntensityPlot ?
                            (ce.IntensityCurve, ce.SharpnessCurve) : (ce.SharpnessCurve, ce.IntensityCurve);
                        if (point == dragCurve[0])
                        {
                            var nextPoint = otherCurve.Find(p => p.Time > cePoint.Time);
                            if (nextPoint != null)
                                _dragMax = Mathf.Min(_dragMax, nextPoint.Time - Settings.NeighbourPointOffset - cePoint.Time);
                        }
                        else if (point == dragCurve[^1])
                        {
                            var previousPoint = otherCurve.FindLast(p => p.Time < point.Time);
                            if (previousPoint != null)
                                _dragMin = Mathf.Max(_dragMin, previousPoint.Time + Settings.NeighbourPointOffset - point.Time);
                        }
                    }

                    _dragValueMin = Mathf.Min(_dragValueMin, point.Value);
                    _dragValueMax = Mathf.Max(_dragValueMax, point.Value);
                }
                _dragValueMin = -_dragValueMin;
                _dragValueMinBound = ((Vector2)_draggedPoint).y + _dragValueMin;
                _dragValueMax = -(_dragValueMax - 1);
                _dragValueMaxBound = ((Vector2)_draggedPoint).y + _dragValueMax;

                if (cePoint != null)
                {
                    var ce = (ContinuousEvent)cePoint.ParentEvent;
                    (List<EventPoint> dragCurve, List<EventPoint> otherCurve) = plotInfos.Item1.MouseLocation == MouseLocation.IntensityPlot ?
                        (ce.IntensityCurve, ce.SharpnessCurve) : (ce.SharpnessCurve, ce.IntensityCurve);
                    if (cePoint == dragCurve[0])
                    {
                        var previousEvent = _Events.FindLast(ev => ev.Time < cePoint.Time && ev is ContinuousEvent);
                        if (previousEvent != null)
                            _dragMin = Mathf.Max(_dragMin, ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + Settings.NeighbourPointOffset - cePoint.Time);
                    }
                    else
                    {
                        var previousPoint = dragCurve.FindLast(p => p.Time < cePoint.Time);
                        if (previousPoint != null)
                            _dragMin = Mathf.Max(_dragMin, previousPoint.Time + Settings.NeighbourPointOffset - cePoint.Time);
                    }

                    for (int i = _selectedPoints.Count - 1; i >= 0; i--)
                    {
                        if (_selectedPoints[i].ParentEvent is ContinuousEvent ce2)
                        {
                            cePoint = _selectedPoints[i];
                            (dragCurve, otherCurve) = plotInfos.Item1.MouseLocation == MouseLocation.IntensityPlot ?
                                (ce2.IntensityCurve, ce2.SharpnessCurve) : (ce2.SharpnessCurve, ce2.IntensityCurve);
                            if (cePoint == dragCurve[^1])
                            {
                                var nextEvent = _Events.Find(ev => ev.Time > cePoint.Time && ev is ContinuousEvent);
                                if (nextEvent != null)
                                    _dragMax = Mathf.Min(_dragMax, nextEvent.Time - Settings.NeighbourPointOffset - cePoint.Time);
                            }
                            else
                            {
                                var nextPoint = dragCurve.Find(p => p.Time > cePoint.Time);
                                if (nextPoint != null)
                                    _dragMax = Mathf.Min(_dragMax, nextPoint.Time - Settings.NeighbourPointOffset - cePoint.Time);
                            }
                            break;
                        }
                    }

                    _dragMinBound = _draggedPoint.Time + _dragMin;
                    _dragMaxBound = _draggedPoint.Time + _dragMax;
                }
            }
        }

        void Plot_PointerUp(PointerUpEvent pointerUpEvent, (PlotInfo, PlotInfo) plotInfos)
        {
            var thisPlot = (VisualElement)pointerUpEvent.target;
            if (plotInfos.Item1.MousePosition != null && !thisPlot.ContainsPointWithBorders(plotInfos.Item1.MousePosition.Value))
            {
                HandlePlotPointerHover(plotInfos.Item1, null);

                var otherPlot = plotInfos.Item2.Plot;
                Vector2 otherPlotLocalPointerPosition = otherPlot.WorldToLocal(pointerUpEvent.position);
                if (otherPlot.ContainsPoint(otherPlotLocalPointerPosition))
                    HandlePlotPointerHover(plotInfos.Item2, otherPlotLocalPointerPosition);
            }
            else
            {
                if (pointerUpEvent.button == (int)MouseButton.LeftMouse)
                {
                    if (_selectingPoints)
                    {
                        Vector2 plotSize = thisPlot.worldBound.size;
                        Vector2 firstPoint = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                        Vector2 secondPoint = SnapPointerPosition(pointerUpEvent.localPosition, plotSize);
                        secondPoint = PlotToRealPoint(secondPoint, plotSize);
                        Vector2 min = Vector2.Min(firstPoint, secondPoint);
                        Vector2 max = Vector2.Max(firstPoint, secondPoint);

                        _selectedPoints.Clear();
                        foreach (var ev in _Events)
                        {
                            if (ev is TransientEvent te && te.Time >= min.x && te.Time <= max.x)
                            {
                                var point = plotInfos.Item1.MouseLocation == MouseLocation.IntensityPlot ? te.Intensity : te.Sharpness;
                                if (point.Value >= min.y && point.Value <= max.y)
                                    _selectedPoints.Add(point);
                            }
                            else if (ev is ContinuousEvent ce)
                            {
                                var curve = plotInfos.Item1.MouseLocation == MouseLocation.IntensityPlot ? ce.IntensityCurve : ce.SharpnessCurve;
                                foreach (var point in curve)
                                {
                                    if (point.Time >= min.x && point.Time <= max.x && point.Value >= min.y && point.Value <= max.y)
                                        _selectedPoints.Add(point);
                                }
                            }
                        }
                        _selectedPoints.Sort();
                        _selectedPointsLocation = plotInfos.Item1.MouseLocation;
                    }
                    else if (_hoverPoint == null)
                    {
                        Vector2 plotSize = thisPlot.worldBound.size;
                        if (_mouseState == MouseState.Clicked)
                        {
                            Vector2 point = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                            if (TryGetContinuousEvent(point.x, out ContinuousEvent ce))
                                ce.AddPointToCurve(point, plotInfos.Item1.MouseLocation);
                            else
                                _Events.Add(new TransientEvent(point, plotInfos.Item1.MouseLocation));
                        }
                        else if (_mouseState == MouseState.Dragging)
                        {
                            Vector2 firstPoint = PlotToRealPoint(_dragStartPosition.Value, plotSize);
                            Vector2 secondPoint = SnapPointerPosition(pointerUpEvent.localPosition, plotSize);
                            secondPoint = PlotToRealPoint(secondPoint, plotSize);
                            if (!AnyContinuousEventsInRange(firstPoint.x, secondPoint.x))
                                _Events.Add(new ContinuousEvent(firstPoint, secondPoint, plotInfos.Item1.MouseLocation));
                        }
                    }
                }
                else if (_hoverPoint != null)
                {
                    if (_selectingPoints && _mouseState != MouseState.Unclicked)
                    {
                        if (pointerUpEvent.button == (int)MouseButton.MiddleMouse)
                            SelectHoverEvent(plotInfos.Item1);
                    }
                    else
                    {
                        bool removeEvent = false;
                        if (pointerUpEvent.button == (int)MouseButton.RightMouse)
                        {
                            removeEvent = _hoverPoint.ParentEvent.ShouldRemoveEventAfterRemovingPoint(_hoverPoint, plotInfos.Item1.MouseLocation);
                            if (!removeEvent)
                            {
                                _selectedPoints.Remove(_hoverPoint);
                                _hoverPoint = null;
                            }
                        }
                        else if (pointerUpEvent.button == (int)MouseButton.MiddleMouse)
                        {
                            removeEvent = true;
                        }

                        if (removeEvent)
                        {
                            if (_hoverPoint.ParentEvent is TransientEvent)
                            {
                                _selectedPoints.Remove(_hoverPoint);
                            }
                            else if (_hoverPoint.ParentEvent is ContinuousEvent ce)
                            {
                                foreach (var point in ce.IntensityCurve)
                                    _selectedPoints.Remove(point);
                                foreach (var point in ce.SharpnessCurve)
                                    _selectedPoints.Remove(point);
                            }
                            _Events.Remove(_hoverPoint.ParentEvent);
                        }
                    }
                }
                HandlePlotPointerHover(plotInfos.Item1, pointerUpEvent.localPosition);
            }

            RefreshSelectionInfo();
            thisPlot.ReleasePointer(0);
            _dragStartPosition = null;
            _draggedPoint = null;
            _mouseState = MouseState.Unclicked;
            _selectingPoints = false;
        }

        void HandlePlotPointerHover(PlotInfo hoverPlotInfo, Vector2? pointerPosition)
        {
            RepaintPoints();
            var hoverPlotName = rootVisualElement.Q<Label>(Controls.HoverPlotName);
            var hoverTime = rootVisualElement.Q<Label>(Controls.HoverTime);
            var hoverValue = rootVisualElement.Q<Label>(Controls.HoverValue);
            if (pointerPosition != null)
            {
                Vector2 plotSize = hoverPlotInfo.PlotPoints.worldBound.size;
                _hoverPoint = GetPointOnPosition(PlotToRealPoint(pointerPosition.Value, plotSize),
                    hoverPlotInfo.MouseLocation);
                hoverPlotInfo.MousePosition = SnapPointerPosition(pointerPosition.Value, plotSize);
                hoverPlotName.text = hoverPlotInfo.Name;
                Vector2 clampedPointerPosition = PlotToRealPoint(hoverPlotInfo.MousePosition.Value, plotSize);
                clampedPointerPosition.x = Mathf.Clamp(clampedPointerPosition.x, 0, _Time);
                clampedPointerPosition.y = Mathf.Clamp01(clampedPointerPosition.y);
                hoverTime.text = clampedPointerPosition.x.ToString();
                hoverValue.text = clampedPointerPosition.y.ToString();
            }
            else
            {
                hoverPlotInfo.MousePosition = null;
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

        void RefreshSelectionInfo()
        {
            var none = rootVisualElement.Q<VisualElement>(Controls.SelectionNone);
            var single = rootVisualElement.Q<VisualElement>(Controls.SelectionSingle);
            var multi = rootVisualElement.Q<VisualElement>(Controls.SelectionMulti);

            switch (_selectedPoints.Count) 
            {
                case 0:
                    none.style.display = DisplayStyle.Flex;
                    single.style.display = DisplayStyle.None;
                    multi.style.display = DisplayStyle.None;
                    single.Q<FloatField>(Controls.SelectionSingleTime).UnregisterValueChangedCallback(SingleSelectionTimeChanged);
                    single.Q<FloatField>(Controls.SelectionSingleValue).UnregisterValueChangedCallback(SingleSelectionValueChanged);
                    break;
                case 1:
                    none.style.display = DisplayStyle.None;
                    single.style.display = DisplayStyle.Flex;
                    multi.style.display = DisplayStyle.None;
                    var singleTime = single.Q<FloatField>(Controls.SelectionSingleTime);
                    singleTime.SetValueWithoutNotify(_selectedPoints[0].Time);
                    singleTime.RegisterValueChangedCallback(SingleSelectionTimeChanged);
                    var singleValue = single.Q<FloatField>(Controls.SelectionSingleValue);
                    singleValue.SetValueWithoutNotify(_selectedPoints[0].Value);
                    singleValue.RegisterValueChangedCallback(SingleSelectionValueChanged);
                    break;
                default:
                    none.style.display = DisplayStyle.None;
                    single.style.display = DisplayStyle.None;
                    multi.style.display = DisplayStyle.Flex;
                    single.Q<FloatField>(Controls.SelectionSingleTime).UnregisterValueChangedCallback(SingleSelectionTimeChanged);
                    single.Q<FloatField>(Controls.SelectionSingleValue).UnregisterValueChangedCallback(SingleSelectionValueChanged);
                    multi.Q<Label>(Controls.SelectionMultiData).text = $"{_selectedPoints.Count} {_selectedPointsLocation} points";
                    break;
            }
        }

        void SingleSelectionTimeChanged(ChangeEvent<float> changeEvent)
        {
            EventPoint point = _selectedPoints[0];
            var plotScrollContainter = rootVisualElement.Q<ScrollView>(Controls.PlotScroll);
            float plotScrollWidth = rootVisualElement.Q<VisualElement>(Controls.AmplitudePlotPoints).worldBound.width;
            _dragMin = _scrollOffset / plotScrollWidth * _Time - point.Time;
            _dragMax = (_scrollOffset + plotScrollContainter.worldBound.width) / plotScrollWidth * _Time - point.Time;

            if (point.ParentEvent is ContinuousEvent ce)
            {
                (List<EventPoint> dragCurve, List<EventPoint> otherCurve) = _selectedPointsLocation == MouseLocation.IntensityPlot ?
                    (ce.IntensityCurve, ce.SharpnessCurve) : (ce.SharpnessCurve, ce.IntensityCurve);
                if (point == dragCurve[0])
                {
                    var nextPoint = otherCurve.Find(p => p.Time > point.Time);
                    if (nextPoint != null)
                        _dragMax = Mathf.Min(_dragMax, nextPoint.Time - Settings.NeighbourPointOffset - point.Time);
                }
                else if (point == dragCurve[^1])
                {
                    var previousPoint = otherCurve.FindLast(p => p.Time < point.Time);
                    if (previousPoint != null)
                        _dragMin = Mathf.Max(_dragMin, previousPoint.Time + Settings.NeighbourPointOffset - point.Time);
                }

                if (point == dragCurve[0])
                {
                    var previousEvent = _Events.FindLast(ev => ev.Time < point.Time && ev is ContinuousEvent);
                    if (previousEvent != null)
                        _dragMin = Mathf.Max(_dragMin, ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + Settings.NeighbourPointOffset - point.Time);
                }
                else
                {
                    var previousPoint = dragCurve.FindLast(p => p.Time < point.Time);
                    if (previousPoint != null)
                        _dragMin = Mathf.Max(_dragMin, previousPoint.Time + Settings.NeighbourPointOffset - point.Time);
                }

                if (point == dragCurve.Last())
                {
                    var nextEvent = _Events.Find(ev => ev.Time > point.Time && ev is ContinuousEvent);
                    if (nextEvent != null)
                        _dragMax = Mathf.Min(_dragMax, nextEvent.Time - Settings.NeighbourPointOffset - point.Time);
                }
                else
                {
                    var nextPoint = dragCurve.Find(p => p.Time > point.Time);
                    if (nextPoint != null)
                        _dragMax = Mathf.Min(_dragMax, nextPoint.Time - Settings.NeighbourPointOffset - point.Time);
                }
            }

            _dragMinBound = point.Time + _dragMin;
            _dragMaxBound = point.Time + _dragMax;

            float time = Mathf.Clamp(changeEvent.newValue, _dragMinBound, _dragMaxBound);
            ((FloatField)changeEvent.target).SetValueWithoutNotify(time);
            if (point.ParentEvent is TransientEvent transientEvent)
            {
                transientEvent.Sharpness.Time = transientEvent.Intensity.Time = time;
            }
            else if (point.ParentEvent is ContinuousEvent continuousEvent)
            {
                if (point.Time == continuousEvent.IntensityCurve.First().Time || point.Time == continuousEvent.SharpnessCurve.First().Time)
                    continuousEvent.IntensityCurve.First().Time = continuousEvent.SharpnessCurve.First().Time = time;
                else if (point.Time == continuousEvent.IntensityCurve.Last().Time || point.Time == continuousEvent.SharpnessCurve.Last().Time)
                    continuousEvent.IntensityCurve.Last().Time = continuousEvent.SharpnessCurve.Last().Time = time;
                else
                    point.Time = time;
            }

            RepaintPoints();
        }

        void SingleSelectionValueChanged(ChangeEvent<float> changeEvent)
        {
            float value = Mathf.Clamp01(changeEvent.newValue);
            ((FloatField)changeEvent.target).SetValueWithoutNotify(value);
            _selectedPoints[0].Value = value;
            RepaintPoints();
        }

        void SelectHoverEvent(PlotInfo currentPlotInfo)
        {
            _selectedPoints.Clear();
            if (_hoverPoint.ParentEvent is TransientEvent)
            {
                _selectedPoints.Add(_hoverPoint);
            }
            else if (_hoverPoint.ParentEvent is ContinuousEvent ce)
            {
                var curve = currentPlotInfo.MouseLocation == MouseLocation.IntensityPlot ? ce.IntensityCurve : ce.SharpnessCurve;
                _selectedPoints.AddRange(curve);
            }
            _selectedPointsLocation = currentPlotInfo.MouseLocation;
        }

        void OnImportSuccess(ImportData importData)
        {
            _Time = GetLastPointTime();
            if (!string.IsNullOrEmpty(importData.ProjectName))
                _ProjectName = importData.ProjectName;
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

        Vector2 RealToPlotPoint(float realPositionX, float realPositionY, Vector2 plotSize)
        {
            return new Vector2(realPositionX / _Time * plotSize.x,
                plotSize.y - (realPositionY * plotSize.y));
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

        bool TryGetContinuousEventsInRange(float time1, float time2, out List<ContinuousEvent> continuousEvents)
        {
            if (time1 > time2)
                (time1, time2) = (time2, time1);
            continuousEvents = new List<ContinuousEvent>();
            foreach (var ev in _Events) 
            {
                if (ev is ContinuousEvent ce && time1 <= ce.IntensityCurve[^1].Time && time2 >= ce.Time)
                    continuousEvents.Add(ce);
            }
            return continuousEvents.Count > 0;
        }

        bool AnyContinuousEventsInRange(float time1, float time2)
        {
            if (time1 > time2)
                (time1, time2) = (time2, time1);
            foreach (var ev in _Events)
            {
                if (ev is ContinuousEvent ce && time2 >= ce.Time && time1 <= ce.IntensityCurve[^1].Time)
                    return true;
            }
            return false;
        }

        EventPoint GetPointOnPosition(Vector2 plotPosition, MouseLocation plot)
        {
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

    internal enum PointDragMode 
    {
        FreeMove = 0,
        LockTime = 1,
        LockValue = 2
    }

    internal enum MouseMode
    {
        None = -1,
        [InspectorName("Add/Remove")] AddRemove = 0,
        Select = 1
    }

    public enum FileFormat
    {
        AHAP = 0,
        Haptic = 1
    }

    internal class PlotInfo
    {
        public VisualElement Plot;
        public VisualElement PlotPoints;
        public VisualElement PlotWaveform;
        public VisualElement XAxisLabelsParent;
        public VisualElement YAxisLabelsParent;
        public Vector2? MousePosition = null;
        public string Name;
        public MouseLocation MouseLocation;
    }
}
