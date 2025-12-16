using System;
using System.Diagnostics;

namespace OpenSage.Diagnostics;

/// <summary>
/// Measures execution time of code blocks and maintains aggregated statistics.
/// Matches EA Generals PerfTimer implementation pattern.
/// </summary>
public class PerfTimer
{
    private readonly string _name;
    private long _totalTicks;
    private int _callCount;
    private long _minTicks;
    private long _maxTicks;
    private long _lastStartTick;
    private bool _isRunning;

    public string Name => _name;
    public int CallCount => _callCount;
    public double TotalMs => (_totalTicks * 1000.0) / Stopwatch.Frequency;
    public double AverageMs => CallCount > 0 ? TotalMs / CallCount : 0.0;
    public double MinMs => (_minTicks * 1000.0) / Stopwatch.Frequency;
    public double MaxMs => (_maxTicks * 1000.0) / Stopwatch.Frequency;

    public PerfTimer(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _totalTicks = 0;
        _callCount = 0;
        _minTicks = long.MaxValue;
        _maxTicks = long.MinValue;
        _lastStartTick = 0;
        _isRunning = false;
    }

    /// <summary>
    /// Starts the timer. Must call Stop() to complete measurement.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException($"PerfTimer '{_name}' is already running");
        }

        _lastStartTick = Stopwatch.GetTimestamp();
        _isRunning = true;
    }

    /// <summary>
    /// Stops the timer and records elapsed time.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException($"PerfTimer '{_name}' is not running");
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - _lastStartTick;
        _totalTicks += elapsedTicks;
        _callCount++;
        _minTicks = Math.Min(_minTicks, elapsedTicks);
        _maxTicks = Math.Max(_maxTicks, elapsedTicks);
        _isRunning = false;
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void Reset()
    {
        _totalTicks = 0;
        _callCount = 0;
        _minTicks = long.MaxValue;
        _maxTicks = long.MinValue;
        _isRunning = false;
    }

    /// <summary>
    /// Executes an action and measures its execution time.
    /// </summary>
    public void Measure(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        Start();
        try
        {
            action();
        }
        finally
        {
            Stop();
        }
    }

    /// <summary>
    /// Executes a function and measures its execution time.
    /// </summary>
    public T Measure<T>(Func<T> function)
    {
        if (function == null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        Start();
        try
        {
            return function();
        }
        finally
        {
            Stop();
        }
    }

    public override string ToString()
    {
        return CallCount == 0
            ? $"{_name}: (no measurements)"
            : $"{_name}: avg={AverageMs:F2}ms (min={MinMs:F2}ms, max={MaxMs:F2}ms, count={CallCount})";
    }
}
