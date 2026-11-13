using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExpansionaryEditor
{
    // Only inspected DoEffect application paths. In the TGCore compound explosion,
    // ordinary damage is in Explode, so only its separate DoEffect call is gated.
    static class WeaponEffects
    {
        internal sealed class Parameter
        {
            internal string Field, Label;
            internal double Minimum;
            internal Parameter(string field,string label,double minimum=0){Field=field;Label=label;Minimum=minimum;}
        }
        internal sealed class Definition
        {
            internal string Name, Note;
            internal Parameter[] Parameters;
            internal Definition(string name,string note,params Parameter[] parameters){Name=name;Note=note;Parameters=parameters;}
        }
        static readonly Dictionary<string,string> Carriers=new Dictionary<string,string> {
            {"ProjectileHitAddEffect","EffectPrefab"},{"ProjectileHitAddEffectWithSize","EffectPrefab"},
            {"MeleeWeaponAddEffect","EffectPrefab"},{"ExplosionAddEffect","EffectPrefab"},{"AddObjectEffect","EffectPrefab"},
            {"TGCore.Library.ExplosionAddEffectOnce","effectPrefab"},
            {"TGCore.Library.MeleeWeaponFreeze","effect"},{"TGCore.Library.ExplosionAddFewEffects","effect"},
            {"rwdseerfd.角色类.小兵类.毒液佣兵.MeleeWeaponAddEffect加强版","EffectPrefab"}
        };
        static readonly Dictionary<string,Definition> Definitions=new Dictionary<string,Definition> {
            {"Effect_IceArrow",new Definition("寒冷","寒冷值是新增减速／冻结积累，不是冰箭命中伤害。",
                new Parameter("effectAmount","寒冷值"),new Parameter("dragPerAmount","每点寒冷阻力"),new Parameter("removeDragSpeed","寒冷消退速度"))},
            {"newice",new Definition("寒冷","臭臭冰冻效果的新增积累，不改变直接命中伤害。",
                new Parameter("effectAmount","寒冷值"),new Parameter("dragPerAmount","每点寒冷阻力"),new Parameter("removeDragSpeed","寒冷消退速度"))},
            {"TGCore.Library.Effect_Frostbite",new Definition("霜寒","TGCore 霜寒积累与减速，和原版寒冷分开。",
                new Parameter("coldAmount","寒冷值"),new Parameter("slowPerAmount","每点寒冷减速"),new Parameter("removeSlowSpeed","寒冷消退速度"))},
            {"DamageOverTimeEffect",new Definition("燃烧／持续伤害","燃烧值是每层伤害基数；实际扣血受曲线与伤害倍率影响。持续时间为周期乘以时长倍率。",
                new Parameter("damagePerStack","燃烧／持续伤害值"),new Parameter("effectTime","持续周期（秒）",0.001),new Parameter("effectDuration","持续时长倍率"))},
            {"TGCore.Library.Effect_Bleed",new Definition("流血","按原效果叠层和结算，不改变普通命中。",
                new Parameter("bleedDamage","流血伤害基数"),new Parameter("baseDuration","基础持续时间（秒）"),new Parameter("durationPerStack","每层追加时间（秒）"),new Parameter("maxStacks","叠层上限"))},
            {"TGCore.Library.Effect_Lightning",new Definition("雷电","附加雷电及连锁效果，与弹丸基础伤害分开。",
                new Parameter("lightningDamage","雷电伤害"),new Parameter("maxTargetRange","连锁距离"),new Parameter("chainLength","连锁长度"),new Parameter("chains","连锁数量"))},
            {"AddonExpansionary.BioChemicalEffectBase",new Definition("生化","生化状态定期扣血；运行中的计时器不作为编辑值。",
                new Parameter("damage","每次生化伤害"),new Parameter("timeMax","生化持续时间（秒）"))},
            {"AddonExpansionary.AcidEffectBase",new Definition("酸蚀","酸蚀协程在两个伤害端点之间取值。",
                new Parameter("damage","酸蚀伤害下限"),new Parameter("damageOffset","酸蚀伤害上限"))},
            {"AddonExpansionary.RustedEffectBase",new Definition("锈蚀","控制锈蚀附加状态；伤害由原脚本计算。")},
            {"HiddenUnits.Effect_Petrification",new Definition("石化","控制石化状态及其附带扣血。",
                new Parameter("petrifyDamage","石化伤害"),new Parameter("petrifySpeed","石化速度"),new Parameter("unPetrifyDelay","解除等待（秒）"),new Parameter("unPetrifySpeed","解除速度"))},
            {"AddonExpansionary.StonedEffect",new Definition("石化","控制扩张石化状态；不暴露脚本在发动时重算的控制参数。",new Parameter("damage","石化伤害"))},
            {"AddonExpansionary.ChompingEffect",new Definition("吞噬","控制吞噬附加状态及其扣血。",new Parameter("damage","吞噬伤害"))},
            {"AddonExpansionary.MageForceEffectBase",new Definition("法师力场","控制力场附加效果。",new Parameter("damage","力场伤害"),new Parameter("pushes","推击次数"))},
            {"AddonExpansionary.CritSlashEffectBase",new Definition("暴击斩击","控制附加斩击，不改变武器本体命中。",new Parameter("baseDamage","斩击伤害基数"))},
            {"AddonExpansionary.PurifierEffect",new Definition("净化","控制净化打击；当前斩击计数不作为编辑值。",new Parameter("damage","净化伤害"),new Parameter("damageRandom","伤害随机范围"),new Parameter("delay","打击间隔（秒）"))},
            {"TGCore.Library.Effect_Disintegrate",new Definition("分解","关闭后不再从此武器入口施加分解；伤害值不等同于销毁开关。",new Parameter("damage","分解伤害"))},
            {"TGCore.Library.Effect_Tase",new Definition("电击控制","电击积累影响控制，不是雷电生命伤害。",new Parameter("initialTaseAmount","初始电击值"),new Parameter("extraTaseAmountPerHit","每次追加电击值"),new Parameter("maxTaseAmount","电击值上限"))},
            {"TGCore.Library.Effect_Oil",new Definition("油污","增幅 0.1 表示额外 10% 承伤；油污附带滑倒，额外扣血使用直接写生命。",new Parameter("duration","油污持续时间（秒）"),new Parameter("initialDamageAmpPercentage","初始承伤增幅（比例）"),new Parameter("damageAmpPerStack","每层承伤增幅（比例）"))},
            {"TGCore.Library.Effect_Weakening",new Definition("虚弱","减速与攻速削弱属于附加状态。",new Parameter("maxDuration","持续时间上限（秒）"),new Parameter("attackSlowPerStack","每层攻速减缓"),new Parameter("weakenLimit","虚弱层数上限"))},
            {"TGCore.Library.Effect_Zombie",new Definition("感染","感染进度与生命伤害分开；保留原有转化及复活条件。",new Parameter("progressToAdd","感染值"))},
            {"AnimalKingdom.Effect_Plague",new Definition("瘟疫","动物王国感染进度与后续状态。",new Parameter("progressToAdd","感染值"))},
            {"Effect_Plague",new Definition("瘟疫","臭臭感染进度与后续状态。",new Parameter("progressToAdd","感染值"))},
            {"AssassEffect",new Definition("暗杀叠层","控制暗杀效果的新增施加和叠层。",new Parameter("damagePerStack","每层伤害"))},
            {"StunEffect",new Definition("眩晕","控制眩晕与附带伤害；不暴露发动时重算的控制参数。",new Parameter("damage","眩晕附带伤害"))},
            {"FearEffect",new Definition("恐惧","恐惧是控制状态，不是独立伤害类型。")},
            {"PotionEffect",new Definition("药水混乱","药水混乱影响动作，不是直接扣血。")}
        };
        static readonly HashSet<MethodBase> Patched=new HashSet<MethodBase>();
        internal static bool IsCarrier(Type type)=>Carriers.ContainsKey(type.FullName);
        internal static bool IsEffectField(Type type,string field)=>Carriers.TryGetValue(type.FullName,out var name)&&name==field;
        internal static bool IsWeaponPath(string path)=>path.StartsWith("R/",StringComparison.Ordinal)||path.StartsWith("L/",StringComparison.Ordinal);
        internal static bool HasRepeatCold(Type carrier,Type effect)=>effect.FullName=="TGCore.Library.Effect_Frostbite"&&
            (carrier.FullName=="TGCore.Library.MeleeWeaponFreeze"||carrier.FullName=="TGCore.Library.ExplosionAddFewEffects");
        internal static Definition Describe(Type type)
        {
            for(var t=type;t!=null;t=t.BaseType)if(Definitions.TryGetValue(t.FullName,out var definition))return definition;
            return new Definition("附加状态","使用原效果脚本；仅提供已确认的施加开关，不推测内部数值。");
        }
        internal static string RangeIssue(ValueSlot slot,string value,IEnumerable<ValueSlot> slots,Dictionary<string,string> values)
        {
            if(slot==null||slot.EffectKey==null||slot.ComponentType.FullName!="AddonExpansionary.AcidEffectBase"||
                (slot.FieldName!="damage"&&slot.FieldName!="damageOffset"))return "";
            var other=slots.FirstOrDefault(s=>s.EffectKey==slot.EffectKey&&s.FieldName==(slot.FieldName=="damage"?"damageOffset":"damage"));
            if(other==null)return "";
            double current=Convert.ToDouble(Numbers.Parse(value,slot.Type));
            double peer=Convert.ToDouble(Numbers.Parse(values.TryGetValue(other.Key,out var saved)?saved:other.Original,other.Type));
            return (slot.FieldName=="damage"?current>peer:peer>current)?"酸蚀伤害下限不能高于上限":"";
        }
        internal static void SetEnabled(Component source,bool enabled)
        {
            var tag=FindOwner(source);
            if(!tag)throw new InvalidOperationException("特殊效果缺少武器副本标记");
            var disabled=(tag.DisabledWeaponEffects??new Component[0]).Where(c=>c&&c!=source).ToList();
            if(!enabled)disabled.Add(source);
            tag.DisabledWeaponEffects=disabled.ToArray();
        }
        internal static void Ensure(Type type,Harmony harmony)
        {
            if(!IsCarrier(type))return;
            var method=AccessTools.Method(type,"DoEffect");
            if(method==null||Patched.Contains(method))return;
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(WeaponEffects),nameof(AllowEffect)));
            Patched.Add(method);
        }
        static bool AllowEffect(Component __instance)
        {
            var p=EditorPlugin.Instance;
            if(p==null||!p.Settings.Enabled)return true;
            var tag=FindOwner(__instance);
            // Unity remaps these component references when it instantiates the weapon/projectile.
            return !tag||tag.DisabledWeaponEffects==null||!tag.DisabledWeaponEffects.Contains(__instance);
        }
        static OwnerTag FindOwner(Component source)
        {
            // Staged prefab hosts are inactive; do not use an active-only parent query.
            for(var t=source.transform;t!=null;t=t.parent){var tag=t.GetComponent<OwnerTag>();if(tag)return tag;}
            return null;
        }
    }
}
