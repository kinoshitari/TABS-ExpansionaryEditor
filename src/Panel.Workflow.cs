using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        string ChangeLabel(RuleChange change)
        {
            if(change.Key=="price")return "价格";
            if(change.Group=="抗性")return ResistanceLabel(change.Key);
            if(change.Group=="基础")return Labels.Stats.FirstOrDefault(d=>d.Field==change.Key)?.Label??change.Key;
            return selected?.Slots?.FirstOrDefault(s=>s.Key==change.Key)?.Label??selected?.Constants?.FirstOrDefault(s=>s.Key==change.Key)?.Label??"旧版参数 · "+change.Key;
        }
        string Baseline(RuleChange change)
        {
            if(change.Key=="price")return selected.OriginalPrice.ToString();
            if(change.Group=="抗性")return "1";
            if(change.Group=="基础"&&selected.OriginalStats.ContainsKey(change.Key))return Numbers.Text(EffectiveStat(selected,change.Key));
            return selected.Slots?.FirstOrDefault(s=>s.Key==change.Key)?.Original??selected.Constants?.FirstOrDefault(s=>s.Key==change.Key)?.Original??"加载时基准";
        }
        void DrawSummary()
        {
            Settings.Units.TryGetValue(selected.Key,out var saved);var changes=EditData.Changes(saved,draft);
            var price=changes.FirstOrDefault(c=>c.Key=="price");
            if(draft.Price.HasValue&&priceInput!=null)
            {
                if(price==null&&priceInput!=draft.Price.Value.ToString()){price=new RuleChange{Group="价格",Key="price",Before=saved?.Price?.ToString()};changes.Insert(0,price);}
                if(price!=null)price.After=priceInput;
            }
            if(changes.Any(c=>c.Group=="参数"||c.Group=="技能"))LoadSelectedGraph();
            GUILayout.Label("未保存",sectionStyle,GUILayout.Height(44));
            foreach(var change in changes)
            {
                GUILayout.BeginHorizontal();GUILayout.Label(ChangeLabel(change),labelStyle,GUILayout.MinHeight(44));
                GUILayout.Label((change.Before??Baseline(change))+" → "+(change.After??Baseline(change)),subStyle,GUILayout.Width(260),GUILayout.MinHeight(44));GUILayout.EndHorizontal();
            }
            if(changes.Count==0)GUILayout.Label("无",columnStyle);
            GUILayout.Space(24);GUILayout.Label("已保存",sectionStyle,GUILayout.Height(44));
            var existing=EditData.Changes(null,saved);
            if(existing.Any(c=>c.Group=="参数"||c.Group=="技能"))LoadSelectedGraph();
            foreach(var change in existing)
            {GUILayout.BeginHorizontal();GUILayout.Label(ChangeLabel(change),labelStyle,GUILayout.MinHeight(44));GUILayout.Label(change.After,subStyle,GUILayout.Width(160),GUILayout.MinHeight(44));GUILayout.EndHorizontal();}
            if(existing.Count==0)GUILayout.Label("无",columnStyle);
        }
        void DrawBatch()
        {
            GUILayout.Label("已选 "+batch.Count,sectionStyle,GUILayout.Height(44));GUILayout.Space(12);
            int next=GUILayout.SelectionGrid(bulkKind,batchLabels,3,buttonStyle,GUILayout.Height(96));
            if(next!=bulkKind){bulkKind=next;InvalidateBatch();}
            GUILayout.Space(16);int op=GUILayout.Toolbar(batchOperation,new[]{"乘以","设为","增加"},buttonStyle,GUILayout.Height(44));
            if(op!=batchOperation){batchOperation=op;InvalidateBatch();}
            if(batchOperation!=1)
            {
                GUILayout.Space(12);int basis=GUILayout.Toolbar(batchBasis,new[]{"原值","当前值"},buttonStyle,GUILayout.Height(44));
                if(basis!=batchBasis){batchBasis=basis;InvalidateBatch();}
            }
            GUILayout.Space(16);GUILayout.BeginHorizontal();inputOrder.Add("bulk-number");
            Rect numberRect=GUILayoutUtility.GetRect(324,44,GUILayout.ExpandWidth(true));
            string value=DrawNumberInput(numberRect,"bulk-number","batch",bulkFactor,typeof(double),inputStyle);if(value!=bulkFactor){bulkFactor=value;InvalidateBatch();}
            GUILayout.Space(12);if(GUILayout.Button("预览",buttonStyle,GUILayout.Width(104),GUILayout.Height(44)))Guard(PrepareBatch);
            GUILayout.EndHorizontal();
            if(batchRows!=null)
            {
                GUILayout.Space(20);GUILayout.Label("预览 "+batchRows.Count,sectionStyle,GUILayout.Height(44));
                foreach(var row in batchRows)
                {GUILayout.BeginHorizontal();GUILayout.Label(row.Name,labelStyle,GUILayout.MinHeight(44));GUILayout.Label(row.Before+" → "+row.After,subStyle,GUILayout.Width(260),GUILayout.MinHeight(44));GUILayout.EndHorizontal();}
                GUILayout.Space(16);if(GUILayout.Button("应用",primaryStyle,GUILayout.Height(44)))Guard(ApplyBatch);
            }
            GUILayout.Space(24);GUILayout.BeginHorizontal();
            if(GUILayout.Button("还原已选",buttonStyle,GUILayout.Height(44)))confirm="batch-reset";
            GUILayout.Space(12);if(GUILayout.Button("还原 Mod",buttonStyle,GUILayout.Height(44)))confirm="module-reset";
            GUILayout.EndHorizontal();
        }
        void DrawLibrary()
        {
            GUILayout.BeginHorizontal();GUILayout.Label("预设",sectionStyle,GUILayout.Height(44));
            if(GUILayout.Button("撤销",buttonStyle,GUILayout.Width(88),GUILayout.Height(44)))Guard(RequestUndo);
            GUILayout.Space(8);if(GUILayout.Button("刷新",buttonStyle,GUILayout.Width(88),GUILayout.Height(44)))Guard(RefreshLibrary);
            GUILayout.Space(8);if(GUILayout.Button("文件夹",buttonStyle,GUILayout.Width(104),GUILayout.Height(44)))Guard(()=>{Directory.CreateDirectory(PresetDirectory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PresetDirectory){UseShellExecute=true});});
            GUILayout.EndHorizontal();GUILayout.Space(16);
            presetScopeChoice=GUILayout.Toolbar(presetScopeChoice,new[]{"全局","Mod","派系"},buttonStyle,GUILayout.Height(44));
            string context=presetScopeChoice==0?"全局":SourceName(moduleFilter)+(presetScopeChoice==2&&selected!=null?" / "+Labels.Faction(selected.Faction):"");
            GUILayout.Label(context,columnStyle,GUILayout.MinHeight(44));GUILayout.BeginHorizontal();
            GUI.SetNextControlName("preset-name");presetName=GUILayout.TextField(presetName,inputStyle,GUILayout.Height(44));
            GUILayout.Space(12);if(GUILayout.Button("另存",buttonStyle,GUILayout.Width(96),GUILayout.Height(44)))Guard(()=>{
                pendingPresetValue=CapturePreset();pendingPresetPath=ScopedPresetPath(pendingPresetValue,presetName);
                if(File.Exists(pendingPresetPath))confirm="overwrite-preset";else SavePreset(pendingPresetPath);
            });GUILayout.EndHorizontal();
            DrawPresetLibrary("Global","全局");DrawPresetLibrary("Module","Mod");DrawPresetLibrary("Faction","派系");
            foreach(var invalid in presetLibrary.Where(p=>p.Kind=="Invalid"))GUILayout.Label(invalid.Name+" · 无效",labelStyle);
            GUILayout.Space(24);GUILayout.Label("导入",sectionStyle,GUILayout.Height(44));GUILayout.BeginHorizontal();
            GUI.SetNextControlName("import-path");importPath=GUILayout.TextField(importPath,inputStyle,GUILayout.Height(44));
            if(string.IsNullOrEmpty(importPath)&&Event.current.type==EventType.Repaint){var rect=GUILayoutUtility.GetLastRect();rect.x+=14;GUI.Label(rect,"JSON 路径",columnStyle);}
            GUILayout.Space(12);if(GUILayout.Button("导入",buttonStyle,GUILayout.Width(96),GUILayout.Height(44)))Guard(()=>PreviewPreset(importPath.Trim().Trim('"')));
            GUILayout.EndHorizontal();
            if(importCandidate!=null)
            {
                GUILayout.Space(20);GUILayout.Label(importCandidate.Name,sectionStyle,GUILayout.MinHeight(44));
                GUILayout.Label(ScopeLabel(importCandidate),columnStyle,GUILayout.MinHeight(44));
                if(importPreview!=null)
                {
                    GUILayout.Label(EditData.ChangedUnits(Settings,importPreview)+" 个兵种",labelStyle,GUILayout.Height(44));
                    if(PresetScopes.Kind(importCandidate)=="Global")GUILayout.Label((Settings.Enabled?"启用":"停用")+" → "+(importPreview.Enabled?"启用":"停用"),columnStyle,GUILayout.Height(44));
                    foreach(string key in Settings.Units.Keys.Union(importPreview.Units.Keys))
                    {
                        Settings.Units.TryGetValue(key,out var before);importPreview.Units.TryGetValue(key,out var after);int count=EditData.Changes(before,after).Count;
                        if(count==0)continue;
                        GUILayout.BeginHorizontal();GUILayout.Label(ByKey.TryGetValue(key,out var entry)?Labels.UnitName(entry):key,labelStyle,GUILayout.MinHeight(40));
                        GUILayout.Label(after==null?"还原":before==null?"新增":count+" 项",columnStyle,GUILayout.Width(90),GUILayout.MinHeight(40));GUILayout.EndHorizontal();
                    }
                }
                if(importWarnings.Count>0)GUILayout.Label("版本不同",labelStyle,GUILayout.Height(44));
                foreach(var problem in importErrors)GUILayout.Label(problem,labelStyle);
                GUILayout.Space(12);GUILayout.BeginHorizontal();bool enabled=GUI.enabled;GUI.enabled=enabled&&importErrors.Count==0&&importPreview!=null;
                if(GUILayout.Button(importWarnings.Count>0?"仍然应用":"应用",primaryStyle,GUILayout.Height(44)))confirm="activate-preset";
                GUI.enabled=enabled;GUILayout.Space(10);
                if(GUILayout.Button("另存",buttonStyle,GUILayout.Width(96),GUILayout.Height(44)))Guard(()=>{
                    pendingPresetValue=EditData.Clone(importCandidate);pendingPresetPath=ScopedPresetPath(pendingPresetValue,presetName);
                    if(File.Exists(pendingPresetPath))confirm="overwrite-preset";else SavePreset(pendingPresetPath);
                });GUILayout.Space(10);
                if(GUILayout.Button("取消",buttonStyle,GUILayout.Width(96),GUILayout.Height(44))){importCandidate=null;importPreview=importBefore=null;importErrors.Clear();importWarnings.Clear();}
                GUILayout.EndHorizontal();
            }
            if(DrawFold("history","历史",historyRows.Count,0,0,false))
            {
                foreach(var item in historyRows.ToArray())
                {
                    GUILayout.BeginHorizontal();GUILayout.BeginVertical();GUILayout.Label(item.Title,labelStyle,GUILayout.MinHeight(34));
                    GUILayout.Label(item.Time.ToString("MM-dd HH:mm"),columnStyle,GUILayout.MinHeight(34));GUILayout.EndVertical();
                    if(GUILayout.Button("恢复",buttonStyle,GUILayout.Width(96),GUILayout.Height(44)))Guard(()=>{if(dirty)throw new FormatException("请先保存");pendingHistory=item;confirm="history";});
                    GUILayout.EndHorizontal();GUILayout.Space(10);
                }
                if(historyRows.Count==0)GUILayout.Label("无",columnStyle);
            }
            if(DrawFold("manage","管理",3,0,0,false))
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button("重新读取",buttonStyle,GUILayout.Height(44))){if(dirty)confirm="reload";else Guard(()=>{Reload();Select(selected);});}
                GUILayout.Space(12);if(GUILayout.Button("全部还原",buttonStyle,GUILayout.Height(44)))confirm="all";GUILayout.EndHorizontal();
                GUILayout.Space(12);if(GUILayout.Button("还原兵种排序",buttonStyle,GUILayout.Height(44)))confirm="menu-order-reset";
            }
        }
        void DrawPresetLibrary(string kind,string title)
        {
            var files=presetLibrary.Where(p=>p.Kind==kind).ToList();
            if(!DrawFold("preset-library/"+kind,title,files.Count,0,0,files.Count>0))return;
            foreach(var file in files)
            {
                GUILayout.BeginHorizontal();GUILayout.BeginVertical();GUILayout.Label(file.Name,labelStyle,GUILayout.MinHeight(40));
                if(kind!="Global")GUILayout.Label(file.Scope,columnStyle,GUILayout.MinHeight(34));GUILayout.EndVertical();
                if(GUILayout.Button("预览",buttonStyle,GUILayout.Width(88),GUILayout.Height(44)))Guard(()=>PreviewPreset(file.Path));
                GUILayout.Space(8);if(GUILayout.Button("改名",buttonStyle,GUILayout.Width(88),GUILayout.Height(44)))Guard(()=>{
                    pendingRenamePath=file.Path;pendingPresetPath=EditData.PresetPath(Path.GetDirectoryName(file.Path),presetName);
                    if(string.Equals(pendingRenamePath,pendingPresetPath,StringComparison.OrdinalIgnoreCase))throw new FormatException("名称相同");
                    if(File.Exists(pendingPresetPath))throw new IOException("名称已存在");confirm="rename-preset";
                });GUILayout.EndHorizontal();GUILayout.Space(10);
            }
            if(files.Count==0)GUILayout.Label("无",columnStyle);
        }
        string ConfirmQuestion()
        {
            switch(confirm)
            {
                case "module":case "switch":case "reload":return "放弃未保存更改？";
                case "unit":return "还原「"+Labels.UnitName(selected)+"」？";
                case "menu-order-reset":return "还原所有派系内的兵种顺序？";
                case "batch-reset":return "还原已选？\n"+batch.Count+" 个兵种";
                case "module-reset":return "还原 "+SourceName(moduleFilter)+"？\n"+ModuleEntries().Count(e=>Settings.Units.ContainsKey(e.Key))+" 个兵种";
                case "all":return "全部还原？\n"+string.Join(" · ",moduleIds.Select(m=>SourceName(m)+" "+Entries.Count(e=>e.Module==m&&Settings.Units.ContainsKey(e.Key))))+"\n其他 "+Settings.Units.Keys.Count(k=>!ByKey.ContainsKey(k));
                case "history":return "恢复历史？\n"+pendingHistory.Time.ToString("MM-dd HH:mm")+"\n"+EditData.ChangedUnits(Settings,pendingHistory.Before)+" 个兵种 · "+(pendingHistory.Before.Enabled?"启用":"停用");
                case "overwrite-preset":return "覆盖预设？\n"+Path.GetFileNameWithoutExtension(pendingPresetPath);
                case "rename-preset":return "重命名？\n"+Path.GetFileNameWithoutExtension(pendingRenamePath)+" → "+Path.GetFileNameWithoutExtension(pendingPresetPath);
                case "activate-preset":return "应用预设？\n"+importCandidate.Name+"\n"+ScopeLabel(importCandidate)+" · "+EditData.ChangedUnits(Settings,importPreview)+" 个兵种";
                default:return "确认？";
            }
        }
        void DrawConfirm(Rect panel)
        {
            GUI.DrawTexture(panel,modalShade);string question=ConfirmQuestion();float width=Mathf.Min(720,panel.width-64);
            float labelHeight=sectionStyle.CalcHeight(new GUIContent(question),width-64);float height=Mathf.Min(panel.height-64,Mathf.Max(244,labelHeight+140));
            var dialog=new Rect(panel.center.x-width/2,panel.center.y-height/2,width,height);GUI.Box(dialog,GUIContent.none,surfaceStyle);
            GUI.Label(new Rect(dialog.x+32,dialog.y+28,width-64,height-120),question,sectionStyle);
            if(GUI.Button(new Rect(dialog.x+32,dialog.yMax-76,(dialog.width-80)/2,44),"确认",primaryStyle))
            {
                string action=confirm;confirm="";
                Guard(()=>{
                    switch(action)
                    {
                        case "module":SwitchModule(pendingModule);break;
                        case "switch":Select(pendingSelection);break;
                        case "reload":Reload();Select(selected);break;
                        case "unit":RestoreKeys(new[]{selected.Key},"恢复当前兵种");break;
                        case "batch-reset":RestoreKeys(batch,"恢复已勾选兵种");break;
                        case "module-reset":RestoreKeys(ModuleEntries().Select(e=>e.Key),"恢复「"+SourceName(moduleFilter)+"」模块");break;
                        case "all":RestoreKeys(Settings.Units.Keys,"恢复全部来源");break;
                        case "menu-order-reset":MenuOrder.ResetUnits();break;
                        case "history":RestoreHistory();break;
                        case "overwrite-preset":SavePreset(pendingPresetPath);break;
                        case "rename-preset":
                            // Move preserves the exact snapshot; only update its display name after reading.
                            var preset=EditData.ReadPreset(File.ReadAllText(pendingRenamePath),presetName);
                            preset.Name=Path.GetFileNameWithoutExtension(pendingPresetPath);
                            if(File.Exists(pendingPresetPath))throw new IOException("目标方案已存在");
                            EditData.AtomicWrite(pendingPresetPath,Newtonsoft.Json.JsonConvert.SerializeObject(preset,Newtonsoft.Json.Formatting.Indented));
                            File.Delete(pendingRenamePath);RefreshLibrary();break;
                        case "activate-preset":ActivatePreset();break;
                    }
                });
            }
            if(GUI.Button(new Rect(dialog.center.x+8,dialog.yMax-76,(dialog.width-80)/2,44),"取消",buttonStyle))confirm="";
        }
    }
}
