using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        int unitFilter;
        bool sortByPrice, hideNavigation;
        GUIStyle listStyle, selectedListStyle, groupStyle, primaryStyle;
        readonly Dictionary<string,FoldoutState> factionFoldouts = new Dictionary<string,FoldoutState>();
        FoldoutState FactionFolds
        {get{if(!factionFoldouts.TryGetValue(moduleFilter,out var state))factionFoldouts[moduleFilter]=state=new FoldoutState();state.SetQuery(search);return state;}}
        sealed class ModuleView { public string Search, Selected; public int Filter; public Vector2 Scroll; }
        readonly Dictionary<string, ModuleView> moduleViews = new Dictionary<string, ModuleView>();
        IEnumerable<UnitEntry> Filtered()
        {
            var result = ModuleEntries().Where(e =>
                (unitFilter != 1 || (Settings.Units.TryGetValue(e.Key,out var rule)&&OverrideCount(rule)>0)) && (unitFilter != 2 || batch.Contains(e.Key)) &&
                (string.IsNullOrWhiteSpace(search) || (e.Name + Labels.UnitName(e) + e.Faction + Labels.Faction(e.Faction)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            return sortByPrice ? result.OrderBy(e => Settings.Units.TryGetValue(e.Key, out var r) && r.Price.HasValue ? r.Price.Value : (double)e.OriginalPrice) : result;
        }
        void DrawUnits(float height)
        {
            GUI.SetNextControlName("unit-search");search=GUILayout.TextField(search,inputStyle,GUILayout.Height(44));
            if(string.IsNullOrEmpty(search)&&Event.current.type==EventType.Repaint)
            {var rect=GUILayoutUtility.GetLastRect();rect.x+=14;GUI.Label(rect,"搜索",columnStyle);}
            GUILayout.Space(12);unitFilter=GUILayout.Toolbar(unitFilter,new[]{"全部","已改","已选"},buttonStyle,GUILayout.Height(40));
            GUILayout.Space(10);GUILayout.BeginHorizontal();
            if(GUILayout.Button("全选",listStyle,GUILayout.Height(36))){foreach(var e in Filtered())batch.Add(e.Key);InvalidateBatch();}
            if(GUILayout.Button("清空",listStyle,GUILayout.Height(36))){batch.Clear();InvalidateBatch();}
            if(GUILayout.Button(sortByPrice?"价格 ↑":"价格",listStyle,GUILayout.Height(36)))sortByPrice=!sortByPrice;
            GUILayout.EndHorizontal();GUILayout.Space(6);GUILayout.BeginHorizontal();
            if(GUILayout.Button("展开",listStyle,GUILayout.Height(36)))FactionFolds.SetAll(ModuleEntries().Select(e=>e.Faction).Distinct(),true);
            if(GUILayout.Button("收起",listStyle,GUILayout.Height(36)))FactionFolds.SetAll(ModuleEntries().Select(e=>e.Faction).Distinct(),false);
            GUILayout.EndHorizontal();GUILayout.Space(10);
            var visible=Filtered().ToList();var folds=FactionFolds;
            unitsScroll=GUILayout.BeginScrollView(unitsScroll,false,false,GUILayout.Height(Mathf.Max(100,height-210)));
            foreach(var group in visible.GroupBy(e=>e.Faction))
            {
                string key=group.Key;bool open=folds.IsOpen(key);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button((open?"⌄  ":"›  ")+Labels.Faction(group.Key),groupStyle,GUILayout.Height(40)))
                {open=!open;folds.SetOpen(key,open);}
                bool all=group.All(e=>batch.Contains(e.Key));
                if(GUILayout.Button(all?"✓":"+",listStyle,GUILayout.Width(30),GUILayout.Height(40)))
                {foreach(var e in group){if(all)batch.Remove(e.Key);else batch.Add(e.Key);}InvalidateBatch();}
                GUILayout.EndHorizontal();if(!open)continue;
                foreach(var entry in group)
                {
                    var rect=GUILayoutUtility.GetRect(1,40,GUILayout.ExpandWidth(true));
                    Settings.Units.TryGetValue(entry.Key,out var rule);
                    bool modified=OverrideCount(rule)>0||selected==entry&&dirty;
                    if(selected==entry)GUI.Box(rect,GUIContent.none,activeStyle);
                    bool old=batch.Contains(entry.Key),next=GUI.Toggle(new Rect(rect.x+5,rect.y+7,26,26),old,old?"✓":"",checkStyle);
                    if(next!=old){if(next)batch.Add(entry.Key);else batch.Remove(entry.Key);InvalidateBatch();}
                    if(GUI.Button(new Rect(rect.x+37,rect.y,rect.width-101,40),Labels.UnitName(entry),modified?selectedListStyle:listStyle)&&selected!=entry)
                    {if(dirty){pendingSelection=entry;confirm="switch";}else Select(entry);}
                    GUI.Label(new Rect(rect.xMax-64,rect.y,62,40),rule?.Price?.ToString()??entry.OriginalPrice.ToString(),modified?subStyle:columnStyle);
                }
            }
            if(visible.Count==0)GUILayout.Label("无结果",columnStyle);
            GUILayout.EndScrollView();
        }
    }
}
