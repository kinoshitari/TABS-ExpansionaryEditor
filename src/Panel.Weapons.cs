using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        string weaponSyncNote="";
        bool SameHandWeapons=>selected!=null&&selected.Right&&selected.Left&&selected.Right==selected.Left;
        void DrawWeaponSync()
        {
            if(!SameHandWeapons)return;
            GUILayout.BeginHorizontal();
            if(weaponSyncNote.Length>0)GUILayout.Label(RemarkText(weaponSyncNote),smallStyle,GUILayout.MinHeight(42));
            GUILayout.FlexibleSpace();bool enabled=GUI.enabled;GUI.enabled=enabled&&!Composing;
            string destination=tab==RightHandTab?"左手":"右手";
            if(GUILayout.Button("同步到"+destination,buttonStyle,GUILayout.Width(160),GUILayout.Height(42)))Guard(SyncOtherHand);
            GUI.enabled=enabled;GUILayout.EndHorizontal();GUILayout.Space(8);
        }
        void SyncOtherHand()
        {
            if(!SameHandWeapons||!ComponentTab||tab==AbilitiesTab||Composing)return;
            string from=tab==RightHandTab?"R/":"L/",to=tab==RightHandTab?"L/":"R/";
            var source=selected.Slots.Where(s=>s.Key.StartsWith(from,StringComparison.Ordinal)).ToList();
            var destination=selected.Slots.Where(s=>s.Key.StartsWith(to,StringComparison.Ordinal)).ToDictionary(s=>s.Key);
            if(source.Count!=destination.Count||source.Any(s=>!destination.TryGetValue(to+s.Key.Substring(2),out var other)||other.Type!=s.Type))
                throw new InvalidOperationException("双手参数路径不一致，未同步");
            var next=EditData.Clone(draft);
            foreach(var slot in source)
            {
                string key=to+slot.Key.Substring(2);
                if(draft.Components.TryGetValue(slot.Key,out var text))
                {
                    var value=Numbers.Parse(text,slot.Type);string issue=FieldIssue(slot.Key,text,slot.Type);
                    if(issue.Length>0)throw new FormatException(slot.Label+"："+issue);
                    next.Components[key]=Numbers.Text(value);
                }
                else next.Components.Remove(key);
            }
            bool changed=!EditData.Same(draft,next);draft=next;
            foreach(var key in fieldErrors.Keys.Where(k=>k.StartsWith(to,StringComparison.Ordinal)).ToArray())fieldErrors.Remove(key);
            pendingTextCommit.RemoveWhere(k=>k.StartsWith(to,StringComparison.Ordinal));GUI.FocusControl(null);RecheckDirty();
            weaponSyncNote=changed?"已同步到"+(tab==RightHandTab?"左手":"右手"):"双手修改一致";
        }
        void DrawWeaponEffects(List<IGrouping<string,ValueSlot>> groups)
        {
            if(groups.Count==0)return;
            var all=groups.SelectMany(g=>g).ToList();
            if(!DrawSlotFold("weapon-effects","特殊效果",all,true))return;
            foreach(var group in groups)
            {
                var first=group.First();var peers=groups.Where(g=>g.First().EffectName==first.EffectName).ToList();
                string title=first.EffectName+(peers.Count>1?"（"+(peers.IndexOf(group)+1)+"）":"");
                if(!DrawSlotFold("weapon-effect/"+group.Key,title,group.ToList(),true,1))continue;
                foreach(var slot in group)DrawValue(draft.Components,slot.Key,slot.Label,slot.Original,slot.Type);
                if(DrawFold("weapon-effect/note/"+group.Key,"注释",0,0,0,false,2))
                {
                    DrawRemark(first.Note);
                    DrawRemark("开关仅控制此入口后续施加效果，不改变普通命中伤害，也不移除目标已有状态。");
                }
            }
        }
    }
}
