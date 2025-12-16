using System;
using System.Threading;
using OpenSage.Diagnostics;
using Xunit;

namespace OpenSage.Tests.Diagnostics;

public class PerfTimerTests
{
    [Fact]
    public void Constructor_InitializesProperties()
    {
        var timer = new PerfTimer("Test");

        Assert.Equal("Test", timer.Name);
        Assert.Equal(0, timer.CallCount);
        Assert.Equal(0.0, timer.TotalMs);
    }

    [Fact]
    public void StartStop_MeasuresTimeCorrectly()
    {
        var timer = new PerfTimer("Sleep");

        timer.Start();
        Thread.Sleep(10);
        timer.Stop();

        Assert.Equal(1, timer.CallCount);
        Assert.True(timer.TotalMs >= 10.0, $"Expected >= 10ms, got {timer.TotalMs}ms");
    }

    [Fact]
    public void MultipleStartStop_AccumulatesTiming()
    {
        var timer = new PerfTimer("Multiple");

        for (int i = 0; i < 3; i++)
        {
            timer.Start();
            Thread.Sleep(5);
            timer.Stop();
        }

        Assert.Equal(3, timer.CallCount);
        Assert.True(timer.TotalMs >= 15.0);
        Assert.True(timer.AverageMs >= 5.0);
    }

    [Fact]
    public void Measure_Action_WorksCorrectly()
    {
        var timer = new PerfTimer("MeasureAction");
        var executed = false;

        timer.Measure(() => {
            executed = true;
            Thread.Sleep(5);
        });

        Assert.True(executed);
        Assert.Equal(1, timer.CallCount);
        Assert.True(timer.TotalMs >= 5.0);
    }

    [Fact]
    public void Measure_Function_ReturnsValue()
    {
        var timer = new PerfTimer("MeasureFunc");

        var result = timer.Measure(() => {
            Thread.Sleep(5);
            return 42;
        });

        Assert.Equal(42, result);
        Assert.Equal(1, timer.CallCount);
    }

    [Fact]
    public void Reset_ClearsAllStatistics()
    {
        var timer = new PerfTimer("Reset");
        timer.Start();
        Thread.Sleep(5);
        timer.Stop();

        Assert.True(timer.CallCount > 0);

        timer.Reset();

        Assert.Equal(0, timer.CallCount);
        Assert.Equal(0.0, timer.TotalMs);
    }

    [Fact]
    public void DoubleStart_Throws()
    {
        var timer = new PerfTimer("Double");
        timer.Start();

        Assert.Throws<InvalidOperationException>(() => timer.Start());

        timer.Stop();
    }

    [Fact]
    public void StopWithoutStart_Throws()
    {
        var timer = new PerfTimer("NoStart");

        Assert.Throws<InvalidOperationException>(() => timer.Stop());
    }

    [Fact]
    public void MinMax_TrackedCorrectly()
    {
        var timer = new PerfTimer("MinMax");

        // First: 10ms
        timer.Start();
        Thread.Sleep(10);
        timer.Stop();

        // Second: 5ms
        timer.Start();
        Thread.Sleep(5);
        timer.Stop();

        // Third: 15ms
        timer.Start();
        Thread.Sleep(15);
        timer.Stop();

        Assert.True(timer.MinMs >= 5.0 && timer.MinMs <= 10.0);
        Assert.True(timer.MaxMs >= 15.0);
    }
}

public class PerfGatherTests
{
    [Fact]
    public void GetOrCreate_ReturnsSameInstance()
    {
        PerfGather.ResetAll();

        var gather1 = PerfGather.GetOrCreate("Test");
        var gather2 = PerfGather.GetOrCreate("Test");

        Assert.Same(gather1, gather2);

        PerfGather.ResetAll();
    }

    [Fact]
    public void Profile_Action_RecordsTime()
    {
        PerfGather.ResetAll();

        PerfGather.Profile("Work", () => {
            Thread.Sleep(10);
        });

        var gather = PerfGather.GetOrCreate("Work");
        Assert.Equal(1, gather.CallCount);
        Assert.True(gather.GrossTotalMs >= 10.0);

        PerfGather.ResetAll();
    }

    [Fact]
    public void Profile_Function_ReturnsValue()
    {
        PerfGather.ResetAll();

        var result = PerfGather.Profile("Calculate", () => {
            Thread.Sleep(5);
            return 99;
        });

        Assert.Equal(99, result);

        var gather = PerfGather.GetOrCreate("Calculate");
        Assert.Equal(1, gather.CallCount);

        PerfGather.ResetAll();
    }

    [Fact]
    public void NestedProfile_CreatesHierarchy()
    {
        PerfGather.ResetAll();

        PerfGather.Profile("Outer", () => {
            Thread.Sleep(5);
            PerfGather.Profile("Inner", () => {
                Thread.Sleep(5);
            });
        });

        var outer = PerfGather.GetOrCreate("Outer");
        Assert.Equal(1, outer.CallCount);
        Assert.Single(outer.Children);

        var inner = outer.Children[0];
        Assert.Equal("Inner", inner.Name);
        Assert.True(inner.GrossTotalMs >= 5.0);

        // Outer's net time should be approximately the gross time minus inner's gross time
        Assert.True(outer.NetAverageMs >= 5.0);

        PerfGather.ResetAll();
    }

    [Fact]
    public void Current_ReturnsActiveGatherer()
    {
        PerfGather.ResetAll();

        Assert.Null(PerfGather.Current);

        var gather = PerfGather.GetOrCreate("Outer");
        gather.Start();

        Assert.Same(gather, PerfGather.Current);

        gather.Stop();

        Assert.Null(PerfGather.Current);

        PerfGather.ResetAll();
    }

    [Fact]
    public void ExportToCsv_GeneratesValidFormat()
    {
        PerfGather.ResetAll();

        PerfGather.Profile("Task1", () => Thread.Sleep(5));
        PerfGather.Profile("Task2", () => Thread.Sleep(5));

        var task1 = PerfGather.GetOrCreate("Task1");
        var csv = task1.ExportToCsv();

        Assert.Contains("Name,GrossAvgMs,NetAvgMs,CallCount", csv);
        Assert.Contains("Task1", csv);

        PerfGather.ResetAll();
    }

    [Fact]
    public void ResetAll_ClearsData()
    {
        PerfGather.Profile("Before", () => Thread.Sleep(1));

        var before = PerfGather.GetOrCreate("Before");
        Assert.True(before.CallCount > 0);

        PerfGather.ResetAll();

        var after = PerfGather.GetOrCreate("After");
        Assert.Equal(0, after.CallCount);
    }

    [Fact]
    public void GrossVsNet_CalculatedCorrectly()
    {
        PerfGather.ResetAll();

        PerfGather.Profile("Parent", () => {
            Thread.Sleep(10);
            PerfGather.Profile("Child", () => {
                Thread.Sleep(5);
            });
        });

        var parent = PerfGather.GetOrCreate("Parent");
        var child = parent.Children[0];

        // Gross should be approximately 15ms (10 + 5)
        // Net should be approximately 10ms (15 - 5)
        Assert.True(parent.GrossTotalMs >= 15.0);
        Assert.True(parent.NetAverageMs >= 8.0 && parent.NetAverageMs <= 12.0);

        PerfGather.ResetAll();
    }
}
