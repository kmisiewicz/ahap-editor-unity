using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow
    {
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
        const float NEIGHBOURING_POINT_OFFSET = 0.001f;
        const float MIN_WAVEFORM_RENDER_SCALE = 0.1f;
        const float MAX_WAVEFORM_RENDER_SCALE = 2f;
        const float CUSTOM_LABEL_WIDTH_OFFSET = 3;
        const float PLOT_AREA_BASE_WIDTH = 0.80f;
        const float PLOT_AREA_MIN_WIDTH = 0.55f;
        const float PLOT_AREA_MAX_WIDTH = 0.86f;
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
            public static readonly Color draggedPoint = new(1f, 1f, 0f, 0.3f);
            public static readonly Color dragBounds = new(0.85f, 1f, 0f);
            public static readonly Color selectedPoint = new(0.5f, 1f, 1f, 0.3f);
            public static readonly Color hoverGuides = new(0.7f, 0f, 0f);
            public static readonly Color selectionRectBorder = new(0, 0.9f, 0.9f);
        }

        class Content
        {
            public const string WINDOW_NAME = "AHAP Editor";
            public const string WINDOW_ICON_NAME = "d_HoloLensInputModule Icon";
            public const string PLAY_ICON_NAME = "d_PlayButton";

            public static readonly GUIContent debugModeLabel = new("Debug Mode");
            public static readonly GUIContent debugLabel = new("Debug");
            public static readonly GUIContent logRectsLabel = new("Log rects");
            public static readonly GUIContent drawRectsLabel = new("Draw Rects");
            public static readonly GUIContent fileLabel = new("File");
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
        }

        class Styles
        {
            public static readonly GUIStyle plotTitleStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            public static readonly GUIStyle yAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            public static readonly GUIStyle xAxisLabelStyle = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
