using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    internal enum MouseState { Unclicked = 0, MouseDown = 1, MouseDrag = 2 }

    internal enum MouseLocation { Outside = 0, IntensityPlot = 1, SharpnessPlot = 2 }

    #region Editor event data

    internal class EventPoint
    {
        public float Time;
        public float Value;

        public EventPoint(float time, float value)
        {
            Time = time;
            Value = value;
        }

        public static implicit operator EventPoint(Vector2 v2) => new EventPoint(v2.x, v2.y);

        public static implicit operator Vector2(EventPoint point) => new Vector2(point.Time, point.Value);
    }

    internal abstract class VibrationEvent
    {
        public float Time;

        public abstract bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint);

        public abstract bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location);

        public abstract List<Pattern> ToPatterns();
    }

    internal class TransientEvent : VibrationEvent
    {
        public EventPoint Intensity;
        public EventPoint Sharpness;

        public TransientEvent(float time, float intensity, float sharpness)
        {
            Time = time;
            Intensity = new EventPoint(time, intensity);
            Sharpness = new EventPoint(time, sharpness);
        }

        public override bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint)
        {
            eventPoint = null;
            Rect offsetRect = new Rect(point - offset, offset * 2);
            if (location == MouseLocation.IntensityPlot && offsetRect.Contains(Intensity))
            {
                eventPoint = Intensity;
                return true;
            }
            else if (location == MouseLocation.SharpnessPlot && offsetRect.Contains(Sharpness))
            {
                eventPoint = Sharpness;
                return true;
            }
            return false;
        }

        public override bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location) => true;

        public override List<Pattern> ToPatterns()
        {
            var e = new Event(Time, AHAPFile.EVENT_TRANSIENT, null, Intensity.Value, Sharpness.Value);
            return new List<Pattern>() { new Pattern(e, null) };
        }
    }

    internal class ContinuousEvent : VibrationEvent
    {
        public List<EventPoint> IntensityCurve;
        public List<EventPoint> SharpnessCurve;

        public ContinuousEvent() { }

        public ContinuousEvent(Vector2 time, Vector2 intensity, Vector2 sharpness)
        {
            Time = time.x;
            IntensityCurve = new List<EventPoint>() { new EventPoint(time.x, intensity.x), new EventPoint(time.y, intensity.y) };
            SharpnessCurve = new List<EventPoint>() { new EventPoint(time.x, sharpness.x), new EventPoint(time.y, sharpness.y) };
        }

        public override bool IsOnPointInEvent(Vector2 point, Vector2 offset, MouseLocation location, out EventPoint eventPoint)
        {
            eventPoint = null;
            Rect offsetRect = new Rect(point - offset, offset * 2);
            List<EventPoint> curve = location == MouseLocation.IntensityPlot ? IntensityCurve : SharpnessCurve;
            foreach (EventPoint ep in curve)
            {
                if (offsetRect.Contains(ep))
                {
                    eventPoint = ep;
                    return true;
                }
            }
            return false;
        }

        public override bool ShouldRemoveEventAfterRemovingPoint(EventPoint pointToRemove, MouseLocation location)
        {
            if (location == MouseLocation.IntensityPlot)
            {
                if (IntensityCurve.Count > 2 && (pointToRemove == IntensityCurve.First() || pointToRemove == IntensityCurve.Last()))
                    return false;
                IntensityCurve.Remove(pointToRemove);
                return IntensityCurve.Count < 2;
            }
            else if (location == MouseLocation.SharpnessPlot)
            {
                if (SharpnessCurve.Count > 2 && (pointToRemove == SharpnessCurve.First() || pointToRemove == SharpnessCurve.Last()))
                    return false;
                SharpnessCurve.Remove(pointToRemove);
                return SharpnessCurve.Count < 2;
            }
            return false;
        }

        public override List<Pattern> ToPatterns()
        {
            var list = new List<Pattern>();

            // Event
            var e = new Event(Time, AHAPFile.EVENT_CONTINUOUS, IntensityCurve.Last().Time - Time, 1, 0);
            list.Add(new Pattern(e, null));

            // Parameter curves
            var curve = new ParameterCurve(Time, AHAPFile.CURVE_INTENSITY);
            foreach (var intensityPoint in IntensityCurve)
            {
                curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(intensityPoint.Time, intensityPoint.Value));
                if (curve.ParameterCurveControlPoints.Count >= 16)
                {
                    list.Add(new Pattern(null, curve));
                    curve = new ParameterCurve(intensityPoint.Time, AHAPFile.CURVE_INTENSITY);
                    curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(intensityPoint.Time, intensityPoint.Value));
                }
            }
            if (list.Count == 1 || (list.Count > 1 && curve.ParameterCurveControlPoints.Count >= 1))
                list.Add(new Pattern(null, curve));

            int count = list.Count;
            curve = new ParameterCurve(Time, AHAPFile.CURVE_SHARPNESS);
            foreach (var sharpnessPoint in SharpnessCurve)
            {
                curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(sharpnessPoint.Time, sharpnessPoint.Value));
                if (curve.ParameterCurveControlPoints.Count >= 16)
                {
                    list.Add(new Pattern(null, curve));
                    curve = new ParameterCurve(sharpnessPoint.Time, AHAPFile.CURVE_SHARPNESS);
                    curve.ParameterCurveControlPoints.Add(new ParameterCurveControlPoint(sharpnessPoint.Time, sharpnessPoint.Value));
                }
            }
            if (list.Count == count || (list.Count > count && curve.ParameterCurveControlPoints.Count >= 0))
                list.Add(new Pattern(null, curve));

            return list;
        }
    }

    #endregion

    public class AHAPEditorWindow : EditorWindow
	{
        private enum SnapMode { None = 0, [InspectorName("0.1")]Tenth = 1, [InspectorName("0.01")] Hundredth = 2, [InspectorName("0.001")] Thousandth = 3 }

        const float hoverOffset = 5;

        Vector2 scrollPosition = Vector2.zero;
		float zoom = 1f;
        float time = 1f;
        Vector2 plotSize = Vector2.one;
        float visiblePlotWidth = 1f;
        int currentEditMode = 0;
        string[] editModeNames = new string[] { "Free Move", "Lock Time", "Lock Value" };
        List<VibrationEvent> events = new List<VibrationEvent>();
        MouseState mouseState = MouseState.Unclicked;
        MouseLocation mouseLocation = MouseLocation.Outside;
        MouseLocation mouseClickLocation = MouseLocation.Outside;
        SnapMode snapMode = SnapMode.None;
        Vector2 mouseClickPlotPosition;
        float continuousEventWindowPos;
        EventPoint hoverPoint = null;
        VibrationEvent hoverPointEvent = null;
        EventPoint draggedPoint = null;
        VibrationEvent draggedPointEvent = null;
        float dragMin, dragMax;
        TextAsset ahapFile;


        [MenuItem("Window/AHAP Editor")]
        public static void OpenWindow()
        {
            AHAPEditorWindow window = GetWindow<AHAPEditorWindow>("AHAP Editor");
            var c = EditorGUIUtility.IconContent("d_HoloLensInputModule Icon", "AHAP Editor");
            c.text = "AHAP Editor";
            window.titleContent = c;
        }

        private void OnGUI()
		{
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float lineSpacing = EditorGUIUtility.standardVerticalSpacing;
            float lineWithSpacing = lineHeight + lineSpacing;
            float lineHalfHeight = lineHeight / 2f;

            #region Zoom and scrolling handling (mouse wheel)

            var currentEvent = UnityEngine.Event.current;
            if (currentEvent.type == EventType.ScrollWheel)
            {
                if (currentEvent.control)
                {
                    zoom += currentEvent.delta.y < 0 ? 0.1f : -0.1f;
                    zoom = Mathf.Clamp(zoom, 1, 7);
                }
                else if (zoom > 1f)
                {
                    var scrollPositionX = scrollPosition.x;
                    scrollPositionX += plotSize.x * (currentEvent.delta.y > 0 ? 0.05f : -0.05f);
                    scrollPositionX = Mathf.Clamp(scrollPositionX, 0, plotSize.x - visiblePlotWidth);
                    scrollPosition = new Vector2(scrollPositionX, 0);
                }
                currentEvent.Use();
            }

            #endregion

            #region Top UI

            int toplinesTaken = 0;
            GUILayout.BeginHorizontal();
            ahapFile = EditorGUILayout.ObjectField("AHAP File", ahapFile, typeof(TextAsset), false) as TextAsset;
            if (ahapFile == null) GUI.enabled = false;
            if (GUILayout.Button("Import", GUILayout.Width(70)))
                HandleImport();
            GUI.enabled = true;
            GUILayout.Space(50);
            time = Mathf.Max(Mathf.Max(EditorGUILayout.FloatField("Time", time), GetLastPointTime()), 0.1f);
            GUILayout.EndHorizontal();
            toplinesTaken++;

            Rect refRect = EditorGUILayout.GetControlRect();
            Rect selModeRect = EditorGUI.PrefixLabel(refRect, new GUIContent("Edit Mode"));
            currentEditMode = GUI.SelectionGrid(selModeRect, currentEditMode, editModeNames, editModeNames.Length);
            toplinesTaken++;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
                HandleSaving();
            if (GUILayout.Button("Clear"))
            {
                if (events != null)
                    events.Clear();
                else
                    events = new List<VibrationEvent>();
                time = 1;
            }
            if (GUILayout.Button("Reset zoom"))
                zoom = 1;
            snapMode = (SnapMode)EditorGUILayout.EnumPopup("Snapping", snapMode);
            GUILayout.EndHorizontal();
            toplinesTaken++;

            #endregion

            Rect bottomLine = new Rect(refRect);
            bottomLine.position = new Vector2(refRect.x, position.height - lineWithSpacing);
            int bottomLinesTaken = 1;

            // Plot area            	
			float topOffset = toplinesTaken * lineWithSpacing + lineSpacing;			
			float bottomOffset = bottomLinesTaken * lineWithSpacing + lineSpacing;
            Rect plotArea = position;
            plotArea.position = new Vector2(refRect.x, topOffset);
			plotArea.width -= plotArea.x * 2 + 10;
			plotArea.height -= plotArea.y + bottomOffset;
			Rect intensityPlotRect = new Rect(plotArea);
			GUIStyle plotTitleStyle = new GUIStyle(GUI.skin.label);
			plotTitleStyle.alignment = TextAnchor.UpperCenter;
			plotTitleStyle.fontStyle = FontStyle.Bold;
			GUI.Label(intensityPlotRect, "Intensity", plotTitleStyle);
			intensityPlotRect.height -= lineHeight;
			intensityPlotRect.height /= 2;
			Rect sharpnessPlotRect = new Rect(intensityPlotRect);
			sharpnessPlotRect.position += new Vector2(0, sharpnessPlotRect.height);
			GUI.Label(sharpnessPlotRect, "Sharpness", plotTitleStyle);
			var plotOffset = new Vector2(35, lineWithSpacing);
            intensityPlotRect.position += plotOffset;
			intensityPlotRect.width -= plotOffset.x;
			intensityPlotRect.height -= plotOffset.y + lineWithSpacing;
			sharpnessPlotRect.position += plotOffset;
			sharpnessPlotRect.width -= plotOffset.x;
			sharpnessPlotRect.height -= plotOffset.y + lineWithSpacing;
            visiblePlotWidth = intensityPlotRect.width;

            // Y axis labels and horizontal lines
            int yAxisLabelCount = Mathf.Clamp((int)(intensityPlotRect.height / (lineHeight * 1.5)), 2, 11);
            if (yAxisLabelCount < 11)
            {
                if (yAxisLabelCount >= 6) yAxisLabelCount = 6;
                else if (yAxisLabelCount >= 3 && yAxisLabelCount < 5) yAxisLabelCount = 3;
            }
            float verticalAxisLabelInterval = 1f / (yAxisLabelCount - 1);
            Rect rect = new Rect(refRect);
            rect.width = 30;
            rect.position = new Vector2(rect.position.x, intensityPlotRect.position.y - (lineHeight / 2f));
            Rect rect2 = new Rect(rect);
            rect2.position = new Vector2(rect2.position.x, sharpnessPlotRect.position.y - (lineHeight / 2f));
            Vector2 yAxisLabelOffset = new Vector2(0, intensityPlotRect.height / (yAxisLabelCount - 1));
            GUIStyle yAxisLabelStyle = new GUIStyle(GUI.skin.label);
            yAxisLabelStyle.alignment = TextAnchor.MiddleRight;
            Handles.color = Color.gray;
            for (int i = 0; i < yAxisLabelCount; i++)
            {
                string label = (1 - i * verticalAxisLabelInterval).ToString("0.##");

                GUI.Label(rect, label, yAxisLabelStyle);
                Handles.DrawLine(new Vector3(intensityPlotRect.x, rect.y + lineHalfHeight),
                    new Vector3(intensityPlotRect.x + intensityPlotRect.width, rect.y + lineHalfHeight));
                rect.position += yAxisLabelOffset;

                GUI.Label(rect2, label, yAxisLabelStyle);
                Handles.DrawLine(new Vector3(sharpnessPlotRect.x, rect2.y + lineHalfHeight),
                    new Vector3(sharpnessPlotRect.x + sharpnessPlotRect.width, rect2.y + lineHalfHeight));
                rect2.position += yAxisLabelOffset;
            }

            Rect scrollArea = new Rect(plotArea);
            scrollArea.position += plotOffset;
            scrollArea.width -= plotOffset.x;
            scrollArea.height -= plotOffset.y;
            GUIStyle xAxisLabelStyle = new GUIStyle(GUI.skin.label);
            xAxisLabelStyle.alignment = TextAnchor.UpperCenter;

            #region Plot scroll

            plotSize.x = scrollArea.width * zoom;
            plotSize.y = intensityPlotRect.height;
            scrollPosition = GUI.BeginScrollView(scrollArea, scrollPosition, new Rect(0, 0, plotSize.x, scrollArea.height), true, false, GUI.skin.horizontalScrollbar, GUIStyle.none);

            float sharpnessPlotHeightOffset = sharpnessPlotRect.y - intensityPlotRect.y;
            int xAxisLabelCount = (int)(plotSize.x / 75);
            var xAxisLabelOffset = new Vector2(plotSize.x / (float)xAxisLabelCount, 0);
            Rect xAxisLabelRect = new Rect(0, intensityPlotRect.height + lineSpacing, 40, lineHeight);
            Rect xAxisLabelRect2 = new Rect(0, sharpnessPlotHeightOffset + sharpnessPlotRect.height + lineSpacing, 40, lineHeight);
            
            var xAxisLabelMiddleOffset = new Vector2(20, 0);
            GUI.Label(xAxisLabelRect, "0");
            xAxisLabelRect.position += xAxisLabelOffset - xAxisLabelMiddleOffset;
            GUI.Label(xAxisLabelRect2, "0");
            xAxisLabelRect2.position += xAxisLabelOffset - xAxisLabelMiddleOffset;
            for (int i = 1; i < xAxisLabelCount; i++)
            {
                string label = (i * time / xAxisLabelCount).ToString("#0.###");

                GUI.Label(xAxisLabelRect, label, xAxisLabelStyle);
                Handles.DrawLine(new Vector3(i * xAxisLabelOffset.x, 0),
                    new Vector3(i * xAxisLabelOffset.x, intensityPlotRect.height));
                xAxisLabelRect.position += xAxisLabelOffset;

                GUI.Label(xAxisLabelRect2, label, xAxisLabelStyle);
                Handles.DrawLine(new Vector3(i * xAxisLabelOffset.x, sharpnessPlotHeightOffset),
                    new Vector3(i * xAxisLabelOffset.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height));
                xAxisLabelRect2.position += xAxisLabelOffset;
            }
            xAxisLabelRect.position -= xAxisLabelMiddleOffset;
            xAxisLabelRect2.position -= xAxisLabelMiddleOffset;
            xAxisLabelStyle.alignment = TextAnchor.UpperRight;
            GUI.Label(xAxisLabelRect, time.ToString("#0.##"), xAxisLabelStyle);
            GUI.Label(xAxisLabelRect2, time.ToString("#0.##"), xAxisLabelStyle);
                        
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(5f, new Vector3(1, 1), new Vector3(plotSize.x - 1, 1), 
                new Vector3(plotSize.x - 1, intensityPlotRect.height - 1), new Vector3(1, intensityPlotRect.height - 1), new Vector3(1, 1));
            Handles.DrawAAPolyLine(5f, new Vector3(1, sharpnessPlotHeightOffset + 1), new Vector3(plotSize.x - 1, sharpnessPlotHeightOffset + 1),
                new Vector3(plotSize.x - 1, sharpnessPlotHeightOffset + sharpnessPlotRect.height - 1), 
                new Vector3(1, sharpnessPlotHeightOffset + sharpnessPlotRect.height - 1), new Vector3(1, sharpnessPlotHeightOffset + 1));

            foreach(var vibraEvent in events)
            {
                if (vibraEvent is TransientEvent transientEvent)
                {
                    Handles.color = new Color(0.22f, 0.6f, 1f);

                    Vector3 intensityPoint = new Vector3((float)(transientEvent.Time / time * plotSize.x), (float)(intensityPlotRect.height - transientEvent.Intensity.Value * intensityPlotRect.height), 0);
                    Handles.DrawSolidDisc(intensityPoint, new Vector3(0, 0, 1), 5.0f);
                    Handles.DrawAAPolyLine(4f, intensityPoint, new Vector3(intensityPoint.x, intensityPlotRect.height));

                    Vector3 sharpnessPoint = new Vector3(intensityPoint.x, (float)(sharpnessPlotHeightOffset + sharpnessPlotRect.height - transientEvent.Sharpness.Value * sharpnessPlotRect.height), 0);
                    Handles.DrawSolidDisc(sharpnessPoint, new Vector3(0, 0, 1), 5.0f);
                    Handles.DrawAAPolyLine(4f, sharpnessPoint, new Vector3(sharpnessPoint.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height));
                }
                else if (vibraEvent is ContinuousEvent continuousEvent)
                {
                    Handles.color = new Color(1f, 0.6f, 0.2f);

                    List<Vector3> points = new List<Vector3>();
                    points.Add(new Vector3(continuousEvent.IntensityCurve[0].Time / time * plotSize.x, intensityPlotRect.height, 0));
                    for (int i = 0; i < continuousEvent.IntensityCurve.Count; i++)
                    {
                        EventPoint point = continuousEvent.IntensityCurve[i];
                        Vector3 intensityPoint = new Vector3(point.Time / time * plotSize.x, intensityPlotRect.height - point.Value * intensityPlotRect.height, 0);
                        points.Add(intensityPoint);
                        Handles.DrawSolidDisc(intensityPoint, new Vector3(0, 0, 1), 5.0f);
                    }
                    points.Add(new Vector3(continuousEvent.IntensityCurve.Last().Time / time * plotSize.x, intensityPlotRect.height, 0));
                    Handles.DrawAAPolyLine(4f, points.ToArray());

                    points.Clear();
                    points.Add(new Vector3(continuousEvent.SharpnessCurve[0].Time / time * plotSize.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height, 0));
                    for (int i = 0; i < continuousEvent.SharpnessCurve.Count; i++)
                    {
                        EventPoint point = continuousEvent.SharpnessCurve[i];
                        Vector3 sharpnessPoint = new Vector3(point.Time / time * plotSize.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height - point.Value * sharpnessPlotRect.height, 0);
                        points.Add(sharpnessPoint);
                        Handles.DrawSolidDisc(sharpnessPoint, new Vector3(0, 0, 1), 5.0f);
                    }
                    points.Add(new Vector3(continuousEvent.IntensityCurve.Last().Time / time * plotSize.x, sharpnessPlotHeightOffset + sharpnessPlotRect.height, 0));
                    Handles.DrawAAPolyLine(4f, points.ToArray());
                }
            }

            GUI.EndScrollView();

            #endregion

            #region Mouse location

            Handles.color = new Color(0.7f, 0, 0);
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
                Handles.DrawSolidDisc(new Vector3(mousePosition.x, mousePosition.y, 0), new Vector3(0, 0, 1), 3.0f);
                float x = (scrollPosition.x + plotRectMousePosition.x) / plotSize.x * time;
                float y = (intensityPlotRect.height - plotRectMousePosition.y) / intensityPlotRect.height;
                realPlotPosition = new Vector2(x, y);
                if (snapMode != SnapMode.None)
                {
                    int decimals = 1;
                    switch (snapMode)
                    {
                        case SnapMode.Tenth:
                            decimals = 1;
                            break;
                        case SnapMode.Hundredth:
                            decimals = 2;
                            break;
                        case SnapMode.Thousandth:
                            decimals = 3;
                            break;
                    }
                    x = (float)Math.Round(x, decimals);
                    y = (float)Math.Round(y, decimals);
                }
                plotPosition = new Vector2(x, y);
                GUI.Label(bottomLine, $"{(mouseLocation == MouseLocation.IntensityPlot ? "Intensity" : "Sharpness")}: x={x}, y={y}");
            }

            #endregion

            #region Highlight hover point

            if (mouseLocation != MouseLocation.Outside)
            {
                hoverPoint = draggedPoint ?? GetEventPointOnPosition(realPlotPosition, mouseLocation, out hoverPointEvent);
                if (hoverPoint != null)
                {
                    Handles.color = new Color(0.8f, 0.8f, 0.8f, 0.2f);
                    if (mouseLocation == MouseLocation.IntensityPlot)
                    {
                        Vector3 intensityPoint = new Vector3(intensityPlotRect.x + hoverPoint.Time / time * plotSize.x - scrollPosition.x, 
                            intensityPlotRect.y + intensityPlotRect.height - hoverPoint.Value * intensityPlotRect.height, 0);
                        Handles.DrawSolidDisc(intensityPoint, new Vector3(0, 0, 1), 10.0f);
                    }
                    else if (mouseLocation == MouseLocation.SharpnessPlot)
                    {
                        Vector3 sharpnessPoint = new Vector3(sharpnessPlotRect.x + hoverPoint.Time / time * plotSize.x - scrollPosition.x,
                            sharpnessPlotRect.y + sharpnessPlotRect.height - hoverPoint.Value * sharpnessPlotRect.height, 0);
                        Handles.DrawSolidDisc(sharpnessPoint, new Vector3(0, 0, 1), 10.0f);
                    }
                }
            }

            #endregion

            #region Mouse click

            if (currentEvent.button == 0)
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
                        if (GetContinuousEventIfBetween(plotPosition.x) != null)
                            mouseState = MouseState.Unclicked;
                        else
                            mouseState = MouseState.MouseDrag;
                    }
                    else if (mouseState != MouseState.Unclicked && currentEvent.type == EventType.MouseUp)
                    {
                        if (mouseClickLocation == mouseLocation)
                        {
                            if (mouseState == MouseState.MouseDown) // Add transient event
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
                            sharpnessPlotRect.y + sharpnessPlotRect.height - mousePosition.y), new Color(1f, 0.6f, 0.2f, 0.5f));
                    }
                }
                else if (hoverPoint != null && draggedPoint == null && currentEvent.type == EventType.MouseDown)
                {
                    draggedPoint = hoverPoint;
                    draggedPointEvent = hoverPointEvent;
                    dragMin = scrollPosition.x / plotSize.x * time + 0.001f;
                    dragMax = (scrollPosition.x + visiblePlotWidth) / plotSize.x * time - 0.001f;
                    if (hoverPointEvent is ContinuousEvent continuousEvent)
                    {
                        ContinuousEvent ce = GetContinuousEventIfBetween(hoverPoint.Time);
                        if (ce != null)
                        {
                            if (mouseLocation == MouseLocation.IntensityPlot)
                            {
                                dragMin = continuousEvent.IntensityCurve.FindLast(point => point.Time < hoverPoint.Time).Time + 0.001f;
                                dragMax = continuousEvent.IntensityCurve.Find(point => point.Time > hoverPoint.Time).Time - 0.001f;
                            }
                            else if (mouseLocation == MouseLocation.SharpnessPlot)
                            {
                                dragMin = continuousEvent.SharpnessCurve.FindLast(point => point.Time < hoverPoint.Time).Time + 0.001f;
                                dragMax = continuousEvent.SharpnessCurve.Find(point => point.Time > hoverPoint.Time).Time - 0.001f;
                            }
                        }
                        else if (draggedPoint == continuousEvent.IntensityCurve.First() || draggedPoint == continuousEvent.SharpnessCurve.First())
                        {
                            dragMax = Mathf.Min(continuousEvent.IntensityCurve[1].Time,continuousEvent.SharpnessCurve[1].Time) - 0.001f;
                            var previousEvent = events.FindLast(ev => ev.Time < hoverPoint.Time && ev is ContinuousEvent);
                            if (previousEvent != null)
                                dragMin = ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + 0.001f;
                        }
                        else if (draggedPoint == continuousEvent.IntensityCurve.Last() || draggedPoint == continuousEvent.SharpnessCurve.Last())
                        {
                            dragMin = Mathf.Max(continuousEvent.IntensityCurve[continuousEvent.IntensityCurve.Count - 2].Time,
                                continuousEvent.SharpnessCurve[continuousEvent.SharpnessCurve.Count - 2].Time) + 0.001f;
                            var nextEvent = events.Find(ev => ev.Time > hoverPoint.Time && ev is ContinuousEvent);
                            if (nextEvent != null)
                                dragMax = ((ContinuousEvent)nextEvent).IntensityCurve.First().Time - 0.001f;
                        }
                    }
                    if (currentEditMode == 1)
                        dragMin = dragMax = draggedPoint.Time;

                    mouseClickLocation = mouseLocation;
                }
                else if (draggedPoint != null && currentEvent.type == EventType.MouseDrag)
                {
                    if (mouseLocation == mouseClickLocation)
                    {
                        draggedPoint.Time = Mathf.Clamp(plotPosition.x, dragMin, dragMax);
                        draggedPoint.Value = currentEditMode != 2 ? Mathf.Clamp(plotPosition.y, 0, 1) : draggedPoint.Value;
                        if (draggedPointEvent is TransientEvent te)
                            te.Time = te.Intensity.Time = te.Sharpness.Time = draggedPoint.Time;
                        else if (draggedPointEvent is ContinuousEvent ce)
                        {
                            if (draggedPoint == ce.IntensityCurve.First() || draggedPoint == ce.SharpnessCurve.First())
                                ce.Time = ce.IntensityCurve.First().Time = ce.SharpnessCurve.First().Time = draggedPoint.Time;
                            else if (draggedPoint == ce.IntensityCurve.Last() || draggedPoint == ce.SharpnessCurve.Last())
                                ce.IntensityCurve.Last().Time = ce.SharpnessCurve.Last().Time = draggedPoint.Time;
                        }
                    }
                }
                else if (draggedPoint != null && currentEvent.type == EventType.MouseUp)
                {
                    draggedPoint = null;
                    draggedPointEvent = null;
                }
            }
            else if (currentEvent.type == EventType.MouseUp && mouseLocation != MouseLocation.Outside && hoverPoint != null && 
                ((currentEvent.button == 1 && hoverPointEvent.ShouldRemoveEventAfterRemovingPoint(hoverPoint, mouseLocation)) || currentEvent.button == 2))
            {
                events.Remove(hoverPointEvent);
                hoverPoint = null;
                hoverPointEvent = null;
            }

            #endregion

            if (mouseOverWindow == this)
                Repaint();
        }

        private ContinuousEvent GetContinuousEventIfBetween(float time)
        {
            foreach (var ev in events)
            {
                if (ev.Time < time && ev is ContinuousEvent continuousEvent)
                {
                    if (time > continuousEvent.Time && time < continuousEvent.IntensityCurve.Last().Time)
                        return continuousEvent;
                }
            }
            return null;
        }

        private EventPoint GetEventPointOnPosition(Vector2 plotPosition, MouseLocation plot, out VibrationEvent vibrationEvent)
        {
            vibrationEvent = null;
            Vector2 pointOffset = new Vector2(hoverOffset * time / plotSize.x, hoverOffset / plotSize.y);
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
            float lastPointTime = 0;
            foreach (var ev in events)
            {
                if (ev is TransientEvent)
                {
                    float t = ev.Time;
                    if (t > lastPointTime)
                        lastPointTime = t;
                }
                else
                {
                    float t = ((ContinuousEvent)ev).IntensityCurve.Last().Time;
                    if (t > lastPointTime)
                        lastPointTime = t;
                }    
            }
            return lastPointTime;
        }

        private AHAPFile ConvertEventsToAHAPFile()
        {
            List<Pattern> patternList = new();
            foreach (var ev in events)
                patternList.AddRange(ev.ToPatterns());
            patternList.Sort();
            AHAPFile file = new();
            file.Metadata = new Metadata();
            file.Pattern = patternList;
            return file;
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

            if (this.ahapFile != null)
            {
                if (EditorUtility.DisplayDialog("Overwrite file?", "Do you want to overwrite selected file?",
                    "Yes, overwrite", "No, create new"))
                {
                    File.WriteAllText(Path.Combine(Environment.CurrentDirectory, AssetDatabase.GetAssetPath(this.ahapFile)), json);
                    EditorUtility.SetDirty(this.ahapFile);
                    return;
                }
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

                    time = GetLastPointTime();

                    foreach (var patternElement in ahap.Pattern)
                    {
                        Event e = patternElement.Event;
                        if (e != null)
                        {
                            if (e.EventType == AHAPFile.EVENT_TRANSIENT)
                            {
                                int intensityIndex = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_INTENSITY);
                                float intensity = intensityIndex != -1 ? (float)e.EventParameters[intensityIndex].ParameterValue : 1;
                                int sharpnessIndex = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_SHARPNESS);
                                float sharpness = sharpnessIndex != -1 ? (float)e.EventParameters[sharpnessIndex].ParameterValue : 0;
                                events.Add(new TransientEvent((float)e.Time, intensity, sharpness));
                            }
                            else if (e.EventType == AHAPFile.EVENT_CONTINUOUS)
                            {
                                int intensityIndex = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_INTENSITY);
                                float intensity = intensityIndex != -1 ? (float)e.EventParameters[intensityIndex].ParameterValue : 1;
                                int sharpnessIndex = e.EventParameters.FindIndex(param => param.ParameterID == AHAPFile.PARAM_SHARPNESS);
                                float sharpness = sharpnessIndex != -1 ? (float)e.EventParameters[sharpnessIndex].ParameterValue : 0;

                                float t = (float)e.Time;
                                var intensityPoints = new List<EventPoint>();
                                var curve = ahap.Pattern.Find(element => element.ParameterCurve != null && (float)element.ParameterCurve.Time == t && 
                                    element.ParameterCurve.ParameterID == AHAPFile.CURVE_INTENSITY);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                    {
                                        intensityPoints.Add(new EventPoint((float)point.Time, (float)point.ParameterValue));
                                        t = (float)point.Time;
                                    }
                                    curve = ahap.Pattern.Find(element => element.ParameterCurve != null && (float)element.ParameterCurve.Time == t &&
                                        element.ParameterCurve.ParameterID == AHAPFile.CURVE_INTENSITY);
                                }
                                if (intensityPoints.Count == 0)
                                {
                                    intensityPoints.Add(new EventPoint((float)e.Time, intensity));
                                    intensityPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), intensity));
                                }
                                else if (!Mathf.Approximately(intensityPoints.Last().Time, (float)(e.Time + e.EventDuration)))
                                    intensityPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), intensityPoints.Last().Value));

                                t = (float)e.Time;
                                var sharpnessPoints = new List<EventPoint>();
                                curve = ahap.Pattern.Find(element => element.ParameterCurve != null && (float)element.ParameterCurve.Time == t &&
                                    element.ParameterCurve.ParameterID == AHAPFile.CURVE_SHARPNESS);
                                while (curve != null)
                                {
                                    foreach (var point in curve.ParameterCurve.ParameterCurveControlPoints)
                                    {
                                        sharpnessPoints.Add(new EventPoint((float)point.Time, (float)point.ParameterValue));
                                        t = (float)point.Time;
                                    }
                                    curve = ahap.Pattern.Find(element => element.ParameterCurve != null && (float)element.ParameterCurve.Time == t &&
                                        element.ParameterCurve.ParameterID == AHAPFile.CURVE_SHARPNESS);
                                }
                                if (sharpnessPoints.Count == 0)
                                {
                                    sharpnessPoints.Add(new EventPoint((float)e.Time, sharpness));
                                    sharpnessPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), sharpness));
                                }
                                else if (!Mathf.Approximately(sharpnessPoints.Last().Time, (float)(e.Time + e.EventDuration)))
                                    intensityPoints.Add(new EventPoint((float)(e.Time + e.EventDuration), sharpnessPoints.Last().Value));

                                ContinuousEvent ce = new ContinuousEvent();
                                ce.Time = (float)e.Time;
                                ce.IntensityCurve = intensityPoints;
                                ce.SharpnessCurve = sharpnessPoints;
                                events.Add(ce);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while importing file {ahapFile.name}{Environment.NewLine}{ex.Message}");
                    return;
                }                
            }
        }
    }    
}
