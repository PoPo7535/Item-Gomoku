using System;
using UnityEngine;

public static class Options
{
    public static event Action<float> BGMVolume;
    public static event Action<float> SFXVolume;

    public static void BGMVolumeChanged(float volume)
    {
        BGMVolume?.Invoke(volume);
    }

    public static void SFXVolumeChanged(float volume)
    {
        SFXVolume?.Invoke(volume);
    }

}
