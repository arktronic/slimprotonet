using System;
using System.Diagnostics;

namespace SlimProtoNet.Wrappers;

/// <summary>
/// Wraps System.Diagnostics.Stopwatch for easier mocking in tests.
/// </summary>
public class StopwatchWrapper
{
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public virtual TimeSpan Elapsed => _stopwatch.Elapsed;

    public virtual void Start()
    {
        _stopwatch.Start();
    }

    public virtual void Reset()
    {
        _stopwatch.Reset();
    }

    public virtual void Restart()
    {
        _stopwatch.Restart();
    }
}
