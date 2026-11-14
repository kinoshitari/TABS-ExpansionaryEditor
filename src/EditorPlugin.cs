using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Landfall.TABS;
using Newtonsoft.Json;
using UnityEngine;

namespace ExpansionaryEditor
{
    [BepInPlugin("local.tabs.expansionaryeditor", "TABS 兵种数值编辑器", "1.10.1")]
    [BepInDependency("org.bepinex.plugins.SLMAcore", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("GeeztJeez.Expansionary", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("chouchou", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("dark", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("rwdseerfd", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ChouChouMap", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("teamgrad.core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("teamgrad.hiddenunits", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("teamgrad.holiday", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("teamgrad.animalkingdom", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("monkeyking", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class EditorPlugin : BaseUnityPlugin
    {
        public static EditorPlugin Instance;
        public Settings Settings = new Settings();
        public readonly List<UnitEntry> Entries = new List<UnitEntry>();
        public readonly Dictionary<string,UnitEntry> ByKey = new Dictionary<string,UnitEntry>();
        readonly Dictionary<DatabaseID,string> runtimeIds = new Dictionary<DatabaseID,string>();
        public bool Ready;
        public string Status = "正在加载";
        public string DataDirectory {get;private set;}
        public string SettingsPath => Path.Combine(DataDirectory,"数值设置.json");
        public Harmony Harmony;
        internal NativeMenuOrder MenuOrder;
        
        readonly Dictionary<string,Transform> prefabHosts = new Dictionary<string,Transform>();
        bool fileError;

        void Awake()
        {
            Instance=this;
            InitWindowSettings();
            DataDirectory=Path.Combine(Paths.ConfigPath,"ExpansionaryEditor");
            Directory.CreateDirectory(DataDirectory);
            LoadSettings();
            Harmony=new Harmony("local.tabs.expansionaryeditor");
            MenuOrder=new NativeMenuOrder(this,message=>{error=message;Logger.LogError(message);},message=>Logger.LogInfo(message));
            MenuOrder.Install(Harmony);
            InstallResistances();
            Harmony.Patch(AccessTools.Method(typeof(Localizer),"GetSinglePhrase",new[]{typeof(string),typeof(string[])}),
                postfix:new HarmonyMethod(typeof(EditorPlugin),nameof(TranslateCatalogPhrase)));
            PatchDerivedTranslations();
            InitializeSources();
            Harmony.Patch(AccessTools.Method(typeof(UnitBlueprint),"GetUnitCost",new[]{typeof(bool)}),
                prefix:new HarmonyMethod(typeof(EditorPlugin),nameof(PricePrefix)));
            StartCoroutine(WaitForCatalog());
            Logger.LogInfo("中文编辑器已启动，F6 打开；数据目录："+DataDirectory);
        }
        void PatchDerivedTranslations()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("CN_AllInOne.AllInOnePatchPlugin")).FirstOrDefault(t=>t!=null);
            if(type==null)return;
            foreach(var name in new[]{"TranslateText","TranslateSilent"})
            {
                var method=AccessTools.Method(type,name,new[]{typeof(string)});
                if(method!=null)Harmony.Patch(method,prefix:new HarmonyMethod(typeof(EditorPlugin),nameof(DerivedTranslation)));
            }
        }
        static bool DerivedTranslation(string __0,ref string __result)
        {
            if(!Labels.KnownUnitTranslation(__0,out var text))return true;
            __result=text;return false;
        }
        static IEnumerable<UnitBlueprint> DatabaseBlueprints(DM.LandfallContentDatabase database)
        {return ((System.Collections.IDictionary)AccessTools.Field(typeof(DM.LandfallContentDatabase),"m_unitBlueprints").GetValue(database)).Values.Cast<UnitBlueprint>();}
        static bool PricePrefix(UnitBlueprint __instance,bool __0,ref uint __result)
        {
            var p=Instance;
            if(p==null || !p.Ready || !p.Settings.Enabled)return true;
            if(!p.runtimeIds.TryGetValue(__instance.Entity.GUID,out var stable)||!p.Settings.Units.TryGetValue(stable,out var rule))return true;
            if(rule.Price.HasValue){__result=(uint)rule.Price.Value;return false;}
            // Native automatic cost includes health, size and equipment. Keep non-price
            // edits independent without creating a persisted price override. Native fixed
            // prices and unedited units retain their original pricing path.
            if(rule.IsEmpty||__instance.forceCost!=0||__instance.useCustomCost||!p.ByKey.TryGetValue(stable,out var entry))return true;
            __result=__0?entry.OriginalPrice:entry.OriginalPriceWithoutRiders;return false;
        }
        public static string Key(UnitBlueprint blueprint) => Instance==null?"Expansionary/"+RawName(blueprint):Instance.ResolveKey(blueprint);
        public static object EffectiveStat(UnitEntry entry,string field)
        {
            var value=entry.OriginalStats[field];
            if((field.StartsWith("_") || field=="dragMultiplier") && value is float f && f==0)return 1f;
            return value;
        }
        public void EnsureGraph(UnitEntry entry)
        {
            if(entry.Slots!=null)return;
            var graph=new CombatGraph(entry.Key);
            ScanRoots(entry,graph,false);
            entry.Slots=graph.Slots;entry.Constants=graph.Constants;entry.Abilities=graph.Abilities;
            foreach(var note in graph.Notes.Distinct())Logger.LogWarning(entry.Name+" "+note);
        }
        static GameObject[] ReadProps(UnitBlueprint bp)
        {
            var direct=AccessTools.Field(typeof(UnitBlueprint),"m_props_primary").GetValue(bp) as GameObject[];
            if(direct!=null)return (GameObject[])direct.Clone();
            var ids=AccessTools.Field(typeof(UnitBlueprint),"m_propDatabaseIds").GetValue(bp) as DatabaseID[];
            return ids==null?new GameObject[0]:(GameObject[])bp.m_props.Clone();
        }
        void ScanRoots(UnitEntry entry,CombatGraph graph,bool assign)
        {
            var b=graph.Root(entry.Base,"B","本体／技能");
            var r=graph.Root(entry.Right,"R","右手武器／弹丸");
            var l=graph.Root(entry.Left,"L","左手武器／弹丸");
            var props=entry.Props==null?null:new GameObject[entry.Props.Length];
            if(props!=null)for(int i=0;i<props.Length;i++)props[i]=graph.Root(entry.Props[i],"P"+i,"附加技能／道具 "+(i+1));
            var children=entry.Children==null?null:new GameObject[entry.Children.Length];
            if(children!=null)for(int i=0;i<children.Length;i++)children[i]=graph.Root(entry.Children[i],"C"+i,"主动技能 "+(i+1));
            if(assign)
            {entry.Blueprint.UnitBase=b;entry.Blueprint.RightWeapon=r;entry.Blueprint.LeftWeapon=l;entry.Blueprint.m_props=props;entry.Blueprint.objectsToSpawnAsChildren=children;}
        }
        public void Apply(UnitEntry entry,UnitRule rule)
        {
            var bp=entry.Blueprint;
            Transform newHost=null;
            CombatGraph graph=null;
            if(rule!=null && (rule.Components.Count>0 || rule.Constants.Count>0))
            {
                EnsureGraph(entry);
                var parent=new GameObject("ExpansionaryEditor prefabs: "+entry.Name);
                parent.SetActive(false);DontDestroyOnLoad(parent);
                newHost=parent.transform;
                graph=new CombatGraph(entry.Key,rule,newHost);
                // Stage the complete graph first. No original prefab is modified.
                try
                {
                    ScanRoots(entry,graph,true);
                    ConstantPatcher.Ensure(entry.Constants,rule,Harmony);
                    RuntimeFields.Ensure(entry.Slots,rule,Harmony);
                    var matched=new HashSet<string>(graph.Slots.Select(s=>s.Key));
                    var missing=rule.Components.Keys.Where(k=>!matched.Contains(k)).ToArray();
                    if(missing.Length>0)throw new InvalidOperationException("有 "+missing.Length+" 项参数路径失效，请恢复该兵种后重新设置");
                }
                catch
                {
                    bp.UnitBase=entry.Base;bp.RightWeapon=entry.Right;bp.LeftWeapon=entry.Left;bp.m_props=entry.Props;bp.objectsToSpawnAsChildren=entry.Children;
                    Destroy(parent);throw;
                }
            }
            else {bp.UnitBase=entry.Base;bp.RightWeapon=entry.Right;bp.LeftWeapon=entry.Left;bp.m_props=entry.Props;bp.objectsToSpawnAsChildren=entry.Children;}
            foreach(var pair in entry.OriginalStats)
                AccessTools.Field(typeof(UnitBlueprint),pair.Key).SetValue(bp,pair.Value);
            if(rule!=null)foreach(var pair in rule.Stats)
            {
                var f=AccessTools.Field(typeof(UnitBlueprint),pair.Key);
                f.SetValue(bp,Numbers.Parse(pair.Value,f.FieldType));
            }
            // Previous generations remain inactive until exit: placed units can still reference their projectiles.
            if(newHost)prefabHosts[entry.Key]=newHost;
        }
        public void Validate(UnitEntry entry,UnitRule rule)
        {
            if(rule==null)return;
            if(rule.Price.HasValue && rule.Price.Value<0)throw new FormatException("价格不能为负数");
            foreach(var pair in rule.Stats)
            {
                var def=Labels.Stats.FirstOrDefault(x=>x.Field==pair.Key);
                if(def==null)throw new FormatException("未知基础属性："+pair.Key);
                var type=AccessTools.Field(typeof(UnitBlueprint),pair.Key).FieldType;
                var value=Numbers.Parse(pair.Value,type);
                if(type!=typeof(bool) && Convert.ToDouble(value)<def.Min)throw new FormatException(def.Label+"不能小于 "+def.Min);
            }
            double min=rule.Stats.TryGetValue("minSizeRandom",out var lo)?(double)Numbers.Parse(lo,typeof(double)):Convert.ToDouble(entry.OriginalStats["minSizeRandom"]);
            double max=rule.Stats.TryGetValue("maxSizeRandom",out var hi)?(double)Numbers.Parse(hi,typeof(double)):Convert.ToDouble(entry.OriginalStats["maxSizeRandom"]);
            if(min>max)throw new FormatException("随机体型下限不能大于上限");
            ValidateResistances(rule);
            if(rule.Components.Count>0 || rule.Constants.Count>0)EnsureGraph(entry);
            foreach(var pair in rule.Components)
            {
                var slot=entry.Slots.FirstOrDefault(s=>s.Key==pair.Key);
                if(slot==null)throw new FormatException("参数路径已失效："+pair.Key);
                var value=Numbers.Parse(pair.Value,slot.Type);
                if(slot.Minimum.HasValue&&Convert.ToDouble(value)<slot.Minimum.Value)throw new FormatException(slot.Label+"不能小于 "+Numbers.Text(slot.Minimum.Value));
                string effectIssue=WeaponEffects.RangeIssue(slot,pair.Value,entry.Slots,rule.Components);
                if(effectIssue.Length>0)throw new FormatException(effectIssue);
                if(slot.ComponentType==typeof(DamageMultiplier)&&Convert.ToDouble(value)<0)throw new FormatException("承伤倍率不能为负数");
                if(slot.Type!=typeof(bool)&&slot.Label.Contains("冷却")&&Convert.ToDouble(value)<0)throw new FormatException(slot.Label+"不能为负数");
                if(slot.FieldName=="magSize" && Convert.ToInt32(value)<1)throw new FormatException("弹匣容量至少为 1");
                if(slot.Key.EndsWith("/@count") && (Convert.ToInt32(value)<0 || Convert.ToInt32(value)>128))throw new FormatException("召唤数量需要在 0–128 之间");
            }
            foreach(var pair in rule.Constants)
            {
                var slot=entry.Constants.FirstOrDefault(s=>s.Key==pair.Key);
                if(slot==null)throw new FormatException("技能脚本版本改变，参数已失效："+pair.Key);
                var value=Numbers.Parse(pair.Value,slot.Double?typeof(double):typeof(float));
                if(slot.Readable&&Convert.ToDouble(value)<0)throw new FormatException(slot.Label+"不能为负数");
            }
        }
        public void Commit(UnitEntry entry,UnitRule rule)
        {
            var normalized=NormalizedRule(entry,rule);
            var next=EditData.Clone(Settings);
            if(normalized==null||normalized.IsEmpty)next.Units.Remove(entry.Key);else next.Units[entry.Key]=normalized;
            SaveTransaction(next,"保存「"+Labels.UnitName(entry)+"」");
            Logger.LogInfo("SAVED "+entry.Name+" "+JsonConvert.SerializeObject(normalized));
        }
        public void SaveSettings()
        {
            if(fileError)throw new IOException("设置文件读取失败，已禁止覆盖。请修正后点击重新读取。");
            EditData.AtomicWrite(SettingsPath,JsonConvert.SerializeObject(Settings,Formatting.Indented),true);
        }
        void LoadSettings()
        {
            if(!File.Exists(SettingsPath)){fileError=false;return;}
            try
            {
                // Preserve the old settings reader's defaults; strict envelopes are required only for new imports.
                var loaded=EditData.ReadSettings(File.ReadAllText(SettingsPath),false);
                Settings=loaded;RefreshResistanceRouting();fileError=false;
            }
            catch(Exception e){fileError=true;Logger.LogError("设置未加载，原文件保留："+e.Message);}
        }
        public void Reload()
        {
            var old=Settings;LoadSettings();
            if(fileError)throw new IOException("读取失败，请检查数值设置.json；原文件已保留");
            try
            {
                foreach(var entry in Entries)
                {
                    Settings.Units.TryGetValue(entry.Key,out var rule);Validate(entry,rule);
                }
                foreach(var entry in Entries)
                {Settings.Units.TryGetValue(entry.Key,out var rule);Apply(entry,Settings.Enabled?rule:null);}
                InvalidateBatch(); Status="已重新读取设置 · 新摆放的兵种使用新数值";
            }
            catch {Settings=old;foreach(var entry in Entries){old.Units.TryGetValue(entry.Key,out var rule);Apply(entry,old.Enabled?rule:null);}throw;}
        }
        public void ExportCatalog(bool full)
        {
            if(full && selected!=null)EnsureGraph(selected);
            var data=Entries.Select(e=>new {e.Key,e.Name,e.AssetName,e.Module,e.Source,e.Faction,DisplayName=Labels.UnitName(e),e.ParentText,Parents=e.Parents.Select(p=>p.Key),Price=e.OriginalPrice,Stats=e.OriginalStats,
                Components=e.Slots?.Select(s=>new {s.Key,s.Group,s.Label,s.Path,s.ComponentName,s.FieldName,s.Original}),
                Constants=e.Constants?.Select(s=>new {s.Key,s.Group,s.Label,s.Original})});
            File.WriteAllText(Path.Combine(DataDirectory,"兵种原始数据.json"),JsonConvert.SerializeObject(data,Formatting.Indented));
        }
        void OnDestroy(){if(Visible)Input.imeCompositionMode=previousIme;MenuOrder?.Dispose();ReleaseUiResources();if(Instance==this){Harmony?.UnpatchSelf();Instance=null;}}
    }
}
