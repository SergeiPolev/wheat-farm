using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    public static event Action OnUpdateFPS;
    [SerializeField] private int _frameCount = 60;

    private float[] _fpsBuffer;
    private int _countInBuffer;
    private int _fpsBufferIndex;
    public static float AverageFPS { get; private set; }
    public static float MinFPS { get; private set; }
    public static float MaxFPS { get; private set; }

    private void Start()
    {
        Init();
    }

    void Update()
    {
        if (_fpsBuffer == null || _fpsBuffer.Length != _frameCount)
            Init();

        UpdateBuffer();
        CalculateFPS();
        OnUpdateFPS?.Invoke();
    }

    private void Init()
    {
        if (_frameCount <= 0)
            _frameCount = 1;
        _fpsBuffer = new float[_frameCount];
        _fpsBufferIndex = 0;
        _countInBuffer = 0;
    }

    private void UpdateBuffer()
    {
        float fps = 1f / Time.unscaledDeltaTime;
        if (float.IsInfinity(fps))
        {
            Debug.LogWarning("FPS is Infinity");
            return;
        }
        if (float.IsNaN(fps))
        {
            Debug.LogWarning("FPS is NaN!");
            return;
        }

        _fpsBuffer[_fpsBufferIndex++] = fps;
        if (_fpsBufferIndex >= _fpsBuffer.Length)
            _fpsBufferIndex = 0;

        if (_countInBuffer < _fpsBuffer.Length)
            _countInBuffer++;
    }

    private void CalculateFPS()
    {
        float sum = 0;
        float min = float.MaxValue;
        float max = 0;
        for (int i = 0; i < _countInBuffer; i++)
        {
            sum += _fpsBuffer[i];
            if (min > _fpsBuffer[i])
                min = _fpsBuffer[i];
            if (max < _fpsBuffer[i])
                max = _fpsBuffer[i];
        }
        AverageFPS = sum / _countInBuffer;
        MinFPS = min;
        MaxFPS = max;
    }
}