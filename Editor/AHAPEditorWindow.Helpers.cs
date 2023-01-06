using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chroma.Utility.Haptics.AHAPEditor
{
    public partial class AHAPEditorWindow : IHasCustomMenu
    {
        bool SafeMode
        {
            get => _safeMode;
            set
            {
                _safeMode = value;
                EditorPrefs.SetBool(SAFE_MODE_KEY, value);
            }
        }

        bool DebugMode
        {
            get => _debugMode;
            set
            {
                _debugMode = value;
                EditorPrefs.SetBool(DEBUG_MODE_KEY, value);
                if (!_debugMode)
                    _drawRects = false;
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(Content.resetLabel, false, () => ResetState());
            menu.AddItem(Content.safeModeLabel, SafeMode, () => SafeMode = !SafeMode);
            menu.AddSeparator("");
            menu.AddItem(Content.debugModeLabel, DebugMode, () => DebugMode = !DebugMode);
        }

        private void Clear()
        {
            _events ??= new List<HapticEvent>();
            _events.Clear();
            _selectedPoints ??= new List<EventPoint>();
            _selectedPoints.Clear();
            _singleSelectedPoint = null;
            _zoom = 1f;
            _time = _waveformClip != null ? _waveformClip.length : 1;
        }

        private void ResetState()
        {
            _vibrationAsset = null;
            _projectName = "";
            _waveformClip = null;
            _waveformRenderScale = _waveformLastPaintedZoom = 1f;
            _waveformVisible = _waveformNormalize = _waveformShouldRepaint = false;
            _pointDragMode = PointDragMode.FreeMove;
            _pointDragModes = Enum.GetNames(typeof(PointDragMode));
            for (int i = 0; i < _pointDragModes.Length; i++)
                _pointDragModes[i] = string.Concat(_pointDragModes[i].Select(x => char.IsUpper(x) ? $" {x}" : x.ToString())).TrimStart(' ');
            _mouseMode = MouseMode.AddRemove;
            _mouseModes = Enum.GetNames(typeof(MouseMode));
            _plotAreaWidthFactor = PLOT_AREA_BASE_WIDTH;
            _pointEditAreaVisible = EditorPrefs.GetBool(ADVANCED_PANEL_KEY, false);
            _currentEvent = null;
            _selectedPoints.Clear();
            SafeMode = EditorPrefs.GetBool(SAFE_MODE_KEY, true);
            _importFormatMenu = new GenericMenu();
            Type dataFormatType = typeof(DataFormat);
            string[] dataFormats = Enum.GetNames(dataFormatType);
            foreach (var dataFormat in dataFormats)
            {
                var attribute = dataFormatType.GetMember(dataFormat).First().GetCustomAttribute<InspectorNameAttribute>();
                string dataFormatName = attribute != null ? attribute.displayName : dataFormat;
                _importFormatMenu.AddItem(new GUIContent(dataFormatName), false, 
                    () => OnImportClicked((DataFormat)Enum.Parse(dataFormatType, dataFormat)));
            }

            DebugMode = EditorPrefs.GetBool(DEBUG_MODE_KEY, false);
        }

        private void OnImportClicked(DataFormat dataFormat = DataFormat.Linear)
        {
            if (SafeMode && _events.Count > 0)
                EditorUtils.ConfirmDialog(title: "Safe Mode warning",
                    message: "You will lose unsaved changes. Continue import?", onOk: () => HandleImport(dataFormat));
            else HandleImport(dataFormat);
        }

        private bool IsTimeInView(float time)
        {
            float scrollTime = time / _time * _plotScrollSize.x;
            return scrollTime >= _scrollPosition.x && scrollTime <= _scrollPosition.x + _plotScreenSize.x;
        }

        private Vector3 PointToScrollCoords(float time, float value, float heightOffset = 0)
        {
            return new Vector3(time / _time * _plotScrollSize.x,
                _plotScreenSize.y - value * _plotScreenSize.y + heightOffset);
        }

        private Vector3 PointToWindowCoords(EventPoint point, Rect plotRect)
        {
            return new Vector3(plotRect.x + point.Time / _time * _plotScrollSize.x - _scrollPosition.x,
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
            if (plot == MouseLocation.Outside) return null;
            Vector2 pointOffset = new(HOVER_OFFSET * _time / _plotScrollSize.x, HOVER_OFFSET / _plotScreenSize.y);
            Rect offsetRect = new(plotPosition - pointOffset, pointOffset * 2);
            foreach (var ev in _events)
            {
                if (ev.IsOnPointInEvent(in offsetRect, plot, out EventPoint eventPoint))
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

        private JsonAHAP ConvertEventsToAHAPFile(DataFormat dataFormat = DataFormat.Linear)
        {
            List<Pattern> patternList = new();
            foreach (var ev in _events)
                patternList.AddRange(ev.ToAHAP());
            patternList.Sort();
            return new JsonAHAP(1, new Metadata(_projectName), patternList);
        }

        private JsonHaptic ConvertEventsToHapticFile(DataFormat dataFormat = DataFormat.Linear)
        {
            _events.Sort();
            List<ContinuousEvent> continuousEvents = new();
            List<TransientEvent> transientEvents = new();
            foreach (var ev in _events)
            {
                if (ev is ContinuousEvent ce) continuousEvents.Add(ce);
                else if (ev is TransientEvent te) transientEvents.Add(te);
            }
            List<Amplitude> amplitudes = new();
            List<Frequency> frequencies = new();
            foreach (var ce in continuousEvents)
            {
                foreach (var intensity in ce.IntensityCurve)
                    amplitudes.Add(new Amplitude(intensity.Time, CalculateExportParameter(intensity.Value, dataFormat)));
                foreach (var sharpness in ce.SharpnessCurve)
                    frequencies.Add(new Frequency(sharpness.Time, CalculateExportParameter(sharpness.Value, dataFormat)));
            }
            if (amplitudes[0].time != 0)
            {
                amplitudes.Insert(0, new Amplitude(0, 0));
                amplitudes.Insert(1, new Amplitude(Mathf.Max((float)amplitudes[1].time - NEIGHBOURING_POINT_OFFSET, 0), 0));
            }
            if (frequencies[0].time != 0)
            {
                frequencies.Insert(0, new Frequency(0, 0));
                frequencies.Insert(1, new Frequency(Mathf.Max((float)frequencies[1].time - NEIGHBOURING_POINT_OFFSET, 0), 0));
            }

            (Amplitude lastAmplitude, Frequency lastFrequency) = (amplitudes.Last(), frequencies.Last());
            if (!Mathf.Approximately((float)lastAmplitude.time, (float)lastFrequency.time)) // shouldn't happen?
            {
                if (lastAmplitude.time > lastFrequency.time)
                    frequencies.Add(new Frequency(lastAmplitude.time, lastFrequency.frequency));
                else
                    amplitudes.Add(new Amplitude(lastFrequency.time, lastAmplitude.amplitude));
            }

            (lastAmplitude, lastFrequency) = (amplitudes.Last(), frequencies.Last());
            if (transientEvents.Count > 0 && transientEvents.Last().Time > (float)amplitudes.Last().time) 
            {
                TransientEvent lastTransient = transientEvents.Last();
                amplitudes.Add(new Amplitude(lastAmplitude.time + NEIGHBOURING_POINT_OFFSET, 0));
                amplitudes.Add(new Amplitude(lastTransient.Time, 0));
                frequencies.Add(new Frequency(lastFrequency.time + NEIGHBOURING_POINT_OFFSET, 0));
                frequencies.Add(new Frequency(lastTransient.Time, 0));
            }

            int startIndex = 0;
            foreach (var te in transientEvents)
            {
                int index = amplitudes.FindIndex(a => Mathf.Approximately((float)a.time, te.Time));
                if (index != -1) 
                {
                    amplitudes[index].emphasis = new Emphasis(CalculateExportParameter(te.Intensity.Value, dataFormat),
                        CalculateExportParameter(te.Sharpness.Value, dataFormat));
                    startIndex = index;
                }
                else
                {
                    index = amplitudes.FindIndex(startIndex, a => a.time > te.Time);
                    if (index != -1)
                    {
                        float val1 = CalculateImportParameter(amplitudes[index - 1].amplitude, dataFormat);
                        float val2 = CalculateImportParameter(amplitudes[index].amplitude, dataFormat);
                        float value = Mathf.Lerp(val1, val2, (te.Time - val1) / (val2 - val1));
                        Amplitude a = new(te.Time, CalculateExportParameter(value, dataFormat));
                        a.emphasis = new Emphasis(CalculateExportParameter(te.Intensity.Value, dataFormat),
                            CalculateExportParameter(te.Sharpness.Value, dataFormat));
                        amplitudes.Insert(index, a);
                        startIndex = index;
                    }
                }
            }

            JsonHaptic hapticFile = new();
            hapticFile.signals.continuous.envelopes.amplitude = amplitudes;
            hapticFile.signals.continuous.envelopes.frequency = frequencies;
            return hapticFile;
        }
    }
}
