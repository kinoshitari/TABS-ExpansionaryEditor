using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        bool showAuxiliary;
        void DrawComponents()
        {
            if(selected.Slots==null)return;
            var slots=selected.Slots.Where(s=>tab==RightHandTab?s.Group.StartsWith("右"):s.Group.StartsWith("左"))
                .Where(s=>s.Readable&&s.FieldName!="enabled"&&Matches(s.Label)).ToList();
            if(slots.Count==0)GUILayout.Label("无参数",labelStyle);
            var sections=slots.GroupBy(s=>s.Key.Substring(0,s.Key.LastIndexOf('/')))
                .OrderBy(g=>g.Any(s=>s.ComponentName=="远程武器"||s.ComponentName=="近战武器")?0:g.Any(s=>s.ComponentName=="近战命中")?1:g.Any(s=>s.ComponentName=="弹丸命中")?2:g.Any(s=>s.ComponentName=="爆炸")?3:4).ToList();
            foreach(var section in sections)
            {
                string title=CombatSection(section.First());var peers=sections.Where(g=>CombatSection(g.First())==title).ToList();
                if(peers.Count>1)title+=" · 环节 "+(peers.IndexOf(section)+1);
                if(!DrawSlotFold("component/"+section.Key,title,section.ToList(),true))continue;
                var categories=section.GroupBy(v=>EditPresentation.Category(v.FieldName,v.Label)).ToList();
                foreach(var category in categories)
                {
                    bool main=category.Key=="攻击与装填"||category.Key=="伤害与命中";
                    if(categories.Count>1&&!DrawSlotFold("component/"+section.Key+"/"+category.Key,category.Key,category.ToList(),main,1))continue;
                    foreach(var slot in category.OrderBy(v=>FieldRank(v.FieldName)))DrawValue(draft.Components,slot.Key,slot.Label,slot.Original,slot.Type,slot.Note);
                }
            }
        }
        static string CombatSection(ValueSlot slot)
        {
            switch(slot.ComponentType?.Name) {
                case "RangeWeapon":return "发射与装填";case "MeleeWeapon":return "挥击与攻击距离";
                case "CollisionWeapon":return "近战命中";case "ProjectileHit":return "弹丸命中";
                case "Explosion":return "范围爆炸";case "DistanceBasedAmmo":return "远近攻击切换";
                case "DamageSwingCollision":return "挥击伤害";case "SpawnObject":return "连续发射";
                default:return "附加效果";
            }
        }
        void DrawAbilities()
        {
            if(selected.Abilities==null)return;
            var abilities=selected.Abilities.Where(a=>Matches(a.Name)||selected.Slots.Any(s=>s.Readable&&s.Key.StartsWith(a.Key+"/")&&Matches(s.Label))||selected.Constants.Any(s=>s.AbilityKey==a.Key&&s.Readable&&Matches(s.Label))).ToList();
            GUILayout.BeginHorizontal();
            GUILayout.Label("技能",sectionStyle,GUILayout.Height(44));
            if(GUILayout.Button(showAuxiliary?"收起辅助":"辅助动作",buttonStyle,GUILayout.Width(132),GUILayout.Height(44)))showAuxiliary=!showAuxiliary;
            GUILayout.EndHorizontal();int shown=0;
            foreach(var ability in abilities.Where(a=>showAuxiliary||!a.Auxiliary).OrderBy(a=>a.Key.StartsWith("C")?0:1))
            {
                shown++;
                var slots=selected.Slots.Where(s=>s.Readable&&s.Key.StartsWith(ability.Key+"/",StringComparison.Ordinal)&&s.FieldName!="enabled").ToList();
                var constants=selected.Constants.Where(s=>s.Readable&&s.AbilityKey==ability.Key).ToList();
                var savedComponents=SavedValues(draft.Components);var savedConstants=SavedValues(draft.Constants);
                int modified=slots.Count(s=>savedComponents?.ContainsKey(s.Key)==true)+constants.Count(s=>savedConstants?.ContainsKey(s.Key)==true);
                Func<FieldVisualState,bool> pendingState=v=>v==FieldVisualState.Draft||v==FieldVisualState.Restore;
                int pending=slots.Count(s=>pendingState(ValueState(draft.Components,s.Key)))+constants.Count(s=>pendingState(ValueState(draft.Constants,s.Key)));
                if(!DrawFold("ability/"+ability.Key,ability.Name,slots.Count+constants.Count,modified,pending,true))continue;
                
                bool ownCooldown=slots.Any(s=>s.FieldName=="cooldown"&&s.ComponentType!=null&&CombatAssemblies.Contains(s.ComponentType.Assembly))||constants.Count>0;
                var primary=slots.Where(s=>IsPrimary(s)&&(!ownCooldown||s.FieldName!="value"||!s.Label.Contains("冷却"))).ToList();
                if(primary.Count+constants.Count>0&&DrawFold("ability/"+ability.Key+"/primary","主要设置",primary.Count+constants.Count,
                    primary.Count(s=>savedComponents?.ContainsKey(s.Key)==true)+constants.Count(s=>savedConstants?.ContainsKey(s.Key)==true),
                    primary.Count(s=>pendingState(ValueState(draft.Components,s.Key)))+constants.Count(s=>pendingState(ValueState(draft.Constants,s.Key))),true,1))
                {
                    foreach(var slot in primary)DrawAbilityValue(slot,slots,ownCooldown);
                    foreach(var slot in constants)DrawValue(draft.Constants,slot.Key,slot.Label,slot.Original,slot.Double?typeof(double):typeof(float),slot.Note);
                }
                if(!primary.Any(s=>s.Label.Contains("冷却"))&&constants.Count==0)GUILayout.Space(0);
                foreach(var group in slots.Except(primary).GroupBy(s=>EditPresentation.Category(s.FieldName,s.Label)))
                {
                    if(!DrawSlotFold("ability/"+ability.Key+"/"+group.Key,group.Key,group.ToList(),false,1))continue;
                    foreach(var slot in group.OrderBy(s=>FieldRank(s.FieldName)))DrawAbilityValue(slot,slots,ownCooldown);
                }
            }
            if(shown==0)GUILayout.Label("无技能",columnStyle);
        }
        static bool IsPrimary(ValueSlot slot)=>slot.Label.Contains("冷却")&&slot.Type!=typeof(bool)&&!slot.Label.Contains("随机")||
            new[]{"range","activationRange","attackRange","amountToSpawn"}.Contains(slot.FieldName)||slot.FieldName=="value"&&slot.Label.StartsWith("触发距离");
        void DrawAbilityValue(ValueSlot slot,List<ValueSlot> all,bool ownCooldown)
        {
            string label=slot.Label;
            var same=all.Where(s=>s.Label==slot.Label).ToList();
            if(same.Count>1)label+=" · 环节 "+(same.IndexOf(slot)+1);
            if(slot.FieldName=="cooldown")label="技能冷却（秒）"+(same.Count>1?" · 环节 "+(same.IndexOf(slot)+1):"");
            if(ownCooldown&&slot.FieldName=="value"&&slot.Label.Contains("冷却"))label="辅助动作触发间隔（秒）"+(same.Count>1?" · 环节 "+(same.IndexOf(slot)+1):"");
            DrawValue(draft.Components,slot.Key,label,slot.Original,slot.Type,slot.Note);
        }
        static int FieldRank(string field)
        {var main=new[]{"enabled","damage","internalCooldown","maxRange","reloadTime","magSize","numberOfObjects","radius","percentDamage","force","spread","allowedHits"};int index=Array.IndexOf(main,field);return index<0?100:index;}
    }
}
