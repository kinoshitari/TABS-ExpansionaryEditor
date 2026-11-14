using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using BepInEx.Bootstrap;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed class SourceSpec
    {
        public string Module,Prefix,Label;
        public SourceSpec(string module,string prefix,string label){Module=module;Prefix=prefix;Label=label;}
    }
    public sealed partial class EditorPlugin
    {
        public static readonly HashSet<Assembly> CombatAssemblies=new HashSet<Assembly>();
        readonly Dictionary<int,SourceSpec> unitSources=new Dictionary<int,SourceSpec>();
        readonly Dictionary<MethodBase,SourceSpec> sourceMethods=new Dictionary<MethodBase,SourceSpec>();
        readonly Dictionary<MethodBase,string> stageMethods=new Dictionary<MethodBase,string>();
        readonly HashSet<string> pendingStages=new HashSet<string>();
        readonly HashSet<DatabaseID> vanillaIds=new HashSet<DatabaseID>();
        readonly Dictionary<int,string> catalogKeys=new Dictionary<int,string>();
        DM.LandfallContentDatabase catalogDatabase;
        Faction[] catalogFactions=new Faction[0];
        static readonly SourceSpec VanillaSource=new SourceSpec("Vanilla","Vanilla","原版");
        static readonly SourceSpec HiddenSource=new SourceSpec("HiddenUnits","HiddenUnits","隐藏模组");
        static readonly SourceSpec HolidaySource=new SourceSpec("Holiday","Holiday","圣诞节");
        static readonly SourceSpec AnimalSource=new SourceSpec("AnimalKingdom","AnimalKingdom","动物王国");
        Type expansionLoader;

        static Type LoadedType(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
        static Type ActiveSourceType(string name,string pluginGuid)=>Chainloader.PluginInfos.ContainsKey(pluginGuid)?LoadedType(name):null;
        public static string SourceName(string id)
        {switch(id){case "Vanilla":return "原版";case "HiddenUnits":return "隐藏";case "ChouChou":return "臭臭";case "Expansionary":return "扩张";case "Holiday":return "圣诞节";case "AnimalKingdom":return "动物王国";case "ChineseMyth":return "中国神话";default:return id;}}
        public static string ComponentId(Type type)=>CombatAssemblies.Contains(type.Assembly)&&type.Namespace!="AddonExpansionary"?
            type.FullName+"@"+type.Assembly.GetName().Name:type.FullName;
        static DM.LandfallContentDatabase CurrentDatabase()
        {
            var instance=AccessTools.Field(typeof(DM.ContentDatabase),"m_instance").GetValue(null) as DM.ContentDatabase;
            return instance?.LandfallContentDatabase;
        }
        void InitializeSources()
        {
            RegisterSource("Expansionary.UManager","Expansionary.Loader",new SourceSpec("Expansionary","Expansionary","扩张模组"),false,"GeeztJeez.Expansionary");
            expansionLoader=ActiveSourceType("Expansionary.Loader","GeeztJeez.Expansionary");
            RegisterSource("ChouChousUnit.UManager","ChouChousUnit.Loader",new SourceSpec("ChouChou","ChouChou/Part1","臭臭模组 · 第一部分"),true,"chouchou");
            RegisterSource("DARK.CManager","DARK.Loader",new SourceSpec("ChouChou","ChouChou/Part2","臭臭模组 · 第二部分"),true,"dark");
            RegisterSource("rwdseerfd.UManager","rwdseerfd.Loader",new SourceSpec("ChouChou","ChouChou/Part3","臭臭模组 · 第三部分"),true,"rwdseerfd");
            RegisterSource("ChouChouMap.UManager","ChouChouMap.Loader",new SourceSpec("ChouChou","ChouChou/Map","臭臭模组 · 地图附属"),true,"ChouChouMap");
            // SLMACore creates these blueprints at runtime. Like the other manager
            // sources, persist their raw names rather than generated database IDs.
            RegisterSource("ChineseMythology.UManager","ChineseMythology.Loader",new SourceSpec("ChineseMyth","ChineseMyth","中国神话"),true,"monkeyking");
            var hidden=ActiveSourceType("HiddenUnits.HUMain","teamgrad.hiddenunits");if(hidden!=null)CombatAssemblies.Add(hidden.Assembly);
            var holiday=ActiveSourceType("Holiday.HolidayMain","teamgrad.holiday");if(holiday!=null)CombatAssemblies.Add(holiday.Assembly);
            var animal=ActiveSourceType("AnimalKingdom.AKMain","teamgrad.animalkingdom");if(animal!=null)CombatAssemblies.Add(animal.Assembly);
            var animalLauncher=ActiveSourceType("AnimalKingdom.AKLauncher","teamgrad.animalkingdom");if(animalLauncher!=null)TrackIterator(animalLauncher,"LaunchMod","动物王国");
            if(hidden!=null||holiday!=null)
            {
                var tg=ActiveSourceType("TGCore.TGMain","teamgrad.core");if(tg!=null)TrackIterator(tg,"InitializeMods","模组目录");
            }
        }
        void RegisterSource(string managerName,string loaderName,SourceSpec source,bool trackLoader,string pluginGuid)
        {
            var manager=ActiveSourceType(managerName,pluginGuid);if(manager==null)return;
            CombatAssemblies.Add(manager.Assembly);
            foreach(var method in manager.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>
                System.Text.RegularExpressions.Regex.IsMatch(m.Name,@"^Init\d*$")||new[]{"MadeUnits","Devil","CPline"}.Contains(m.Name)))
            {
                if(method.GetMethodBody()==null)continue;sourceMethods[method]=source;
                bool takesDatabase=method.GetParameters().FirstOrDefault()?.ParameterType==typeof(DM.LandfallContentDatabase);
                Harmony.Patch(method,prefix:new HarmonyMethod(typeof(EditorPlugin),takesDatabase?nameof(BeforeSourceInitWithDatabase):nameof(BeforeSourceInit)),postfix:new HarmonyMethod(typeof(EditorPlugin),nameof(AfterSourceInit)));
            }
            if(trackLoader){var loader=ActiveSourceType(loaderName,pluginGuid);if(loader!=null)TrackIterator(loader,"call",source.Label);}
        }
        void TrackIterator(Type owner,string name,string label)
        {
            var iterator=AccessTools.DeclaredMethod(owner,name,Type.EmptyTypes);
            var attribute=iterator?.GetCustomAttributes(typeof(System.Runtime.CompilerServices.IteratorStateMachineAttribute),false)
                .Cast<System.Runtime.CompilerServices.IteratorStateMachineAttribute>().SingleOrDefault();
            var method=attribute==null?null:AccessTools.DeclaredMethod(attribute.StateMachineType,"MoveNext",Type.EmptyTypes);
            if(method==null)throw new InvalidOperationException(label+"的加载入口不匹配");
            pendingStages.Add(label);
            stageMethods[method]=label;Harmony.Patch(method,postfix:new HarmonyMethod(typeof(EditorPlugin),nameof(AfterLoaderStep)));
        }
        static void AfterLoaderStep(MethodBase __originalMethod,bool __result)
        {if(!__result&&Instance.stageMethods.TryGetValue(__originalMethod,out var label))Instance.pendingStages.Remove(label);}
        static void BeforeSourceInitWithDatabase(DM.LandfallContentDatabase __0,ref HashSet<int> __state)
        {
            var p=Instance;var db=__0;
            if(db==null)return;p.catalogDatabase=db;__state=new HashSet<int>(DatabaseBlueprints(db).Where(b=>b).Select(b=>b.GetInstanceID()));
        }
        static void BeforeSourceInit(ref HashSet<int> __state)=>BeforeSourceInitWithDatabase(CurrentDatabase(),ref __state);
        static void AfterSourceInit(MethodBase __originalMethod,HashSet<int> __state)
        {
            var p=Instance;if(__state==null||p.catalogDatabase==null)return;
            var source=p.sourceMethods[__originalMethod];int added=0;
            foreach(var bp in DatabaseBlueprints(p.catalogDatabase).Where(b=>b&&!__state.Contains(b.GetInstanceID())))
                if(!p.unitSources.ContainsKey(bp.GetInstanceID())){p.unitSources[bp.GetInstanceID()]=source;added++;}
            p.Logger.LogInfo("来源识别 "+source.Label+" / "+__originalMethod.Name+": "+added);
        }
        void ReadOfficialUnitIds()
        {
            var resource=Resources.Load<TextAsset>("DMLandfallContentDatabase");
            if(!resource)throw new InvalidOperationException("找不到游戏原版目录");
            var type=typeof(UnitBlueprint).Assembly.GetType("DM.LandfallContentDatabaseFile",true);
            var serializer=typeof(UnitBlueprint).Assembly.GetType("DM.DatabaseSerializer",true);
            var method=serializer.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Single(m=>m.Name=="Deserialize"&&m.IsGenericMethodDefinition);
            var file=method.MakeGenericMethod(type).Invoke(null,new object[]{resource.bytes});
            foreach(var item in (IEnumerable)AccessTools.Field(type,"unitBlueprintGuids").GetValue(file))
            {
                var t=item.GetType();vanillaIds.Add(new DatabaseID((int)AccessTools.Field(t,"modId").GetValue(item),(int)AccessTools.Field(t,"id").GetValue(item)));
            }
        }
        void ReadHiddenUnitIds()
        {
            var type=ActiveSourceType("HiddenUnits.HUMain","teamgrad.hiddenunits");if(type==null)return;
            CombatAssemblies.Add(type.Assembly);
            var bundle=AccessTools.Field(type,"hiddenUnits").GetValue(null) as AssetBundle;
            if(!bundle)throw new InvalidOperationException("隐藏模组的资源未加载完成");
            var ids=new HashSet<DatabaseID>(bundle.LoadAllAssets<UnitBlueprint>().Where(b=>b).Select(b=>b.Entity.GUID));
            foreach(var bp in DatabaseBlueprints(catalogDatabase).Where(b=>b&&ids.Contains(b.Entity.GUID)&&!vanillaIds.Contains(b.Entity.GUID)))
                unitSources[bp.GetInstanceID()]=HiddenSource;
        }
        void ReadBundleUnitIds(string typeName,string fieldName,SourceSpec source,string pluginGuid)
        {
            var type=ActiveSourceType(typeName,pluginGuid);if(type==null)return;
            var bundle=AccessTools.Field(type,fieldName).GetValue(null) as AssetBundle;
            if(!bundle)throw new InvalidOperationException(source.Label+"资源未就绪");
            var ids=new HashSet<DatabaseID>(bundle.LoadAllAssets<UnitBlueprint>().Where(b=>b).Select(b=>b.Entity.GUID));
            foreach(var bp in DatabaseBlueprints(catalogDatabase).Where(b=>b&&b.UnitBase&&ids.Contains(b.Entity.GUID)&&!vanillaIds.Contains(b.Entity.GUID)))
                if(!unitSources.ContainsKey(bp.GetInstanceID()))unitSources[bp.GetInstanceID()]=source;
        }
        public static string RawName(UnitBlueprint bp)=>(string)AccessTools.Field(typeof(DatabaseEntity),"m_name").GetValue(bp.Entity);
        static string IdText(DatabaseID id)=>id.m_modID+":"+id.m_ID;
        string ResolveKey(UnitBlueprint bp)
        {
            if(catalogKeys.TryGetValue(bp.GetInstanceID(),out var key))return key;
            if(vanillaIds.Contains(bp.Entity.GUID))return "Vanilla/"+IdText(bp.Entity.GUID);
            if(unitSources.TryGetValue(bp.GetInstanceID(),out var source))return source.Prefix+"/"+(new[]{"HiddenUnits","Holiday","AnimalKingdom"}.Contains(source.Module)?IdText(bp.Entity.GUID):RawName(bp));
            return "Expansionary/"+RawName(bp);
        }
        bool SourcesLoading()
        {
            if(pendingStages.Count>0){Status="正在等待："+string.Join("、",pendingStages);return true;}
            if(expansionLoader!=null)
            {
                var instance=FindObjectsOfType(expansionLoader).Cast<object>().FirstOrDefault();
                var done=AccessTools.Field(expansionLoader,"doneExecuted");
                if(instance!=null&&done!=null&&!(bool)done.GetValue(instance)){Status="正在等待扩张模组完成…";return true;}
            }
            return false;
        }
        IEnumerator WaitForCatalog()
        {
            while(CurrentDatabase()==null||SourcesLoading())yield return null;
            yield return null;
            try
            {
                catalogDatabase=CurrentDatabase();ReadOfficialUnitIds();ReadHiddenUnitIds();
                ReadBundleUnitIds("Holiday.HolidayMain","holiday",HolidaySource,"teamgrad.holiday");
                ReadBundleUnitIds("AnimalKingdom.AKMain","kermate",AnimalSource,"teamgrad.animalkingdom");
                catalogFactions=DM.ContentDatabase.Instance().GetFactionsOrderedByIndex().Where(f=>f)
                    .Concat(Resources.FindObjectsOfTypeAll<Faction>().Where(f=>f).OrderBy(f=>f.index)).Distinct().ToArray();
                // Use registered factions, not an unordered scan of loaded resources.
                // Custom collections must not replace a unit's original faction.
                var factions=catalogFactions.Where(f=>!f.IsCustom).Concat(catalogFactions.Where(f=>f.IsCustom)).ToArray();
                foreach(var bp in DatabaseBlueprints(catalogDatabase).Where(b=>b))
                {
                    SourceSpec source;
                    if(vanillaIds.Contains(bp.Entity.GUID))source=VanillaSource;
                    else if(!unitSources.TryGetValue(bp.GetInstanceID(),out source))continue;
                    // The HiddenUnits bundle also registers 23 empty *_VANILLA reference placeholders.
                    // They have no body prefab and are not units that can be spawned or balanced.
                    if(!bp.UnitBase)continue;
                    string key=ResolveKey(bp);
                    if(ByKey.ContainsKey(key))throw new InvalidOperationException(source.Label+"有重复兵种标识："+RawName(bp));
                    var faction=factions.FirstOrDefault(f=>f.Units?.Contains(bp)==true);
                    var entry=new UnitEntry {Key=key,Name=RawName(bp),AssetName=bp.name,Module=source.Module,Source=source.Label,Faction=faction?faction.Entity.Name:"附加／召唤兵种",Blueprint=bp,
                        NativeFaction=faction,CatalogOrder=Entries.Count,
                        OriginalPrice=bp.GetUnitCost(true),OriginalPriceWithoutRiders=bp.GetUnitCost(false),Base=bp.UnitBase,Right=bp.RightWeapon,Left=bp.LeftWeapon,Props=ReadProps(bp),
                        Children=bp.objectsToSpawnAsChildren==null?new GameObject[0]:(GameObject[])bp.objectsToSpawnAsChildren.Clone()};
                    foreach(var stat in Labels.Stats)entry.OriginalStats[stat.Field]=AccessTools.Field(typeof(UnitBlueprint),stat.Field).GetValue(bp);
                    Entries.Add(entry);ByKey.Add(key,entry);catalogKeys[bp.GetInstanceID()]=key;runtimeIds[bp.Entity.GUID]=key;
                }
                InitializeDamageSources();
                CatalogRelations.Build(Entries);
                foreach(var entry in Entries)if(Settings.Enabled&&Settings.Units.TryGetValue(entry.Key,out var rule)){Validate(entry,rule);Apply(entry,rule);}
                Ready=true;
                RefreshCatalogOrder();
                Status=fileError?"设置文件有错误，原文件保留。":"已载入 "+Entries.Count+" 个兵种 · 保存后重新摆放／重开关卡";
                if(!Entries.Any(e=>e.Module==moduleFilter))moduleFilter="Vanilla";
                ExportCatalog(false);
                Logger.LogInfo("READY: "+string.Join("; ",Entries.GroupBy(e=>e.Module).Select(g=>g.Key+"="+g.Count()))+"; "+Settings.Units.Count+" overrides");
            }
            catch(Exception ex){Status="目录加载失败："+ex.Message;Logger.LogError(ex);}
        }
        void RefreshCatalogOrder()
        {
            if(!Ready)return;
            // Follow the visible hotbar, then the native database order for the rest.
            var factions=FindObjectsOfType<PlacementFactionBar>().Where(b=>b.isActiveAndEnabled)
                .SelectMany(b=>b.GetFactionsOnBar()).Concat(catalogFactions).Where(f=>f).Distinct().ToList();
            var ranks=new Dictionary<Faction,Dictionary<DatabaseID,int>>();
            foreach(var faction in factions)
            {
                var units=new Dictionary<DatabaseID,int>();
                foreach(var bp in (faction.Units==null?new UnitBlueprint[0]:faction.GetRearrangedUnits()).Where(b=>b))
                    if(!units.ContainsKey(bp.Entity.GUID))units.Add(bp.Entity.GUID,units.Count);
                ranks[faction]=units;
            }
            foreach(var entry in Entries)
            {
                int faction=entry.NativeFaction?factions.IndexOf(entry.NativeFaction):-1;
                entry.FactionOrder=faction<0?int.MaxValue:faction;
                entry.UnitOrder=entry.NativeFaction&&ranks.TryGetValue(entry.NativeFaction,out var units)&&
                    units.TryGetValue(entry.Blueprint.Entity.GUID,out var rank)?rank:int.MaxValue;
            }
            Entries.Sort((a,b)=>{
                int order=string.Compare(a.Module,b.Module,StringComparison.Ordinal);
                if(order==0)order=a.FactionOrder.CompareTo(b.FactionOrder);
                if(order==0)order=a.UnitOrder.CompareTo(b.UnitOrder);
                return order==0?a.CatalogOrder.CompareTo(b.CatalogOrder):order;
            });
        }
    }
}
