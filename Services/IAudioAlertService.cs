using System;
using System.Threading.Tasks;

namespace YureteruWPF.Services;

/// <summary>
/// Interface for audio alert service
/// </summary>
public interface IAudioAlertService
{
    void CheckAndPlayAlert(double intensity);
    void PlayAlertSound();
}
