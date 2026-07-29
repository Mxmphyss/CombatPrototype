using System;
using UnityEngine;

public sealed class CombatFrameClock : MonoBehaviour
{
    public const int DefaultFramesPerSecond = 60;

    [Min(1)]
    [SerializeField] private int framesPerSecond =
        DefaultFramesPerSecond;
    [Min(1)]
    [SerializeField] private int maxCatchUpTicks = 8;

    private double accumulator;

    public event Action<int> OnCombatTick;

    public int CurrentFrame { get; private set; }
    public bool IsRunning { get; private set; }
    public int FramesPerSecond => Mathf.Max(1, framesPerSecond);
    public float SecondsPerFrame => 1f / FramesPerSecond;

    private void Update()
    {
        if (!IsRunning)
            return;

        accumulator += Time.unscaledDeltaTime;
        double step = 1d / FramesPerSecond;
        int ticks = 0;
        int catchUpLimit = Mathf.Max(1, maxCatchUpTicks);

        while (accumulator + double.Epsilon >= step &&
               ticks < catchUpLimit)
        {
            accumulator -= step;
            AdvanceOneFrame();
            ticks++;
        }

        // Keep a bounded backlog instead of jumping the logical frame.
        // Combat ticks remain contiguous even after a rendering hitch.
        double maximumBacklog = step * catchUpLimit;
        if (accumulator > maximumBacklog)
            accumulator = maximumBacklog;
    }

    public void Configure(CombatFrameDataSettings settings)
    {
        if (settings == null)
            return;

        framesPerSecond = settings.FramesPerSecond;
        maxCatchUpTicks = settings.MaxCatchUpTicks;
    }

    public void StartClock()
    {
        IsRunning = true;
    }

    public void StopClock()
    {
        IsRunning = false;
        accumulator = 0d;
    }

    public void ResetClock(bool keepRunning = true)
    {
        CurrentFrame = 0;
        accumulator = 0d;
        IsRunning = keepRunning;
    }

    public void AdvanceOneFrame()
    {
        CurrentFrame++;
        OnCombatTick?.Invoke(CurrentFrame);
    }

    public void AdvanceFramesForTests(int frameCount)
    {
        int count = Mathf.Max(0, frameCount);
        for (int i = 0; i < count; i++)
            AdvanceOneFrame();
    }
}
