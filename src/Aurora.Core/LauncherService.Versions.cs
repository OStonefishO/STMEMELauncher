using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aurora.Core.Configuration;
using Aurora.Core.Models;

namespace Aurora.Core;

public sealed record RenameOutcome(bool Ok, string Message, string? NewId)
{
    public static RenameOutcome Fail(string message) => new(false, message, null);
    public static RenameOutcome Success(string message, string newId) => new(true, message, newId);
}

/// <summary>版本改名 / 备注：整合包作者常用的两件事。</summary>
public partial class LauncherService
{
    private static readonly Regex IdPattern = new(@"^[A-Za-z0-9._\- ]+$", RegexOptions.Compiled);

    /// <summary>给版本改个名字（同时改文件夹、json/jar 文件名和 json 里的 id）。</summary>
    public RenameOutcome RenameVersion(ResolvedVersion version, string newId)
    {
        newId = (newId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newId))
        {
            return RenameOutcome.Fail("名字不能是空的");
        }

        if (!IdPattern.IsMatch(newId))
        {
            return RenameOutcome.Fail("名字里只能用字母、数字、点、横线、下划线和空格");
        }

        if (string.Equals(newId, version.Id, StringComparison.Ordinal))
        {
            return RenameOutcome.Fail("新名字和原来一样");
        }

        var root = Path.GetDirectoryName(version.Directory);
        if (string.IsNullOrEmpty(root))
        {
            return RenameOutcome.Fail("找不到版本目录");
        }

        var target = Path.Combine(root, newId);
        if (Directory.Exists(target))
        {
            return RenameOutcome.Fail("这个目录里已经有叫「" + newId + "」的版本了");
        }

        try
        {
            Directory.Move(version.Directory, target);
        }
        catch (Exception ex)
        {
            return RenameOutcome.Fail("改不了目录：" + ex.Message);
        }

        try
        {
            RenameIfExists(Path.Combine(target, version.Id + ".json"), Path.Combine(target, newId + ".json"));
            RenameIfExists(Path.Combine(target, version.Id + ".jar"), Path.Combine(target, newId + ".jar"));
            PatchVersionId(Path.Combine(target, newId + ".json"), version.Id, newId);
        }
        catch (Exception ex)
        {
            return RenameOutcome.Fail("目录改了，但文件没改完：" + ex.Message);
        }

        // 配置里所有指向旧 id 的地方一起搬
        if (string.Equals(Settings.SelectedVersionId, version.Id, StringComparison.Ordinal))
        {
            Settings.SelectedVersionId = newId;
        }

        if (Settings.VersionNotes.Remove(version.Id, out var note))
        {
            Settings.VersionNotes[newId] = note;
        }

        if (Settings.IsolatedVersions.Remove(version.Id, out var isolated))
        {
            Settings.IsolatedVersions[newId] = isolated;
        }

        ConfigStore.Save(Settings);

        var inheritors = CountInheritors(root, version.Id);
        return RenameOutcome.Success(
            inheritors > 0
                ? $"已改名成「{newId}」，但有 {inheritors} 个版本继承它，那些版本会起不来，需要手动改 inheritsFrom"
                : $"已改名成「{newId}」",
            newId);
    }

    /// <summary>保存版本备注。</summary>
    public void SetVersionNote(string versionId, string? note)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return;
        }

        note = (note ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(note))
        {
            Settings.VersionNotes.Remove(versionId);
        }
        else
        {
            Settings.VersionNotes[versionId] = note;
        }

        ConfigStore.Save(Settings);
    }

    public string? VersionNote(string versionId) =>
        Settings.VersionNotes.TryGetValue(versionId, out var note) ? note : null;

    private static void RenameIfExists(string from, string to)
    {
        if (File.Exists(from) && !File.Exists(to))
        {
            File.Move(from, to);
        }
    }

    private static void PatchVersionId(string jsonPath, string oldId, string newId)
    {
        if (!File.Exists(jsonPath))
        {
            return;
        }

        var text = File.ReadAllText(jsonPath);
        var replaced = Regex.Replace(
            text,
            "\"id\"\\s*:\\s*\"" + Regex.Escape(oldId) + "\"",
            "\"id\": \"" + newId + "\"",
            RegexOptions.IgnoreCase);

        if (!string.Equals(text, replaced, StringComparison.Ordinal))
        {
            File.WriteAllText(jsonPath, replaced);
        }
    }

    /// <summary>数一下有多少个版本的 inheritsFrom 指向这个 id。</summary>
    private static int CountInheritors(string versionsRoot, string id)
    {
        var count = 0;
        try
        {
            foreach (var json in Directory.GetFiles(versionsRoot, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(json));
                    if (doc.RootElement.TryGetProperty("inheritsFrom", out var parent) &&
                        string.Equals(parent.GetString(), id, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
                catch
                {
                    // 坏 json 跳过
                }
            }
        }
        catch
        {
            // 忽略
        }

        return count;
    }
}
