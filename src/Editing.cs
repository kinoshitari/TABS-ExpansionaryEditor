using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using Landfall.TABS;
using Newtonsoft.Json;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        EditRepository Repository => new EditRepository(DataDirectory);
        string PresetDirectory => Path.Combine(DataDirectory, "平衡方案");
        readonly string[] batchFields = { "price", "_healthMultiplier", "_damageMultiplaier", "_attackSpeedMultiplier", "movementSpeedMuiltiplier" };
        readonly string[] batchLabels = { "价格", "生命值倍率", "伤害倍率", "攻击速度倍率", "移动速度倍率" };
        sealed class BatchRow { public string Name, Before, After; }
        List<BatchRow> batchRows;
        Settings batchBefore, batchCandidate;
        int batchOperation, batchBasis;
        BalancePreset importCandidate;
        BalancePreset pendingPresetValue;
        Settings importPreview, importBefore;
        int presetScopeChoice;
        readonly List<string> importErrors = new List<string>(), importWarnings = new List<string>();
        string importPath = "", presetName = "好友对战", presetMessage = "", pendingPresetPath, pendingRenamePath;
        List<string> presetFiles = new List<string>();
        sealed class PresetFile { public string Path, Name, Kind, Scope, Error; }
        List<PresetFile> presetLibrary = new List<PresetFile>();
        List<EditHistory> historyRows = new List<EditHistory>();
        EditHistory pendingHistory;

        void SaveTransaction(Settings next, string title)
        {
            if (fileError) throw new IOException("设置文件读取失败，已禁止覆盖。请修正后重新读取。");
            var before = Settings;
            var keys = new HashSet<string>(before.Units.Keys.Union(next.Units.Keys).Where(k =>
                !EditData.Same(before.Units.TryGetValue(k, out var a) ? a : null, next.Units.TryGetValue(k, out var b) ? b : null)));
            var affected = Entries.Where(e => keys.Contains(e.Key) || before.Enabled != next.Enabled).ToList();
            string warning = Repository.Commit(before, next, title, value => {
                foreach (var key in keys)
                    if (value.Units.ContainsKey(key) && !ByKey.ContainsKey(key))
                        throw new FormatException("当前未加载兵种，无法应用：" + key);
                foreach (var entry in affected) { value.Units.TryGetValue(entry.Key, out var rule); Validate(entry, rule); }
            }, value => {
                Settings = value;
                var failures=new List<Exception>();
                foreach (var entry in affected)
                {
                    value.Units.TryGetValue(entry.Key, out var rule);
                    try { Apply(entry, value.Enabled ? rule : null); }
                    catch(Exception e) { if(!ReferenceEquals(value,before))throw;failures.Add(e); }
                }
                if(failures.Count>0)throw new AggregateException("部分兵种运行覆盖未能恢复",failures);
            });
            InvalidateBatch();
            Status = string.IsNullOrEmpty(warning) ? title + " · 价格立即生效，其余请重新摆放／重开关卡" : warning;
        }
        UnitRule NormalizedRule(UnitEntry entry, UnitRule source)
        {
            if (source == null) return null;
            var result = EditData.Clone(source); Validate(entry, result);
            foreach (var key in result.Stats.Keys.ToArray()) result.Stats[key] = Numbers.Text(Numbers.Parse(result.Stats[key], AccessTools.Field(typeof(UnitBlueprint), key).FieldType));
            foreach (var key in result.Components.Keys.ToArray()) result.Components[key] = Numbers.Text(Numbers.Parse(result.Components[key], entry.Slots.First(s => s.Key == key).Type));
            foreach (var key in result.Constants.Keys.ToArray()) result.Constants[key] = Numbers.Text(Numbers.Parse(result.Constants[key], entry.Constants.First(s => s.Key == key).Double ? typeof(double) : typeof(float)));
            return result;
        }
        void SaveDraft()
        {
            if (selected == null || !dirty || !string.IsNullOrEmpty(UnityEngine.Input.compositionString)) return;
            var candidate = EditData.Clone(draft);
            if (candidate.Price.HasValue && priceInput != null) candidate.Price = (int)Numbers.Parse(priceInput, typeof(int));
            Commit(selected, candidate); Select(selected);
        }
        void ToggleOverrides()
        {
            if (dirty) throw new FormatException("先保存或放弃草稿，再切换补丁状态");
            var next = EditData.Clone(Settings); next.Enabled = !next.Enabled;
            SaveTransaction(next, next.Enabled ? "启用全部覆盖" : "停用全部覆盖");
        }
        void RestoreKeys(IEnumerable<string> keys, string title)
        {
            var next = EditData.Clone(Settings);
            foreach (var key in keys.ToArray()) next.Units.Remove(key);
            SaveTransaction(next, title); Select(selected);
        }
        void InvalidateBatch() { batchRows = null; batchBefore = batchCandidate = null; }
        void PrepareBatch()
        {
            InvalidateBatch();
            if (dirty) throw new FormatException("先保存或放弃当前草稿，再预览批量调整");
            double operand = (double)Numbers.Parse(bulkFactor, typeof(double));
            var targets = Entries.Where(e => batch.Contains(e.Key)).ToList();
            if (targets.Count == 0) throw new FormatException("请先在左侧勾选兵种");
            var next = EditData.Clone(Settings); var rows = new List<BatchRow>(); string field = batchFields[bulkKind];
            foreach (var entry in targets)
            {
                next.Units.TryGetValue(entry.Key, out var rule); rule = rule ?? new UnitRule();
                double original = bulkKind == 0 ? entry.OriginalPrice : Convert.ToDouble(EffectiveStat(entry, field));
                double saved = bulkKind == 0 ? rule.Price ?? original : rule.Stats.TryGetValue(field, out var raw) ? (double)Numbers.Parse(raw, typeof(double)) : original;
                double result = Numbers.Batch(batchBasis == 0 ? original : saved, operand, batchOperation);
                if (bulkKind == 0) { if(result<0)throw new FormatException(Labels.UnitName(entry)+"：价格不能为负数");rule.Price = checked((int)Math.Round(result)); result = rule.Price.Value; }
                else rule.Stats[field] = Numbers.Text(result);
                next.Units[entry.Key] = NormalizedRule(entry, rule);
                rows.Add(new BatchRow { Name = Labels.UnitName(entry), Before = Numbers.Text(saved), After = Numbers.Text(result) });
            }
            batchBefore = EditData.Clone(Settings); batchCandidate = next; batchRows = rows;
        }
        void ApplyBatch()
        {
            if (dirty || batchCandidate == null || !EditData.Same(Settings, batchBefore)) { InvalidateBatch(); throw new InvalidOperationException("预览已失效，请重新预览"); }
            int count = batchRows.Count; SaveTransaction(batchCandidate, "批量调整" + batchLabels[bulkKind] + " · " + count + " 个兵种"); Select(selected);
        }
        void RefreshLibrary()
        {
            presetFiles = Directory.Exists(PresetDirectory) ? Directory.GetFiles(PresetDirectory, "*.json",SearchOption.AllDirectories).OrderBy(Path.GetFileName, StringComparer.CurrentCulture).ToList() : new List<string>();
            presetLibrary=new List<PresetFile>();
            foreach(var path in presetFiles)
            {
                var file=new PresetFile{Path=path,Name=Path.GetFileNameWithoutExtension(path)};
                try { var preset=EditData.ReadPreset(File.ReadAllText(path),file.Name);file.Kind=PresetScopes.Kind(preset);file.Scope=ScopeLabel(preset); }
                catch(Exception e){file.Kind="Invalid";file.Error=e.Message;}
                presetLibrary.Add(file);
            }
            historyRows = Repository.History();
        }
        IEnumerable<PresetUnit> PresetCatalog()=>Entries.Select(e=>new PresetUnit{Key=e.Key,Module=e.Module,Faction=e.Faction});
        string ScopeLabel(BalancePreset preset)=>PresetScopes.Kind(preset)=="Global"?"全局":SourceName(preset.Scope.Module)+(preset.Scope.Kind=="Faction"?" / "+Labels.Faction(preset.Scope.Faction):" / 整个 Mod");
        BalancePreset CapturePreset()
        {
            if(dirty)throw new FormatException("先保存或放弃草稿，再另存预设");
            if(presetScopeChoice==2&&selected==null)throw new FormatException("先在左侧选择该派系的一个兵种");
            var preset=PresetScopes.Capture(Settings,PresetCatalog(),new[]{"Global","Module","Faction"}[presetScopeChoice],moduleFilter,selected?.Faction);
            preset.EditorVersion=Info.Metadata.Version.ToString();preset.Modules=ModuleVersions();
            if(preset.Scope!=null)
            {
                var allowed=preset.Scope.Module=="Expansionary"?new[]{"GeeztJeez.Expansionary"}:preset.Scope.Module=="HiddenUnits"?new[]{"teamgrad.hiddenunits","teamgrad.core"}:preset.Scope.Module=="ChouChou"?new[]{"chouchou","dark","rwdseerfd","ChouChouMap"}:preset.Scope.Module=="Holiday"?new[]{"teamgrad.holiday","teamgrad.core"}:preset.Scope.Module=="AnimalKingdom"?new[]{"teamgrad.animalkingdom"}:new string[0];
                preset.Modules=preset.Modules.Where(p=>allowed.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);
            }
            return preset;
        }
        string ScopedPresetPath(BalancePreset preset,string name)
        {
            string directory=PresetDirectory;
            if(PresetScopes.Kind(preset)=="Global")directory=Path.Combine(directory,"全局");
            else if(preset.Scope.Kind=="Module")directory=Path.Combine(directory,"Mod","Mod-"+Uri.EscapeDataString(preset.Scope.Module));
            else directory=Path.Combine(directory,"派系","Mod-"+Uri.EscapeDataString(preset.Scope.Module),"派系-"+Uri.EscapeDataString(preset.Scope.Faction));
            return EditData.PresetPath(directory,name);
        }
        Dictionary<string,string> ModuleVersions() => Chainloader.PluginInfos.Values.Where(p => new[] {
            "GeeztJeez.Expansionary", "teamgrad.hiddenunits", "teamgrad.core", "chouchou", "dark", "rwdseerfd", "ChouChouMap", "teamgrad.holiday", "teamgrad.animalkingdom"
        }.Contains(p.Metadata.GUID)).ToDictionary(p => p.Metadata.GUID, p => p.Metadata.Version.ToString());
        void SavePreset(string path)
        {
            if (dirty) throw new FormatException("先保存或放弃草稿，再另存方案");
            var preset = pendingPresetValue ?? CapturePreset();
            preset.Name = Path.GetFileNameWithoutExtension(path);
            EditData.AtomicWrite(path, JsonConvert.SerializeObject(preset, Formatting.Indented), true);
            pendingPresetValue = null;
            presetMessage = "已另存「" + preset.Name + "」；将此 JSON 发给朋友即可。"; RefreshLibrary();
        }
        void PreviewPreset(string path)
        {
            importCandidate = null; importPreview=importBefore=null; importErrors.Clear(); importWarnings.Clear();
            if (dirty) throw new FormatException("先保存或放弃草稿，再预览切换方案");
            var preset = EditData.ReadPreset(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            try { importPreview=PresetScopes.ApplyTo(Settings,preset,PresetCatalog());importBefore=EditData.Clone(Settings); }
            catch(Exception e){importErrors.Add(e.Message);}
            var versions = ModuleVersions();
            if (!string.IsNullOrEmpty(preset.EditorVersion) && preset.EditorVersion != Info.Metadata.Version.ToString()) importWarnings.Add("编辑器版本：方案 " + preset.EditorVersion + " / 当前 " + Info.Metadata.Version);
            foreach (var pair in preset.Modules)
                if (!versions.TryGetValue(pair.Key, out var version)) importWarnings.Add("当前未加载模组：" + pair.Key);
                else if (version != pair.Value) importWarnings.Add(pair.Key + "：方案 " + pair.Value + " / 当前 " + version);
            foreach (var pair in preset.Settings.Units)
            {
                if (!ByKey.TryGetValue(pair.Key, out var entry)) { importErrors.Add("未加载兵种：" + pair.Key); continue; }
                try { Validate(entry, pair.Value); } catch (Exception e) { importErrors.Add(Labels.UnitName(entry) + "：" + e.Message); }
            }
            importCandidate = preset;
        }
        void ActivatePreset()
        {
            if (dirty || importCandidate == null || importErrors.Count != 0) throw new InvalidOperationException("方案仍有未解决项，不能激活");
            if(importPreview==null||!EditData.Same(Settings,importBefore))throw new InvalidOperationException("当前设置已变化，请重新读取预设预览");
            var next=PresetScopes.ApplyTo(Settings,importCandidate,PresetCatalog());
            SaveTransaction(next, "应用"+ScopeLabel(importCandidate)+"预设「" + importCandidate.Name + "」");
            importCandidate = null; importPreview=importBefore=null; Select(selected); RefreshLibrary();
        }
        void RestoreHistory()
        {
            if (dirty) throw new FormatException("先保存或放弃草稿，再恢复历史");
            if (pendingHistory == null) throw new InvalidOperationException("请选择历史记录");
            SaveTransaction(EditData.Clone(pendingHistory.Before), "回到「" + pendingHistory.Title + "」之前");
            pendingHistory = null; Select(selected); RefreshLibrary();
        }
        void RequestUndo()
        {
            if(dirty)throw new FormatException("先保存或放弃草稿，再撤销保存");
            RefreshLibrary();
            pendingHistory=historyRows.FirstOrDefault(h=>!h.Title.StartsWith("回到「",StringComparison.Ordinal)&&EditData.Same(h.After,Settings));
            if(pendingHistory==null)throw new InvalidOperationException("最近记录中没有与当前设置匹配的保存；可按下方历史逐项恢复");
            confirm="history";
        }
    }
}
