// Copyright © 2018 Jesse Keogh
// https://github.com/jesse-scam/algorithmic-beat-mapping-unity
// Slightly reformatted for better readability

using System.Collections.Generic;
using UnityEngine;

public class SpectralFluxInfo 
{
	public float Time;
	public float SpectralFlux;
	public float Threshold;
	public float PrunedSpectralFlux;
	public bool IsPeak;
}

public class SpectralFluxAnalyzer 
{
    public List<SpectralFluxInfo> SpectralFluxSamples;

    /// <summary>
    /// Sensitivity multiplier to scale the average threshold.
    /// If a rectified spectral flux sample value is larger than
    /// <see cref="_thresholdMultiplier"/> times the average, it is a peak.
    /// </summary>
    float _thresholdMultiplier = 1.5f;

    /// <summary>
    /// Number of samples to average in our window.
    /// </summary>
	int _thresholdWindowSize = 50;

	int _sampleCount = 1024;
	int _indexToProcess;
	float[] _currentSpectrum;
	float[] _previousSpectrum;

	public int ThresholdWindowSize => _thresholdWindowSize;

	public SpectralFluxAnalyzer(int sampleChunkSize, float sensitivity) 
	{
		_sampleCount = sampleChunkSize;
		_thresholdMultiplier = 1f + sensitivity;
		SpectralFluxSamples = new List<SpectralFluxInfo> ();

		// Start processing from middle of first window and increment by 1 from there
		_indexToProcess = _thresholdWindowSize / 2;

		_currentSpectrum = new float[_sampleCount];
		_previousSpectrum = new float[_sampleCount];
	}

    public void SetCurrentSpectrum(float[] spectrum)
    {
        _currentSpectrum.CopyTo(_previousSpectrum, 0);
        spectrum.CopyTo(_currentSpectrum, 0);
    }

    public void AnalyzeSpectrum(float[] spectrum, float time) 
	{
		// Set spectrum
		SetCurrentSpectrum(spectrum);

		// Get current spectral flux from spectrum
		SpectralFluxInfo currentInfo = new();
		currentInfo.Time = time;
        currentInfo.SpectralFlux = CalculateRectifiedSpectralFlux();
        SpectralFluxSamples.Add(currentInfo);

        // We have enough samples to detect a peak
        if (SpectralFluxSamples.Count >= _thresholdWindowSize)
        {
            // Get Flux threshold of time window surrounding index to process
            SpectralFluxSamples[_indexToProcess].Threshold = GetFluxThreshold(_indexToProcess);

            // Only keep amp amount above threshold to allow peak filtering
            SpectralFluxSamples[_indexToProcess].PrunedSpectralFlux = GetPrunedSpectralFlux(_indexToProcess);

            // After processing n samples, n-1 has neighbors (n-2, n) to determine peak
            int indexToDetectPeak = _indexToProcess - 1;
            if (IsPeak(indexToDetectPeak))
                SpectralFluxSamples[indexToDetectPeak].IsPeak = true;

            _indexToProcess++;
		}
	}

    float CalculateRectifiedSpectralFlux()
    {
        float sum = 0f;
        for (int i = 0; i < _sampleCount; i++)
            sum += Mathf.Max(0f, _currentSpectrum[i] - _previousSpectrum[i]);
        return sum;
    }

    float GetFluxThreshold(int spectralFluxIndex)
    {
        // How many samples in the past and future to include in average
        int windowStartIndex = Mathf.Max(0, spectralFluxIndex - _thresholdWindowSize / 2);
        int windowEndIndex = Mathf.Min(SpectralFluxSamples.Count - 1, spectralFluxIndex + _thresholdWindowSize / 2);

        // Add up our spectral flux over the window
        float sum = 0f;
        for (int i = windowStartIndex; i < windowEndIndex; i++)
            sum += SpectralFluxSamples[i].SpectralFlux;

        // Return the average multiplied by sensitivity multiplier
        float avg = sum / (windowEndIndex - windowStartIndex);
        return avg * _thresholdMultiplier;
    }

    float GetPrunedSpectralFlux(int spectralFluxIndex)
    {
        return Mathf.Max(0f, SpectralFluxSamples[spectralFluxIndex].SpectralFlux - SpectralFluxSamples[spectralFluxIndex].Threshold);
    }

    bool IsPeak(int spectralFluxIndex)
    {
        if (SpectralFluxSamples[spectralFluxIndex].PrunedSpectralFlux > SpectralFluxSamples[spectralFluxIndex + 1].PrunedSpectralFlux &&
            SpectralFluxSamples[spectralFluxIndex].PrunedSpectralFlux > SpectralFluxSamples[spectralFluxIndex - 1].PrunedSpectralFlux)
        {
            return true;
        }
        
        return false;
    }
}