using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpansionaryEditor
{
    public sealed class PresetScope
    {
        public string Kind, Module, Faction;
        public List<string> UnitKeys = new List<string>();
    }
    public sealed class PresetUnit
    {
        public string Key, Module, Faction;
    }
    public static class PresetScopes
    {
        public static string Kind(BalancePreset preset) => preset.Scope?.Kind ?? "Global";
        public static void Check(BalancePreset preset)
        {
            if (preset.PresetVersion == 1)
            {
                if (preset.Scope != null && preset.Scope.Kind != "Global") throw new FormatException("局部预设必须使用版本 2，不能当作旧版全局方案");
                return;
            }
            var scope = preset.Scope;
            if (preset.PresetVersion != 2 || scope == null || (scope.Kind != "Module" && scope.Kind != "Faction") ||
                string.IsNullOrEmpty(scope.Module) || (scope.Kind == "Faction" && string.IsNullOrEmpty(scope.Faction)))
                throw new FormatException("局部预设缺少有效的 Mod / 派系范围");
            if (scope.UnitKeys == null || scope.UnitKeys.Count == 0 || scope.UnitKeys.Any(string.IsNullOrEmpty) || scope.UnitKeys.Distinct().Count() != scope.UnitKeys.Count)
                throw new FormatException("预设的兵种范围为空或含重复标识");
            if (preset.Settings.Units.Keys.Any(k => !scope.UnitKeys.Contains(k))) throw new FormatException("预设含范围外的兵种规则，不能应用");
        }
        public static BalancePreset Capture(Settings settings, IEnumerable<PresetUnit> catalog, string kind, string module, string faction)
        {
            if (kind == "Global") return new BalancePreset { Settings = EditData.Clone(settings) };
            if (kind != "Module" && kind != "Faction") throw new FormatException("未知预设范围");
            var keys = catalog.Where(e => e.Module == module && (kind == "Module" || e.Faction == faction)).Select(e => e.Key).Distinct().ToList();
            if (keys.Count == 0) throw new FormatException("当前范围没有已加载的兵种");
            var snapshot = EditData.Clone(settings);
            snapshot.Units = snapshot.Units.Where(p => keys.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
            var preset = new BalancePreset { PresetVersion = 2, Settings = snapshot,
                Scope = new PresetScope { Kind = kind, Module = module, Faction = kind == "Faction" ? faction : null, UnitKeys = keys } };
            Check(preset); return preset;
        }
        public static Settings ApplyTo(Settings current, BalancePreset preset, IEnumerable<PresetUnit> catalog)
        {
            Check(preset);
            if (Kind(preset) == "Global") return EditData.Clone(preset.Settings);
            var entries = catalog.ToDictionary(e => e.Key);
            foreach (var key in preset.Scope.UnitKeys)
            {
                if (!entries.TryGetValue(key, out var entry)) throw new FormatException("范围内兵种未加载：" + key);
                if (entry.Module != preset.Scope.Module || (preset.Scope.Kind == "Faction" && entry.Faction != preset.Scope.Faction))
                    throw new FormatException("兵种不属于预设标明的 Mod / 派系：" + key);
            }
            var result = EditData.Clone(current);
            foreach (var key in preset.Scope.UnitKeys) result.Units.Remove(key);
            foreach (var pair in preset.Settings.Units) result.Units[pair.Key] = EditData.Clone(pair.Value);
            // Global enable and all rules outside the captured membership retain their current values.
            return result;
        }
    }
}
