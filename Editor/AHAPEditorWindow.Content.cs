using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow
    {
        // EditorPrefs keys
        const string SAFE_MODE_KEY = "CHROMA_HAPTICS_EDITOR_SAFE_MODE";
        const string ADVANCED_PANEL_KEY = "CHROMA_HAPTICS_EDITOR_ADVANCED_PANEL";
        const string DEBUG_MODE_KEY = "CHROMA_HAPTICS_EDITOR_DEBUG_MODE";

        // Const settings
        const float MIN_TIME = 0.1f;
        const float MAX_TIME = 30f;
        const float HOVER_OFFSET = 5f;
        const float ZOOM_INCREMENT = 0.1f;
        const float MAX_ZOOM = 7f;
        const float SCROLL_INCREMENT = 0.05f;
        const float HOVER_HIGHLIGHT_SIZE = 9f;
        const float DRAG_HIGHLIGHT_SIZE = 15f;
        const float SELECT_HIGHLIGHT_SIZE = 12f;
        const float HOVER_DOT_SIZE = 3f;
        const float PLOT_EVENT_POINT_SIZE = 5f;
        const float PLOT_EVENT_LINE_WIDTH = 4f;
        const float PLOT_BORDER_WIDTH = 5f;
        const float NEIGHBOUR_POINT_OFFSET = 0.001f;
        const float MIN_WAVEFORM_RENDER_SCALE = 0.1f;
        const float MAX_WAVEFORM_RENDER_SCALE = 2f;
        const float LABEL_WIDTH_OFFSET = 3;
        const float PLOT_AREA_BASE_WIDTH = 0.80f;
        const float PLOT_AREA_MIN_WIDTH = 0.55f;
        const float PLOT_AREA_MAX_WIDTH = 0.86f;
        const float TOP_BAR_OPTIONS_SIZE_FACTOR = 0.12f * 16 / 9;

        static readonly Vector3 POINT_NORMAL = new(0, 0, 1);
        static readonly Vector2 CONTENT_MARGIN = new(3, 2);

        internal class Colors
        {
            public static readonly Color plotBorder = Color.white;
            public static readonly Color plotGrid = Color.gray;
            public static readonly Color waveform = new(0.42f, 1f, 0f, 0.2f);
            public static readonly Color waveformBg = Color.clear;
            public static readonly Color eventTransient = new(0.22f, 0.6f, 1f);
            public static readonly Color eventContinuous = new(1f, 0.6f, 0.2f);
            public static readonly Color eventContinuousCreation = new(1f, 0.6f, 0.2f, 0.5f);
            public static readonly Color hoverPoint = new(0.8f, 0.8f, 0.8f, 0.2f);
            public static readonly Color draggedPoint = new(1f, 1f, 0f, 0.3f);
            public static readonly Color dragBounds = new(0.85f, 1f, 0f);
            public static readonly Color selectedPoint = new(0.5f, 1f, 1f, 0.3f);
            public static readonly Color hoverGuides = new(0.7f, 0f, 0f);
            public static readonly Color selectionRectBorder = new(0, 0.9f, 0.9f);
        }

        internal class Content
        {
            public const string WINDOW_NAME = "AHAP Editor";
            public const string WINDOW_ICON_NAME = "d_HoloLensInputModule Icon";
            public const string PLAY_ICON_NAME = "d_PlayButton";
            public const string STOP_ICON_NAME = "d_PreMatQuad";

            public static readonly GUIContent safeModeLabel = new("Safe Mode");
            public static readonly GUIContent debugModeLabel = new("Debug Mode");
            public static readonly GUIContent debugLabel = new("Debug");
            public static readonly GUIContent logRectsLabel = new("Log Rects");
            public static readonly GUIContent drawRectsLabel = new("Draw Rects");
            public static readonly GUIContent fileLabel = new("File");
            public static readonly GUIContent assetLabel = new("Asset",
                "Supports AHAP and Haptic JSON or HapticClip (Lofelt's NiceVibrations) asset.");
            public static readonly GUIContent importLabel = new("Import");
            public static readonly GUIContent saveLabel = new("Save");
            public static readonly GUIContent waveformSectionLabel = new("Reference waveform");
            public static readonly GUIContent waveformVisibleLabel = new("Visible");
            public static readonly GUIContent normalizeLabel = new("Normalize");
            public static readonly GUIContent renderScaleLabel = new("Render scale",
                "Lower scale will reduce quality but improve render time.");
            public static readonly GUIContent projectNameLabel = new("Project name",
                "Name that will be saved in project's metadata.");
            public static readonly GUIContent plotViewLabel = new("Plot view");
            public static readonly GUIContent timeLabel = new("Time");
            public static readonly GUIContent zoomLabel = new("Zoom");
            public static readonly GUIContent zoomResetLabel = new("Reset zoom");
            public static readonly GUIContent clearLabel = new("Clear");
            public static readonly GUIContent trimButtonLabel = new("Trim",
                "Trims time to last point or audio clip duration.");
            public static readonly GUIContent pointEditingLabel = new("Point editing");
            public static readonly GUIContent advancedPanelLabel = new("Advanced panel");
            public static readonly GUIContent snappingLabel = new("Snapping");
            public static readonly GUIContent yAxisDummyLabel = new("#.##");
            public static readonly GUIContent xAxisDummyLabel = new("##.###");
            public static readonly GUIContent intensityLabel = new("Intensity");
            public static readonly GUIContent sharpnessLabel = new("Sharpness");
            public static readonly GUIContent selectionLabel = new("Selection");
            public static readonly GUIContent hoverInfoLabel = new("Hover info");
            public static readonly GUIContent plotLabel = new("Plot");
            public static readonly GUIContent valueLabel = new("Value");
            public static readonly GUIContent resetLabel = new("Reset");
            public static readonly GUIContent pointDragLabel = new("Point Drag Mode");
            public static readonly GUIContent mouseOptionsLabel = new("Mouse Options");
            public static readonly GUIContent dataFormatLabel = new("Data Format");
            public static readonly GUIContent fileFormatLabel = new("File Format");
            public static readonly GUIContent overwriteLabel = new("Overwrite");
            public static readonly GUIContent cancelLabel = new("Cancel");
            public static readonly GUIContent saveAsJsonLabel = new("Use .json Extension");
            public static readonly GUIContent generateLabel = new("Generate");
            public static readonly GUIContent audioAnalysisLabel = new("Audio analysis",
                "These algorithms can help you with haptics generation. " +
                "Adjust the results yourself to achieve desired results.");
            public static readonly GUIContent genTransChunkSizeLabel = new("Chunk Size",
                "Audio samples will be split in chunks for calculations. " +
                "Lower value will give more detail in time domain.");
            public static readonly GUIContent genTransSensitivityLabel = new("Sensitivity",
                "Spectral flux sample must be at least this times bigger than the average.");
            public static readonly GUIContent genTransRmsThresholdLabel = new("RMS Threshold", 
                "Only peaks above this RMS threshold will be kept.");
            public static readonly GUIContent genContFilterLabel = new("Filter", 
                "Frequency range for sharpness/frequency envelope generation.");
            public static readonly GUIContent genContSimplificationLabel = new("Tolerance",
                "This value is used to evaluate which points should be removed from the line. " +
                "A higher value results in a simpler line (less points). " +
                "A positive value close to zero results in a line with little to no reduction. " +
                "A value of zero or less has no effect. Values below 0.1 recommended.");
            public static readonly GUIContent genContRmsChunkSizeLabel = new("RMS Chunk",
                "Audio samples will be split in chunks for calculations. " +
                "Lower value will give more detail in time domain.");
            public static readonly GUIContent genContFftChunkSizeLabel = new("FFT Chunk",
                "Audio samples will be split in chunks for calculations. " +
                "Lower value will give more detail in time domain but less frequency bins to work with." +
                "Must be power of 2.");
            public static readonly GUIContent genContNormalizeLabel = new("Normalize",
                "Normalize audio clip before further calculations.");
            public static readonly GUIContent genContLerpToRmsLabel = new("Lerp to RMS",
                "Interpolate between calculated value and RMS of each audio chunk.");
            public static readonly GUIContent genContMultByRmsLabel = new("Multiply by RMS",
                "Multiplies calculated frequency by RMS of each audio chunk.");
            public static readonly GUIContent generalParamsLabel = new("General parameters");
            public static readonly GUIContent intensityGenParamsLabel = new("Intensity generation");
            public static readonly GUIContent frequencyGenParamsLabel = new("Frequency generation");
        }

        internal class Styles
        {
            public static readonly GUIStyle plotTitleStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            public static readonly GUIStyle yAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            public static readonly GUIStyle xAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }

        internal enum SnapMode
        {
            None = 0,
            [InspectorName("0.1")] Tenth = 1,
            [InspectorName("0.01")] Hundredth = 2,
            [InspectorName("0.001")] Thousandth = 3
        }

        internal enum PointDragMode { FreeMove = 0, LockTime = 1, LockValue = 2 }

        enum MouseButton { Left = 0, Right = 1, Middle = 2 }

        enum MouseMode 
        { 
            None = -1 ,
            [InspectorName("Add/Remove")] AddRemove = 0,
            Select = 1
        }

        enum DataFormat 
        { 
            Linear = 0,
            [InspectorName("Power of 2")] Squared = 1,
            [InspectorName("Power of 2.28")] Power2_28 = 2,
        }

        enum FileFormat { AHAP = 0, Haptic = 1 }
    }
}
