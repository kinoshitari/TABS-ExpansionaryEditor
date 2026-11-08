using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        static readonly Dictionary<string,string> ResistanceLabels=new Dictionary<string,string> {
            {"Default","普通承伤倍率"},{"Piercing","穿刺承伤倍率"},
            {"Blunt","钝击承伤倍率"},{"Magic","魔法承伤倍率"}
        };
        static string ResistanceLabel(string key)=>ResistanceLabels.TryGetValue(key,out var title)?title:key;
        void InstallResistances()
        {
            // Scale before armor/refund prefixes so those mods receive the same
            // adjusted damage as the native handler rather than refunding old damage.
            var prefix=new HarmonyMethod(typeof(EditorPlugin),nameof(IncomingDamage)){priority=Priority.First};
            Harmony.Patch(AccessTools.Method(typeof(HealthHandler),"TakeDamage",new[]{typeof(float),typeof(Vector3),typeof(Unit),typeof(DamageType)}),prefix:prefix);
        }
        static void IncomingDamage(ref float __0,DamageType __3,Unit ___unit)
        {
            var p=Instance;
            // Healing and damage paths that bypass the native health handler retain
            // their original meaning. Unconfigured units never enter the new path.
            if(__0<=0||p==null||!p.Ready||!p.Settings.Enabled||!___unit||!___unit.unitBlueprint)return;
            if(!p.runtimeIds.TryGetValue(___unit.unitBlueprint.Entity.GUID,out var key)||
                !p.Settings.Units.TryGetValue(key,out var rule)||rule.Resistances.Count==0)return;
            double multiplier=rule.Resistances.TryGetValue(__3.ToString(),out var text)?(float)Numbers.Parse(text,typeof(float)):1;
            if(damageContexts!=null)foreach(var source in damageContexts.Distinct())
                if(rule.Resistances.TryGetValue(source,out var sourceValue))multiplier*=(float)Numbers.Parse(sourceValue,typeof(float));
            __0=multiplier==0?0:(float)Math.Min(float.MaxValue,(double)__0*multiplier);
        }
        static void ValidateResistances(UnitRule rule)
        {
            foreach(var pair in rule.Resistances)
            {
                if(!IsResistanceKey(pair.Key))throw new FormatException("未知承伤类型："+pair.Key);
                if((float)Numbers.Parse(pair.Value,typeof(float))<0)throw new FormatException(ResistanceLabel(pair.Key)+"不能为负数");
            }
        }
        void DrawResistances()
        {
            var saved=SavedValues(draft.Resistances);
            int pending=ResistanceLabels.Keys.Count(k=>ValueState(draft.Resistances,k)==FieldVisualState.Draft||ValueState(draft.Resistances,k)==FieldVisualState.Restore);
            if(!DrawFold("resistance","伤害抗性",4,saved?.Count??0,pending,true))return;
            foreach(var key in new[]{"Default","Piercing","Blunt","Magic"})DrawValue(draft.Resistances,key,ResistanceLabel(key),"1",typeof(float));
            var sourceKeys=ResistanceLabels.Keys.Except(new[]{"Default","Piercing","Blunt","Magic"})
                .Union(draft.Resistances.Keys.Where(k=>k.StartsWith("source/",StringComparison.Ordinal))).ToList();
            foreach(var group in sourceKeys.GroupBy(k=>ResistanceGroups.TryGetValue(k,out var title)?title:"未加载来源"))
            {
                int changed=group.Count(k=>saved?.ContainsKey(k)==true);
                int edits=group.Count(k=>ValueState(draft.Resistances,k)==FieldVisualState.Draft||ValueState(draft.Resistances,k)==FieldVisualState.Restore);
                bool main=group.Key=="燃烧与爆炸"||group.Key=="冰冻控制";
                if(!DrawFold("resistance/"+group.Key,group.Key,group.Count(),changed,edits,main||changed>0||edits>0,1))continue;
                foreach(var key in group.OrderBy(ResistanceLabel))DrawValue(draft.Resistances,key,ResistanceLabel(key),"1",typeof(float));
            }
            int modified=Settings.Units.TryGetValue(selected.Key,out var rule)?rule.Components.Keys.Count(k=>k.Contains("/DamageMultiplier[")):0;
            if(DrawFold("resistance/body","部位命中",0,modified,0,modified>0,1))
            {
                EnsureGraph(selected);
                var slots=selected.Slots.Where(s=>s.ComponentType==typeof(DamageMultiplier)&&s.FieldName=="multiplier"&&s.Key.StartsWith("B/",StringComparison.Ordinal)).ToList();
                if(slots.Count==0)GUILayout.Label("无部位系数",columnStyle,GUILayout.Height(40));
                foreach(var slot in slots)
                {
                    string part=BodyPartName(slot.Path);var peers=slots.Where(s=>BodyPartName(s.Path)==part).ToList();
                    if(peers.Count>1)part+=" "+(peers.IndexOf(slot)+1);
                    DrawValue(draft.Components,slot.Key,part+"承伤倍率",slot.Original,slot.Type);
                }
            }
            int armorModified=rule?.Components.Keys.Count(k=>k.Contains("DamageResistance")||k.Contains("AchillesArmor")||k.Contains("/Armor["))??0;
            if(!DrawFold("resistance/armor","护甲与冲击",0,armorModified,0,armorModified>0,1))return;
            EnsureGraph(selected);
            var defenses=selected.Slots.Where(s=>s.FieldName=="resistPercentage"&&s.ComponentType?.FullName=="HiddenUnits.DamageResistance"||
                s.ComponentType?.FullName=="TGCore.Library.AchillesArmor"||s.ComponentType==typeof(Armor)&&s.FieldName=="projectileHitForceMultiplier").ToList();
            foreach(var group in defenses.GroupBy(s=>s.ComponentType==typeof(Armor)?"弹丸冲击":"模组护甲"))
                if(DrawSlotFold("resistance/defense/"+group.Key,group.Key,group.ToList(),false,1))
                    foreach(var slot in group)DrawValue(draft.Components,slot.Key,slot.Label,slot.Original,slot.Type);
            if(defenses.Count==0)GUILayout.Label("无护甲参数",columnStyle,GUILayout.Height(40));
        }
        static string BodyPartName(string name)
        {
            string n=(name??"").ToLowerInvariant().Replace("_","").Replace(" ","");
            string side=n.Contains("left")||n.EndsWith("l")?"左":n.Contains("right")||n.EndsWith("r")?"右":"";
            if(n.Contains("head"))return "头部";
            if(n.Contains("torso")||n.Contains("chest"))return "躯干";
            if(n.Contains("hip")||n.Contains("pelvis"))return "髋部";
            if(n.Contains("elbow"))return side+"肘部";
            if(n.Contains("arm"))return side+"手臂";
            if(n.Contains("wrist")||n.Contains("hand"))return side+"手部";
            if(n.Contains("knee"))return side+"膝部";
            if(n.Contains("leg"))return side+"腿部";
            if(n.Contains("foot")||n.Contains("feet"))return side+"脚部";
            return Labels.HasChinese(name)?name:"部位";
        }
        static void TranslateCatalogPhrase(string __0,ref string __result)
        {
            if(Labels.HasChinese(__result))return;
            if(Labels.KnownUnitTranslation(__0,out var text)||Labels.KnownUnitTranslation(__result,out text))__result=text;
        }
    }
}
