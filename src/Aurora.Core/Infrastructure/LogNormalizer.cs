using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.Core.Infrastructure;

/// <summary>
/// 现代版本的 log4j 会把 XML 事件直接写到 stdout；这里把它还原成人类可读的
/// “[HH:mm:ss] [thread/LEVEL]: message” 形式，普通文本原样透传。
/// </summary>
public sealed class LogNormalizer
{
    private readonly StringBuilder _buffer = new();
    private bool _inEvent;

    public IEnumerable<string> Feed(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            yield break;
        }

        if (!_inEvent && line.Contains("<log4j:Event", StringComparison.Ordinal))
        {
            _inEvent = true;
            _buffer.Clear();
        }

        if (_inEvent)
        {
            _buffer.AppendLine(line);
            if (line.Contains("</log4j:Event>", StringComparison.Ordinal))
            {
                _inEvent = false;
                var raw = _buffer.ToString();
                _buffer.Clear();
                yield return Format(raw);
            }

            yield break;
        }

        yield return line;
    }

    /// <summary>进程结束时把未闭合的缓冲吐出来，避免丢日志。</summary>
    public IEnumerable<string> Flush()
    {
        if (_inEvent && _buffer.Length > 0)
        {
            _inEvent = false;
            var raw = _buffer.ToString();
            _buffer.Clear();
            yield return Format(raw);
        }
    }

    private static string Format(string xml)
    {
        var level = Match(xml, "level=\"([A-Z]+)\"");
        var thread = Match(xml, "thread=\"([^\"]*)\"");
        var logger = Match(xml, "logger=\"([^\"]*)\"");
        var message = Cdata(xml, "log4j:Message");
        var throwable = Cdata(xml, "log4j:Throwable");

        var sb = new StringBuilder();
        sb.Append('[').Append(thread ?? "?").Append('/').Append(level ?? "INFO").Append("]: ");
        if (!string.IsNullOrEmpty(logger) && level is "ERROR" or "WARN")
        {
            sb.Append('(').Append(logger).Append(") ");
        }

        sb.Append(string.IsNullOrWhiteSpace(message) ? "(空消息)" : message.Trim());

        if (!string.IsNullOrWhiteSpace(throwable))
        {
            var lines = throwable.Trim().Split('\n');
            foreach (var l in lines.Take(8))
            {
                sb.Append('\n').Append("    ").Append(l.TrimEnd());
            }
        }

        return sb.ToString();
    }

    private static string? Match(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? Cdata(string text, string tag)
    {
        var m = Regex.Match(text, "<" + tag + "><!\\[CDATA\\[(.*?)\\]\\]></" + tag + ">", RegexOptions.Singleline);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }

        m = Regex.Match(text, "<" + tag + ">(.*?)</" + tag + ">", RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : null;
    }
}
