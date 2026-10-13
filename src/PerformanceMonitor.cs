using System;
using System.Diagnostics;
using System.Text;
namespace XLinUI;
internal enum UiWork { Frame, Layouts, NativeScan, NativeCallback, Visibility, Appearance, Top, Chat, Combat, Map, Icons, Count }
internal sealed class PerformanceMonitor
{
    private readonly Sample[] samples=new Sample[(int)UiWork.Count];
    private long frameStarted,lastFrame,allocated;
    internal double LargestFrameGap {get;private set;}
    internal long LargestFrameAllocation {get;private set;}
    internal int Gen0Collections {get;private set;}
    private readonly int initialGen0=GC.CollectionCount(0);
    internal Scope Measure(UiWork work)=>new(this,work);
    internal void BeginFrame()
    {
        var now=Stopwatch.GetTimestamp();if(lastFrame!=0)LargestFrameGap=Math.Max(LargestFrameGap,Stopwatch.GetElapsedTime(lastFrame,now).TotalMilliseconds);
        lastFrame=frameStarted=now;allocated=GC.GetAllocatedBytesForCurrentThread();
    }
    internal void EndFrame()
    {
        Add(UiWork.Frame,Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds);
        LargestFrameAllocation=Math.Max(LargestFrameAllocation,GC.GetAllocatedBytesForCurrentThread()-allocated);
        Gen0Collections=GC.CollectionCount(0)-initialGen0;
    }
    private void Add(UiWork work,double elapsed)
    {
        ref var s=ref samples[(int)work];s.Count++;s.Total+=elapsed;s.Max=Math.Max(s.Max,elapsed);
    }
    internal string Report()
    {
        var b=new StringBuilder("XLinUI 耗时诊断（毫秒，加载以来）\n");
        for(var i=0;i<samples.Length;i++){var s=samples[i];if(s.Count>0)b.AppendLine($"{(UiWork)i}: 平均 {s.Total/s.Count:F3} / 峰值 {s.Max:F3} / 次数 {s.Count}");}
        return b.AppendLine($"绘制回调间隔峰值 {LargestFrameGap:F1} ms · 单帧分配峰值 {LargestFrameAllocation:N0} B\n进程 Gen0 回收 {Gen0Collections} 次（包括其他插件）。间隔反映整帧停顿，不能单独归因于 XLinUI。").ToString();
    }
    private struct Sample {internal long Count;internal double Total,Max;}
    internal readonly struct Scope : IDisposable
    {
        private readonly PerformanceMonitor monitor;private readonly UiWork work;private readonly long start;
        internal Scope(PerformanceMonitor monitor,UiWork work){this.monitor=monitor;this.work=work;start=Stopwatch.GetTimestamp();}
        public void Dispose()=>monitor.Add(work,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
