using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow : IHasCustomMenu
    {
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(Content.debugModeLabel, DebugMode, () => DebugMode = !DebugMode);
        }

        private void Clear()
        {
            _events ??= new List<VibrationEvent>();
            _events.Clear();
            _selectedPoints ??= new List<EventPoint>();
            _selectedPoints.Clear();
            _zoom = 1f;
            _time = _waveformClip != null ? _waveformClip.length : 1;
        }

        private void ResetState()
        {
            _ahapFile = null;
            _projectName = "";
            _waveformClip = null;
            _waveformRenderScale = _waveformLastPaintedZoom = 1f;
            _waveformVisible = _waveformNormalize = _waveformShouldRepaint = false;
            _pointDragModes = Enum.GetNames(typeof(PointDragMode));
            for (int i = 0; i < _pointDragModes.Length; i++)
                _pointDragModes[i] = string.Concat(_pointDragModes[i].Select(x => char.IsUpper(x) ? $" {x}" : x.ToString())).TrimStart(' ');
            _plotAreaWidthFactor = PLOT_AREA_BASE_WIDTH;
            _pointEditAreaVisible = false;

            DebugMode = false;
        }

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

        private Vector3 PointToWindowCoords(Vector2 point, Rect plotRect)
        {
            return new Vector3(plotRect.x + point.x / _time * _plotScrollSize.x - _scrollPosition.x,
                plotRect.y + plotRect.height - point.y * plotRect.height);
        }

        private bool TryGetContinuousEvent(float time, out ContinuousEvent continuousEvent)
        {
            continuousEvent = null;
            foreach (var ev in _events)
            {
                if (time >= ev.Time && ev is ContinuousEvent ce && time <= ce.IntensityCurve.Last().Time)
                {
                    continuousEvent = ce;
                    return true;
                }
            }
            return false;
        }

        private EventPoint GetPointOnPosition(Vector2 plotPosition, MouseLocation plot)
        {
            Vector2 pointOffset = new(HOVER_OFFSET * _time / _plotScrollSize.x, HOVER_OFFSET / _plotScreenSize.y);
            foreach (var ev in _events)
            {
                if (ev.IsOnPointInEvent(plotPosition, pointOffset, plot, out EventPoint eventPoint))
                    return eventPoint;
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
    }
}
