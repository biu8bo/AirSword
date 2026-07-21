using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;

namespace LingXuZhi.App.Diagnostics;

/// <summary>
/// 全局异常捕获:XAML 未处理异常 / AppDomain 未处理异常 / 未观察的 Task 异常。
/// 统一写 crash.log(含环境信息与完整异常链),并回调到 UI 日志面板。
/// </summary>
public static class GlobalExceptionHandler
{
    private static readonly string CrashLogPath =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    private static readonly object WriteLock = new();

    /// <summary>UI 日志回调(线程安全实现方负责调度),由 MainViewModel 注册。</summary>
    public static Action<string>? OnExceptionLogged { get; set; }

    public static void Register(Application app)
    {
        // XAML 线程异常:记录后标记已处理,非致命异常不闪退
        app.UnhandledException += (_, e) =>
        {
            Handle("XAML UnhandledException", e.Exception);
            e.Handled = true;
        };

        // 非 UI 线程未处理异常:进程即将终止,尽力落盘
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Handle("AppDomain UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Handle("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>手动上报已捕获但需要留痕的异常。</summary>
    public static void Report(string source, Exception ex) => Handle(source, ex);

    private static void Handle(string source, Exception? ex)
    {
        var message = $"[{source}] {ex?.GetType().Name}: {ex?.Message ?? "(null)"}";

        try
        {
            OnExceptionLogged?.Invoke(message);
        }
        catch
        {
            // UI 日志失败不影响落盘
        }

        try
        {
            lock (WriteLock)
                File.AppendAllText(CrashLogPath, BuildEntry(source, ex));
        }
        catch
        {
            // 崩溃记录失败时无能为力,避免二次异常
        }
    }

    private static string BuildEntry(string source, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"================ {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ================");
        sb.AppendLine($"Source   : {source}");
        sb.AppendLine($"OS       : {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($"Runtime  : {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Process  : PID {Environment.ProcessId}, x{(Environment.Is64BitProcess ? "64" : "86")}, WorkingSet {Environment.WorkingSet / 1024 / 1024} MB");
        sb.AppendLine($"BaseDir  : {AppContext.BaseDirectory}");

        var depth = 0;
        for (var current = ex; current is not null; current = current.InnerException, depth++)
        {
            sb.AppendLine(depth == 0 ? "Exception:" : $"Inner[{depth}]:");
            sb.AppendLine($"  Type   : {current.GetType().FullName}");
            sb.AppendLine($"  Message: {current.Message}");
            sb.AppendLine($"  Stack  : {current.StackTrace}");
        }

        if (ex is null)
            sb.AppendLine("Exception: (null)");

        sb.AppendLine();
        return sb.ToString();
    }
}
