using UnityEngine;
using System.IO;
using System;

/// <summary>
/// 游戏日志服务 — 集中管理日志输出，支持文件写入和总开关
/// </summary>
public class GameLogger
{
    /// <summary>
    /// 总开关：设为 false 时所有日志方法直接返回
    /// </summary>
    public static bool Enabled = true;

    private static StreamWriter _writer;
    private static string _logFilePath;
    private static int _flushCounter;
    private const int FLUSH_INTERVAL = 60; // 每 60 条日志刷盘一次

    /// <summary>
    /// 初始化日志系统，创建日志文件
    /// </summary>
    public static void Initialize()
    {
        if (!Enabled) return;

        try
        {
            string logDir = Application.persistentDataPath + "/Logs";
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDir, $"game_{timestamp}.log");

            _writer = new StreamWriter(_logFilePath, false, System.Text.Encoding.UTF8)
            {
                AutoFlush = false
            };

            _flushCounter = 0;

            // 写入文件头
            _writer.WriteLine($"=== GameLog {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer.Flush();

            Debug.Log($"[GameLogger] 日志文件: {_logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameLogger] 初始化失败: {e.Message}");
            _writer = null;
        }
    }

    /// <summary>
    /// 关闭日志系统，刷盘并释放资源
    /// </summary>
    public static void Shutdown()
    {
        if (_writer != null)
        {
            try
            {
                _writer.WriteLine($"=== GameLog End {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _writer.Flush();
                _writer.Close();
                _writer = null;
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// 立即刷盘
    /// </summary>
    public static void Flush()
    {
        if (_writer != null)
        {
            try { _writer.Flush(); } catch (Exception) { }
        }
    }

    public static void Log(string tag, string msg)
    {
        if (!Enabled) return;
        string line = FormatLine(tag, msg);
        Debug.Log(line);
        WriteToFile(line);
    }

    public static void LogWarning(string tag, string msg)
    {
        if (!Enabled) return;
        string line = FormatLine(tag, msg);
        Debug.LogWarning(line);
        WriteToFile("[W]" + line);
    }

    public static void LogError(string tag, string msg)
    {
        if (!Enabled) return;
        string line = FormatLine(tag, msg);
        Debug.LogError(line);
        WriteToFile("[E]" + line);
    }

    private static string FormatLine(string tag, string msg)
    {
        return $"[{DateTime.Now:HH:mm:ss.fff}][{tag}] {msg}";
    }

    private static void WriteToFile(string line)
    {
        if (_writer == null) return;

        try
        {
            _writer.WriteLine(line);
            _flushCounter++;

            if (_flushCounter >= FLUSH_INTERVAL)
            {
                _writer.Flush();
                _flushCounter = 0;
            }
        }
        catch (Exception) { }
    }
}
