using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        readonly FoldoutState parameterFolds = new FoldoutState();
        GUIStyle stateBadgeStyle, neutralBoolStyle;
        Color StateColor(FieldVisualState state) => ClassicUI ?
            (state==FieldVisualState.Error?new Color(1,.54f,.42f):state==FieldVisualState.Saved?new Color(.43f,.78f,.60f):new Color(.98f,.75f,.37f)) :
            (state==FieldVisualState.Error?new Color(.85f,.15f,.15f):state==FieldVisualState.Saved?new Color(0,.42f,.96f):new Color(.88f,.48f,0));
        Color StateBackground(FieldVisualState state) => ClassicUI ?
            (state==FieldVisualState.Error?new Color(.31f,.16f,.15f):state==FieldVisualState.Saved?new Color(.18f,.25f,.22f):new Color(.28f,.24f,.16f)) :
            (state==FieldVisualState.Error?new Color(1,.945f,.945f):state==FieldVisualState.Saved?new Color(.95f,.975f,1):new Color(1,.976f,.925f));
        static void Fill(Rect rect,Color color)
        {var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        void DrawStateBadge(Rect rect,FieldVisualState state,string text=null)
        {
            if(state==FieldVisualState.Default)return;
            var old=GUI.contentColor;GUI.contentColor=StateColor(state);
            GUI.Label(rect,text??EditPresentation.StateLabel(state),stateBadgeStyle);GUI.contentColor=old;
        }
        static int OverrideCount(UnitRule rule) => rule==null?0:(rule.Price.HasValue?1:0)+rule.Stats.Count+rule.Components.Count+rule.Constants.Count;
        Dictionary<string,string> SavedValues(Dictionary<string,string> values)
        {
            if(selected==null||!Settings.Units.TryGetValue(selected.Key,out var saved))return null;
            return ReferenceEquals(values,draft.Stats)?saved.Stats:ReferenceEquals(values,draft.Components)?saved.Components:saved.Constants;
        }
        FieldVisualState ValueState(Dictionary<string,string> values,string key)
        {
            bool enabled=values.TryGetValue(key,out var value);var saved=SavedValues(values);string previous=null;
            bool was=saved!=null&&saved.TryGetValue(key,out previous);
            return EditPresentation.FieldState(enabled,value,was,previous);
        }
        bool DrawFold(string key,string title,int count,int modified,int pending,bool initial=true,int depth=0)
        {
            key=(selected?.Key??moduleFilter)+"/"+tab+"/"+key;
            bool open=parameterFolds.IsOpen(key,initial);
            GUILayout.Space(depth==0?18:8);
            var rect=GUILayoutUtility.GetRect(1,44,GUILayout.ExpandWidth(true));rect.x+=depth*16;rect.width-=depth*16;
            if(GUI.Button(rect,(open?"⌄  ":"›  ")+title,groupStyle))
            {open=!open;parameterFolds.SetOpen(key,open);}
            if(pending>0||modified>0)GUI.Label(new Rect(rect.xMax-32,rect.y,30,44),"●",subStyle);
            return open;
        }
        bool DrawSlotFold(string key,string title,List<ValueSlot> slots,bool initial,int depth=0)
        {
            var saved=SavedValues(draft.Components);
            int modified=slots.Count(s=>saved?.ContainsKey(s.Key)==true);
            int pending=slots.Count(s=>{var state=ValueState(draft.Components,s.Key);return state==FieldVisualState.Draft||state==FieldVisualState.Restore;});
            return DrawFold(key,title,slots.Count,modified,pending,initial||modified>0||pending>0,depth);
        }
    }
}
