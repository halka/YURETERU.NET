using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace YureteruWPF.Services;

/// <summary>
/// Audio alert service using NAudio (ported from useAudioAlert.js)
/// </summary>
public class AudioAlertService : IAudioAlertService
{
    private readonly double _alertThreshold;
    private readonly int _cooldownMs;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public AudioAlertService(double alertThreshold = 3.0, int cooldownMs = 6000)
    {
        _alertThreshold = alertThreshold;
        _cooldownMs = cooldownMs;
    }

    public void CheckAndPlayAlert(double intensity)
    {
        if (intensity >= _alertThreshold)
        {
            var now = DateTime.Now;
            if ((now - _lastAlertTime).TotalMilliseconds > _cooldownMs)
            {
                PlayAlertSound();
                _lastAlertTime = now;
            }
        }
    }

    public void PlayAlertSound()
    {
        try
        {
            // Create a sawtooth wave that sweeps from 880Hz to 440Hz
            var sampleRate = 44100;
            var duration = 0.3; // seconds
            var samples = (int)(sampleRate * duration);

            var buffer = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                var t = (double)i / sampleRate;
                // Exponential frequency sweep from 880 to 440 Hz
                var freq = 880 * Math.Pow(0.5, t / duration);
                // Sawtooth wave
                var phase = (freq * t) % 1.0;
                var sawtoothValue = (float)(2.0 * phase - 1.0);
                // Exponential volume envelope
                var envelope = (float)(0.3 * Math.Pow(0.01 / 0.3, t / duration));
                buffer[i] = sawtoothValue * envelope;
            }

            // Play the sound
            var provider = new RawSourceWaveStream(
                new MemoryStream(ConvertToBytes(buffer)),
                new WaveFormat(sampleRate, 32, 1));

            using var outputDevice = new WaveOutEvent();
            outputDevice.Init(provider);
            outputDevice.Play();
            
            // Wait for playback to finish
            System.Threading.Thread.Sleep((int)(duration * 1000));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio playback failed: {ex.Message}");
        }
    }

    private byte[] ConvertToBytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
