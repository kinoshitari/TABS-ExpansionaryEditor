using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExpansionaryEditor
{
    public sealed class Settings
    {
        public int FormatVersion = 1;
        public bool Enabled = true;
        public Dictionary<string, UnitRule> Units = new Dictionary<string, UnitRule>();
    }

    public sealed class UnitRule
    {
        public int? Price;
        public Dictionary<string, string> Stats = new Dictionary<string, string>();
        public Dictionary<string, string> Components = new Dictionary<string, string>();
        public Dictionary<string, string> Constants = new Dictionary<string, string>();
        [JsonIgnore] public bool IsEmpty => Price == null && Stats.Count == 0 && Components.Count == 0 && Constants.Count == 0;
    }

    public static class Numbers
    {
        public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        public static string Text(object value)
        {
            if (value is float f) return f.ToString("R", Culture);
            if (value is double d) return d.ToString("R", Culture);
            if (value is bool b) return b ? "true" : "false";
            return Convert.ToString(value, Culture);
        }
        public static string Normalize(string text)
        {
            var result = new StringBuilder();
            foreach (char c in (text ?? "").Trim())
                if (c >= '０' && c <= '９') result.Append((char)('0' + c - '０'));
                else if (c == '．') result.Append('.');
                else if (c == '－' || c == '−') result.Append('-');
                else if (c == '＋') result.Append('+');
                else if (c == 'ｅ') result.Append('e');
                else if (c == 'Ｅ') result.Append('E');
                else result.Append(c);
            return result.ToString();
        }
        public static object Parse(string text, Type type)
        {
            if (type == typeof(bool)) return bool.Parse(text);
            double number;
            if (!double.TryParse(Normalize(text), NumberStyles.Float, Culture, out number))
                throw new FormatException("请输入数字，小数使用 .；不支持逗号分隔");
            if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("请输入有限数字");
            if (type == typeof(float))
            {
                if (Math.Abs(number) > float.MaxValue) throw new FormatException("数值过大");
                return (float)number;
            }
            if (type == typeof(double)) return number;
            if (Math.Truncate(number) != number) throw new FormatException("这里需要整数");
            return Convert.ChangeType(number, type, Culture);
        }
        public static bool Scalar(Type t) => t == typeof(float) || t == typeof(double) || t == typeof(int) || t == typeof(uint) || t == typeof(bool);
        public static double Batch(double basis, double operand, int operation)
        {
            if (operation == 0 && operand <= 0) throw new FormatException("倍率必须大于 0");
            double result = operation == 0 ? basis * operand : operation == 1 ? operand : basis + operand;
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new FormatException("计算结果超出范围");
            return result;
        }
    }

    public sealed class RuleChange
    {
        public string Group, Key, Before, After;
    }
    public sealed class EditHistory
    {
        public string Id = Guid.NewGuid().ToString("N");
        public DateTime Time = DateTime.Now;
        public string Title;
        public int Count;
        public Settings Before, After;
    }
    public sealed class BalancePreset
    {
        public int PresetVersion = 1;
        public string Name, EditorVersion;
        public DateTime SavedAt = DateTime.Now;
        public Dictionary<string, string> Modules = new Dictionary<string, string>();
        public Settings Settings;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public PresetScope Scope;
    }

    public static class EditData
    {
        public static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        public static bool Same(object a, object b) => JToken.DeepEquals(a == null ? JValue.CreateNull() : JToken.FromObject(a), b == null ? JValue.CreateNull() : JToken.FromObject(b));
        public static void Check(Settings value, bool strict = true)
        {
            if (value == null || value.FormatVersion != 1 || value.Units == null) throw new FormatException("不支持的设置格式");
            foreach (var pair in value.Units)
                if (pair.Value == null || pair.Value.Stats == null || pair.Value.Components == null || pair.Value.Constants == null ||
                    (strict && (string.IsNullOrEmpty(pair.Key) || pair.Value.Stats.Values.Any(v => v == null) || pair.Value.Components.Values.Any(v => v == null) || pair.Value.Constants.Values.Any(v => v == null))))
                    throw new FormatException("兵种标识和参数不能为空");
        }
        public static Settings ReadSettings(string text, bool requireEnvelope = true)
        {
            var obj = JObject.Parse(text);
            if (requireEnvelope && (obj["FormatVersion"] == null || obj["Units"] == null)) throw new FormatException("需要包含 FormatVersion 和 Units 的数值设置");
            var value = obj.ToObject<Settings>(); Check(value, requireEnvelope); return value;
        }
        public static BalancePreset ReadPreset(string text, string name)
        {
            var obj = JObject.Parse(text);
            if (obj["PresetVersion"] == null) return new BalancePreset { Name = name, Settings = ReadSettings(text) };
            var result = obj.ToObject<BalancePreset>();
            if ((result.PresetVersion != 1 && result.PresetVersion != 2) || result.Modules == null) throw new FormatException("不支持的方案格式");
            // Require the same complete envelope as a plain settings file.
            result.Settings = ReadSettings((obj["Settings"] ?? throw new FormatException("方案缺少数值设置")).ToString());
            PresetScopes.Check(result);
            if (string.IsNullOrWhiteSpace(result.Name)) result.Name = name;
            return result;
        }
        public static List<RuleChange> Changes(UnitRule before, UnitRule after)
        {
            before = before ?? new UnitRule(); after = after ?? new UnitRule();
            var result = new List<RuleChange>();
            if (before.Price != after.Price) result.Add(new RuleChange { Group = "价格", Key = "price", Before = before.Price?.ToString(), After = after.Price?.ToString() });
            AddChanges(result, "基础", before.Stats, after.Stats);
            AddChanges(result, "参数", before.Components, after.Components);
            AddChanges(result, "技能", before.Constants, after.Constants);
            return result;
        }
        static void AddChanges(List<RuleChange> result, string group, Dictionary<string,string> a, Dictionary<string,string> b)
        {
            foreach (string key in a.Keys.Union(b.Keys))
            {
                a.TryGetValue(key, out var old); b.TryGetValue(key, out var next);
                if (old != next) result.Add(new RuleChange { Group = group, Key = key, Before = old, After = next });
            }
        }
        public static int ChangedUnits(Settings a, Settings b) => a.Units.Keys.Union(b.Units.Keys).Count(k =>
            !Same(a.Units.TryGetValue(k, out var x) ? x : null, b.Units.TryGetValue(k, out var y) ? y : null));
        public static void AtomicWrite(string path, string contents, bool backup = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, backup ? path + ".bak" : null);
            else File.Move(temp, path);
        }
        public static string PresetPath(string directory, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" "))
                throw new FormatException("方案名需为 1–80 个字符，不能包含路径或文件名禁用字符");
            return Path.Combine(directory, name + ".json");
        }
    }

    // The small journal covers the two-file boundary without changing the existing settings format.
    // A pending record is history only when the on-disk settings equal its successful result.
    public sealed class EditRepository
    {
        readonly string path, historyPath, pendingPath;
        public EditRepository(string directory)
        {
            path = Path.Combine(directory, "数值设置.json");
            historyPath = Path.Combine(directory, "编辑历史.json");
            pendingPath = Path.Combine(directory, "编辑历史.pending.json");
        }
        List<EditHistory> ReadLog()
        {
            if (!File.Exists(historyPath)) return new List<EditHistory>();
            var result = JsonConvert.DeserializeObject<List<EditHistory>>(File.ReadAllText(historyPath));
            if (result == null || result.Any(x => x == null || x.Before == null || x.After == null)) throw new FormatException("历史文件损坏，请保留文件后修复");
            return result;
        }
        EditHistory Pending()
        {
            if (!File.Exists(pendingPath)) return null;
            var record = JsonConvert.DeserializeObject<EditHistory>(File.ReadAllText(pendingPath));
            if (record == null || record.Before == null || record.After == null) throw new FormatException("历史事务文件损坏");
            return record;
        }
        bool IsCommitted(EditHistory record) => File.Exists(path) && EditData.Same(EditData.ReadSettings(File.ReadAllText(path), false), record.After);
        public List<EditHistory> History()
        {
            var list = ReadLog(); var pending = Pending();
            if (pending != null && IsCommitted(pending) && list.All(x => x.Id != pending.Id)) list.Insert(0, pending);
            return list.Take(10).ToList();
        }
        void Recover()
        {
            var pending = Pending(); if (pending == null) return;
            if (IsCommitted(pending)) EditData.AtomicWrite(historyPath, JsonConvert.SerializeObject(History(), Formatting.Indented));
            File.Delete(pendingPath);
        }
        public string Commit(Settings before, Settings next, string title, Action<Settings> validate, Action<Settings> activate)
        {
            EditData.Check(next, false); validate(next);
            if (EditData.Same(before, next)) return "没有需要保存的变化";
            Recover(); var history = ReadLog();
            var record = new EditHistory { Title = title, Count = EditData.ChangedUnits(before, next), Before = EditData.Clone(before), After = EditData.Clone(next) };
            EditData.AtomicWrite(pendingPath, JsonConvert.SerializeObject(record, Formatting.Indented));
            try
            {
                activate(next);
                EditData.AtomicWrite(path, JsonConvert.SerializeObject(next, Formatting.Indented), true);
            }
            catch (Exception failure)
            {
                try { activate(before); }
                catch (Exception rollback) { throw new AggregateException("保存失败且运行覆盖回滚失败，请重启游戏以读取原配置", failure, rollback); }
                // The original main file is unchanged if AtomicWrite failed. The pending record is ignored.
                try { File.Delete(pendingPath); } catch (IOException) { }
                throw;
            }
            history.Insert(0, record);
            try
            {
                EditData.AtomicWrite(historyPath, JsonConvert.SerializeObject(history.Take(10), Formatting.Indented));
                File.Delete(pendingPath);
                return "";
            }
            catch (Exception e)
            {
                // The settings commit succeeded. Keep its journal for History()/Recover(), never report it as a failed save.
                return "数值已保存；历史记录待整理：" + e.Message;
            }
        }
    }
}
