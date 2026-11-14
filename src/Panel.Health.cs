using System;
using System.Linq;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        string observedHealthKey,observedHealth="未摆放";
        float observedHealthUntil;

        double HealthStat(UnitRule rule,string field)
        {
            return rule!=null&&rule.Stats.TryGetValue(field,out var text)?
                Convert.ToDouble(Numbers.Parse(text,typeof(float))):Convert.ToDouble(EffectiveStat(selected,field));
        }
        string[] HealthValues(UnitRule rule)
        {
            try
            {
                double health=HealthStat(rule,"health"),multiplier=HealthStat(rule,"_healthMultiplier");
                if(multiplier==0)multiplier=1; // Native healthMultiplier getter.
                double size=HealthStat(rule,"sizeMultiplier"),min=HealthStat(rule,"minSizeRandom"),max=HealthStat(rule,"maxSizeRandom");
                if(min>max)return new[]{"需修正","需修正","需修正"};
                bool random=!selected.Blueprint.forceNoRandomSize&&min!=max;
                // RandomiseUnitSize leaves sizeRand at 1 when min == max;
                // forceNoRandomSize instead uses sizeMultiplier directly.
                double scale=selected.Blueprint.forceNoRandomSize||random?size:1;
                double combined=multiplier*scale*scale,nominal=health*combined;
                return new[]{HealthNumber(combined),HealthNumber(nominal),random?
                    HealthRange(nominal*min*min,nominal*max*max):HealthNumber(nominal)};
            }
            catch(FormatException){return new[]{"需修正","需修正","需修正"};}
            catch(OverflowException){return new[]{"需修正","需修正","需修正"};}
        }
        static string HealthNumber(double value)=>double.IsInfinity(value)?"无限":value.ToString("G7",Numbers.Culture);
        static string HealthRange(double min,double max)=>min==max?HealthNumber(min):HealthNumber(min)+" – "+HealthNumber(max);

        void DrawHealthOverview()
        {
            if(!DrawFold("health/overview","生成生命",0,0,0,true))return;
            var original=HealthValues(null);var current=HealthValues(draft);
            DrawReadOnlyValue("生成生命倍率",original[0],current[0]);
            DrawReadOnlyValue("体型折算生命",original[1],current[1]);
            DrawReadOnlyValue("常规生命范围",original[2],current[2]);
            if(observedHealthKey!=selected.Key||Time.unscaledTime>=observedHealthUntil)
            {
                observedHealthKey=selected.Key;observedHealthUntil=Time.unscaledTime+1;
                var health=FindObjectsOfType<Unit>().Where(u=>u&&u.unitBlueprint&&u.data&&
                    u.unitBlueprint.Entity.GUID==selected.Blueprint.Entity.GUID)
                    .Select(u=>(double)u.data.maxHealth).Where(v=>!double.IsNaN(v)).ToArray();
                observedHealth=health.Length==0?"未摆放":HealthRange(health.Min(),health.Max());
            }
            DrawReadOnlyValue("场上最大生命","—",observedHealth);
        }
        void DrawReadOnlyValue(string label,string original,string value)
        {
            if(!Matches(label))return;
            float height=Mathf.Max(58,valueLabelStyle.CalcHeight(new GUIContent(label),rowLabelWidth)+16);
            height=Mathf.Max(height,Mathf.Max(labelStyle.CalcHeight(new GUIContent(original),100),subStyle.CalcHeight(new GUIContent(value),324))+8);
            var row=GUILayoutUtility.GetRect(1,height,GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(row.x+12,row.y+4,row.width-552,height-8),label,labelStyle);
            GUI.Label(new Rect(row.xMax-532,row.y+4,100,height-8),original,labelStyle);
            GUI.Label(new Rect(row.xMax-420,row.y+4,324,height-8),value,subStyle);
            GUI.DrawTexture(new Rect(row.x+12,row.yMax-1,row.width-24,1),lineTexture);
        }
    }
}
