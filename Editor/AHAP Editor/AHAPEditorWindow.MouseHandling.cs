using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow
    {
        private void HandleNonHoverClick()
        {
            _mouseClickPosition = _currentEvent.mousePosition;
            _mouseClickLocation = _mouseLocation;
            _mouseClickPlotPosition = _mousePlotPosition;
            _previousMouseState = EventType.MouseDown;
        }

        private void HandleAddFinish()
        {
            if (_previousMouseState == EventType.MouseDown) // Just clicked
            {
                if (TryGetContinuousEvent(_mousePlotPosition.x, out ContinuousEvent ce)) // Add point to continuous event if clicked between start and end
                    ce.AddPointToCurve(_mousePlotPosition, _mouseLocation);
                else // Add transient event
                    _events.Add(new TransientEvent(_mousePlotPosition, _mouseLocation));
            }
            else if (_previousMouseState == EventType.MouseDrag && !TryGetContinuousEvent(_mousePlotPosition.x, out _))
            {
                Vector2 endPoint = _currentEvent.shift ? new Vector2(_mousePlotPosition.x, _mouseClickPlotPosition.y) : _mousePlotPosition;
                _events.Add(new ContinuousEvent(_mouseClickPlotPosition, endPoint, _mouseLocation));
            }
            _events.Sort();
            _previousMouseState = EventType.MouseUp;
        }

        private void HandleDragStart()
        {
            _previousMouseState = EventType.MouseDrag; //MouseDown?
            _draggedPoint = _hoverPoint;
            _mouseClickLocation = _mouseLocation;
            _mouseClickPlotPosition = _draggedPoint;

            _dragMin = _scrollPosition.x / _plotScrollSize.x * _time + NEIGHBOUR_POINT_OFFSET - _selectedPoints[0].Time;
            _dragMax = (_scrollPosition.x + _plotScreenSize.x) / _plotScrollSize.x * _time - NEIGHBOUR_POINT_OFFSET - _selectedPoints[^1].Time;
            _dragValueMin = 1;
            _dragValueMax = 0;

            EventPoint cePoint = null;
            foreach (var point in _selectedPoints)
            {
                if (point.ParentEvent is ContinuousEvent ce)
                {
                    cePoint ??= point;

                    (List<EventPoint> dragCurve, List<EventPoint> otherCurve) = _mouseLocation == MouseLocation.IntensityPlot ?
                        (ce.IntensityCurve, ce.SharpnessCurve) : (ce.SharpnessCurve, ce.IntensityCurve);
                    if (point == dragCurve[0])
                    {
                        var nextPoint = otherCurve.Find(p => p.Time > cePoint.Time);
                        if (nextPoint != null) _dragMax = Mathf.Min(_dragMax, nextPoint.Time - NEIGHBOUR_POINT_OFFSET - cePoint.Time);
                    }
                    else if (point == dragCurve.Last())
                    {
                        var previousPoint = otherCurve.FindLast(p => p.Time < point.Time);
                        if (previousPoint != null) _dragMin = Mathf.Max(_dragMin, previousPoint.Time + NEIGHBOUR_POINT_OFFSET - point.Time);
                    }
                }

                _dragValueMin = Mathf.Min(_dragValueMin, point.Value);
                _dragValueMax = Mathf.Max(_dragValueMax, point.Value);
            }
            _dragValueMin = -_dragValueMin;
            _dragValueMax = -(_dragValueMax - 1);

            if (cePoint != null)
            {
                var ce = (ContinuousEvent)cePoint.ParentEvent;
                (List<EventPoint> dragCurve, List<EventPoint> otherCurve) = _mouseLocation == MouseLocation.IntensityPlot ?
                    (ce.IntensityCurve, ce.SharpnessCurve) : (ce.SharpnessCurve, ce.IntensityCurve);
                if (cePoint == dragCurve[0])
                {
                    var previousEvent = _events.FindLast(ev => ev.Time < cePoint.Time && ev is ContinuousEvent);
                    if (previousEvent != null)
                        _dragMin = Mathf.Max(_dragMin, ((ContinuousEvent)previousEvent).IntensityCurve.Last().Time + NEIGHBOUR_POINT_OFFSET - cePoint.Time);
                }
                else
                {
                    var previousPoint = dragCurve.FindLast(p => p.Time < cePoint.Time);
                    if (previousPoint != null) _dragMin = Mathf.Max(_dragMin, previousPoint.Time + NEIGHBOUR_POINT_OFFSET - cePoint.Time);
                }

                for (int i = _selectedPoints.Count - 1; i >= 0; i--)
                {
                    if (_selectedPoints[i].ParentEvent is ContinuousEvent ce2)
                    {
                        cePoint = _selectedPoints[i];
                        (dragCurve, otherCurve) = _mouseLocation == MouseLocation.IntensityPlot ?
                            (ce2.IntensityCurve, ce2.SharpnessCurve) : (ce2.SharpnessCurve, ce2.IntensityCurve);
                        if (cePoint == dragCurve.Last())
                        {
                            var nextEvent = _events.Find(ev => ev.Time > cePoint.Time && ev is ContinuousEvent);
                            if (nextEvent != null) _dragMax = Mathf.Min(_dragMax, nextEvent.Time - NEIGHBOUR_POINT_OFFSET - cePoint.Time);
                        }
                        else
                        {
                            var nextPoint = dragCurve.Find(p => p.Time > cePoint.Time);
                            if (nextPoint != null) _dragMax = Mathf.Min(_dragMax, nextPoint.Time - NEIGHBOUR_POINT_OFFSET - cePoint.Time);
                        }
                        break;
                    }
                }

                _dragMinBound = _selectedPoints[0].Time + _dragMin;
                _dragMaxBound = _selectedPoints[^1].Time + _dragMax;
            }
        }

        private void HandleDrag()
        {
            Vector2 offset = _mousePlotPosition - _mouseClickPlotPosition;
            offset.x = Mathf.Clamp(offset.x, _dragMin, _dragMax);
            offset.y = Mathf.Clamp(offset.y, _dragValueMin, _dragValueMax);
            Vector2 newDragPointPosition = _mouseClickPlotPosition + offset;
            offset = newDragPointPosition - _draggedPoint;

            if (_pointDragMode == PointDragMode.LockTime || _currentEvent.alt)
                offset.x = 0;
            if (_pointDragMode == PointDragMode.LockValue || _currentEvent.shift)
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

        private void HandlePointRemoval()
        {
            bool removeEvent = false;
            if (_currentEvent.button == (int)MouseButton.Right)
            {
                removeEvent = _hoverPoint.ParentEvent.ShouldRemoveEventAfterRemovingPoint(_hoverPoint, _mouseLocation);
                if (!removeEvent)
                {
                    _selectedPoints.Remove(_hoverPoint);
                    _hoverPoint = null;
                }
            }
            else if (_currentEvent.button == (int)MouseButton.Middle)
                removeEvent = true;

            if (removeEvent)
            {
                if (_hoverPoint.ParentEvent is TransientEvent && _selectedPoints.Contains(_hoverPoint))
                    _selectedPoints.Remove(_hoverPoint);
                else if (_hoverPoint.ParentEvent is ContinuousEvent ce && _selectedPointsLocation == _mouseLocation)
                {
                    var curve = _mouseLocation == MouseLocation.IntensityPlot ? ce.IntensityCurve : ce.SharpnessCurve;
                    foreach (var point in curve)
                        _selectedPoints.Remove(point);
                }
                _events.Remove(_hoverPoint.ParentEvent);
                _hoverPoint = null;
            }
        }

        private void HandleSelection()
        {
            float minTime = Mathf.Min(_mouseClickPlotPosition.x, _mousePlotPosition.x);
            float maxTime = Mathf.Max(_mouseClickPlotPosition.x, _mousePlotPosition.x);
            float minValue = Mathf.Min(_mouseClickPlotPosition.y, _mousePlotPosition.y);
            float maxValue = Mathf.Max(_mouseClickPlotPosition.y, _mousePlotPosition.y);

            _selectedPoints.Clear();
            foreach (var ev in _events)
            {
                if (ev is TransientEvent te && te.Time >= minTime && te.Time <= maxTime)
                {
                    var point = _mouseClickLocation == MouseLocation.IntensityPlot ? te.Intensity : te.Sharpness;
                    if (point.Value >= minValue && point.Value <= maxValue)
                        _selectedPoints.Add(point);
                }
                else if (ev is ContinuousEvent ce)
                {
                    var curve = _mouseClickLocation == MouseLocation.IntensityPlot ? ce.IntensityCurve : ce.SharpnessCurve;
                    foreach (var point in curve)
                    {
                        if (point.Time >= minTime && point.Time <= maxTime && point.Value >= minValue && point.Value <= maxValue)
                            _selectedPoints.Add(point);
                    }
                }
            }
            _selectedPoints.Sort();
            _selectedPointsLocation = _mouseClickLocation;
        }

        private void SelectHoverEvent()
        {
            _selectedPoints.Clear();
            if (_hoverPoint.ParentEvent is TransientEvent)
                _selectedPoints.Add(_hoverPoint);
            else if (_hoverPoint.ParentEvent is ContinuousEvent ce)
            {
                var curve = _mouseLocation == MouseLocation.IntensityPlot ? ce.IntensityCurve : ce.SharpnessCurve;
                _selectedPoints.AddRange(curve);
            }
            _selectedPointsLocation = _mouseLocation;
        }
    }
}
