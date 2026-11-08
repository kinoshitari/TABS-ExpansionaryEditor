using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        [ThreadStatic] static List<string> damageContexts;
        static readonly Dictionary<MethodBase,string> DamageMethods=new Dictionary<MethodBase,string>();
        static readonly Dictionary<Type,string> EffectSources=new Dictionary<Type,string>();
        static readonly Dictionary<string,string> ResistanceGroups=new Dictionary<string,string>();
        bool usesDamageSources;
        sealed class DamageScope { internal int Depth; }
        static bool IsResistanceKey(string key)=>ResistanceLabels.ContainsKey(key)||key.StartsWith("source/",StringComparison.Ordinal)&&key.Split('/').Length==3;
        void RefreshResistanceRouting()=>usesDamageSources=Settings.Units.Values.Any(r=>r.Resistances.Keys.Any(k=>k!="Freeze"&&!new[]{"Default","Piercing","Blunt","Magic"}.Contains(k)));

        void InitializeDamageSources()
        {
            RegisterResistance("Burning","燃烧／持续承伤倍率","燃烧与爆炸");
            RegisterResistance("Explosion","爆炸承伤倍率","燃烧与爆炸");
            RegisterResistance("Freeze","冰冻积累倍率","冰冻控制");
            var assemblies=Chainloader.PluginInfos.Values.Where(p=>p.Instance).Select(p=>p.Instance.GetType().Assembly)
                .Where(a=>a!=typeof(EditorPlugin).Assembly).Distinct().ToList();
            assemblies.Insert(0,typeof(Unit).Assembly);
            var nativeTypes=new HashSet<string>{"Effect_IceArrow","DamageOverTimeEffect","Explosion","CollisionDamage","DataHandler","Water","CollisionWeapon","DamageTrigger","ParentSpell","ProjectileHit","Telekanesis","VapireBite","AssassEffect","BlackHole","DarkPHand","DealDamageToParentUnit","ProjectileStickDamage","ReaperWings","StunEffect"};
            int installed=0;
            foreach(var assembly in assemblies)
            {
                var native=assembly==typeof(Unit).Assembly;
                foreach(var type in DamageTypesIn(assembly).OrderBy(t=>t.FullName,StringComparer.Ordinal))
                {
                    Type owner=type;while(owner.DeclaringType!=null)owner=owner.DeclaringType;
                    if(native&&!nativeTypes.Contains(owner.Name))continue;
                    if(type.ContainsGenericParameters)continue;
                    if(!native&&typeof(DamageOverTimeEffect).IsAssignableFrom(type)&&type!=typeof(DamageOverTimeEffect))
                    {
                        var effectKey=RegisterSourceResistance(assembly,owner);EffectSources[type]=effectKey;
                        var update=AccessTools.DeclaredMethod(typeof(DamageOverTimeEffect),"Update",Type.EmptyTypes);
                        if(update!=null&&DamageMethods.ContainsKey(update))RecordDamageAnnotation(effectKey,owner,update);
                    }
                    if(type==typeof(Effect_IceArrow)||type.FullName=="TGCore.Library.Effect_Frostbite"||type.FullName=="newice")
                    {
                        var add=AccessTools.DeclaredMethod(type,"Add",new[]{typeof(float)});
                        if(add!=null)
                        {
                            Harmony.Patch(add,prefix:new HarmonyMethod(typeof(EditorPlugin),nameof(ScaleFreeze)));
                            RecordDamageAnnotation("Freeze",type,add);
                        }
                    }
                    foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
                    {
                        if(method.IsAbstract||method.ContainsGenericParameters)continue;
                        if(owner.Name=="HealSelf"||owner.Name=="HealSelfS")continue;
                        bool callsDamage;
                        try
                        {
                            var implementation=method.GetMethodImplementationFlags();
                            if((implementation&(MethodImplAttributes.CodeTypeMask|MethodImplAttributes.InternalCall))!=MethodImplAttributes.IL||
                                (method.Attributes&MethodAttributes.PinvokeImpl)!=0)continue;
                            // Merged mods can retain IL declarations with RVA=0. Mono throws
                            // here instead of returning null; isolate them like unreadable IL.
                            if(method.GetMethodBody()==null)continue;
                            callsDamage=PatchProcessor.GetOriginalInstructions(method).Any(i=>i.operand is MethodInfo m&&m.Name=="TakeDamage"&&(m.DeclaringType==typeof(Damagable)||m.DeclaringType==typeof(HealthHandler)));
                        }
                        catch(Exception ex){Logger.LogWarning("伤害来源未读取 "+assembly.GetName().Name+" / "+type.FullName+"."+method.Name+": "+ex.Message);continue;}
                        if(!callsDamage)continue;
                        string key;
                        if(owner.Name=="Explosion"||owner.FullName=="TGCore.Library.ExplosionAddFewEffects"||owner.FullName=="myfpsmodd.boomreeew")key="Explosion";
                        else if(native&&owner==typeof(DamageOverTimeEffect))key="Burning";
                        else key=RegisterSourceResistance(assembly,owner);
                        try
                        {
                            Harmony.Patch(method,prefix:new HarmonyMethod(typeof(EditorPlugin),nameof(BeginDamageSource)){priority=Priority.First},
                                finalizer:new HarmonyMethod(typeof(EditorPlugin),nameof(EndDamageSource)));
                            DamageMethods[method]=key;installed++;
                            RecordDamageAnnotation(key,owner,method);
                        }
                        catch(Exception ex){Logger.LogWarning("伤害来源未接入 "+type.FullName+"."+method.Name+": "+ex.Message);}
                    }
                }
            }
            RefreshResistanceRouting();Logger.LogInfo("伤害来源接入："+installed+" 个实际扣血调用入口；冰冻单独调整积累量");
        }
        IEnumerable<Type> DamageTypesIn(Assembly assembly)
        {
            try{return assembly.GetTypes();}
            catch(ReflectionTypeLoadException ex)
            {
                Logger.LogWarning("部分伤害来源类型未载入 "+assembly.GetName().Name+": "+string.Join("; ",ex.LoaderExceptions.Select(e=>e.Message)));
                return ex.Types.Where(t=>t!=null);
            }
        }
        static void RegisterResistance(string key,string label,string group)
        {ResistanceLabels[key]=label;ResistanceGroups[key]=group;}
        static string RegisterSourceResistance(Assembly assembly,Type owner)
        {
            string key="source/"+assembly.GetName().Name+"/"+owner.FullName;
            if(ResistanceLabels.ContainsKey(key))return key;
            string group=assembly==typeof(Unit).Assembly?"原版其他伤害":SourceAssemblyName(assembly);
            RegisterResistance(key,DamageSourceName(owner)+"承伤倍率",group);return key;
        }
        static string SourceAssemblyName(Assembly assembly)
        {
            if(assembly.GetType("Expansionary.Loader")!=null)return "扩张伤害";
            if(assembly.GetType("HiddenUnits.HUMain")!=null)return "隐藏伤害";
            if(assembly.GetType("Holiday.HolidayMain")!=null)return "圣诞节伤害";
            if(assembly.GetType("AnimalKingdom.AKMain")!=null)return "动物王国伤害";
            if(assembly.GetType("ChineseMythology.Loader")!=null)return "中国神话伤害";
            if(assembly.GetType("TGCore.TGMain")!=null)return "TGCore 效果";
            if(assembly.GetType("ChouChousUnit.Loader")!=null||assembly.GetType("DARK.Loader")!=null||assembly.GetType("rwdseerfd.Loader")!=null)return "臭臭伤害";
            return assembly.GetName().Name;
        }
        static string DamageSourceName(Type type)
        {
            var names=new Dictionary<string,string>{
                {"CollisionDamage","碰撞"},{"DataHandler","跌落"},{"Water","水域"},{"CollisionWeapon","近战命中"},
                {"DamageTrigger","接触区域"},{"ParentSpell","法术"},{"ProjectileHit","弹丸命中"},{"Telekanesis","念力"},
                {"VapireBite","吸血撕咬"},{"AssassEffect","暗杀效果"},{"BlackHole","黑洞"},{"DarkPHand","黑暗之手"},
                {"DealDamageToParentUnit","自身伤害"},{"ProjectileStickDamage","附着弹丸"},{"ReaperWings","收割之翼"},{"StunEffect","眩晕效果"},
                {"Effect_Bleed","流血"},{"Effect_Disintegrate","分解"},{"Effect_Lightning","雷电"},{"Effect_Plague","瘟疫"},
                {"Effect_Petrification","石化"},{"Effect_Garlic","大蒜效果"},{"BioChemicalEffectBase","生化"},{"AcidEffectBase","酸蚀"},
                {"RustedEffectBase","锈蚀"},{"StonedEffect","石化"},{"CritSlashEffectBase","暴击斩击"},
                {"MageForceEffectBase","法师力场"},{"NightstalkConfusion","暗袭混乱"},{"PurifierEffect","净化"},
                {"ChompingEffect","吞噬"},{"DamageOverTimeEffect2","模组持续伤害"},{"TakeDamagePercent","百分比伤害"},
                {"NiuEngerMove","牛魔王狂暴"},{"SuperStick","超级一棒"},{"BillyLaser","比利激光"},
                {"BoulderBehavior","雪球撞击"},{"ProjectileHitPercentDamage","百分比弹丸"},{"ProjectileHitLeech","吸血弹丸"}
            };
            return names.TryGetValue(type.Name,out var name)?name:Labels.HasChinese(type.Name)?type.Name:Labels.Component(type);
        }
        static void BeginDamageSource(MethodBase __originalMethod,object __instance,out DamageScope __state)
        {
            __state=null;var p=Instance;
            if(p==null||!p.Settings.Enabled||!p.usesDamageSources||!DamageMethods.TryGetValue(__originalMethod,out var key))return;
            if(damageContexts==null)damageContexts=new List<string>();
            __state=new DamageScope{Depth=damageContexts.Count};damageContexts.Add(key);
            if(__instance!=null&&EffectSources.TryGetValue(__instance.GetType(),out var effectKey))damageContexts.Add(effectKey);
        }
        static void EndDamageSource(DamageScope __state)
        {
            if(__state!=null&&damageContexts!=null&&damageContexts.Count>__state.Depth)
                damageContexts.RemoveRange(__state.Depth,damageContexts.Count-__state.Depth);
        }
        static void ScaleFreeze(Component __instance,ref float __0)
        {
            var p=Instance;if(p==null||!p.Ready||!p.Settings.Enabled||__0<=0)return;
            var unit=__instance.GetComponentInParent<Unit>();
            if(!unit||!unit.unitBlueprint||!p.runtimeIds.TryGetValue(unit.unitBlueprint.Entity.GUID,out var key)||
                !p.Settings.Units.TryGetValue(key,out var rule)||!rule.Resistances.TryGetValue("Freeze",out var value))return;
            __0=(float)Math.Min(float.MaxValue,(double)__0*(float)Numbers.Parse(value,typeof(float)));
        }
    }
}
