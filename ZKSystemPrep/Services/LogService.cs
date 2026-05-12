using System;
using System.IO;

namespace ZKSystemPrep.Services;

/// <summary>
/// 统一日志服务 - 所有操作通过此类写入日志文件和 UI
/// </summary>
public sealed class LogService
{
    private static readonly object _lock = new();
    private static LogService? _instance;
    private readonly string _logDir;
    private readonly string _appLogPath;

    /// <summary>UI 日志回调，主窗体注册后可实时显示</summary>
    public event Action<string, LogLevel>? OnLog;

    public enum LogLevel { INFO, WARN, ERROR, SUCCESS }

    private LogService()
    {
        _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(_logDir))
            Directory.CreateDirectory(_logDir);
        _appLogPath = Path.Combine(_logDir, "app.log");
    }

    public static LogService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LogService();
                }
            }
            return _instance;
        }
    }

    public string LogDir => _logDir;

    /// <summary>写入 app.log 并触发 UI 回调</summary>
    public void Log(string message, LogLevel level = LogLevel.INFO)
    {
        WriteToFile(_appLogPath, message, level);
        OnLog?.Invoke(message, level);
    }

    /// <summary>写入指定日志文件并触发 UI 回调</summary>
    public void LogTo(string fileName, string message, LogLevel level = LogLevel.INFO)
    {
        var filePath = Path.Combine(_logDir, fileName);
        WriteToFile(filePath, message, level);
        // 同时写入 app.log
        WriteToFile(_appLogPath, message, level);
        OnLog?.Invoke(message, level);
    }

    /// <summary>只写文件不触发 UI（用于批量输出避免刷屏）</summary>
    public void LogToFileOnly(string fileName, string message, LogLevel level = LogLevel.INFO)
    {
        var filePath = Path.Combine(_logDir, fileName);
        WriteToFile(filePath, message, level);
    }

    /// <summary>获取 app.log 最后 N 行</summary>
    public string[] GetLastLines(int count = 100)
    {
        if (!File.Exists(_appLogPath)) return Array.Empty<string>();
        try
        {
            var lines = File.ReadAllLines(_appLogPath);
            if (lines.Length <= count) return lines;
            return lines[^count..];
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void WriteToFile(string filePath, string message, LogLevel level)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            try
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
            catch
            {
                // 文件写入失败时静默处理，避免死循环
            }
        }
    }
}
