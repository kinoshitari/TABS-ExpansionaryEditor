using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        string DrawNumberInput(Rect rect,string control,string key,string text,Type type,GUIStyle style)
        {
            const float button=44,gap=4,side=2*(button+gap);
            var field=new Rect(rect.x+side,rect.y,rect.width-2*side,rect.height);
            GUI.SetNextControlName(control);
            text=GUI.TextField(field,text,style);
            if(GUI.GetNameOfFocusedControl()==control)
                Input.compositionCursorPos=GUIUtility.GUIToScreenPoint(new Vector2(field.x,field.yMax));
            bool enabled=GUI.enabled;
            for(int i=0;i<4;i++)
            {
                int direction=i<2?-1:1;bool large=i==0||i==3;
                bool available=TryNumberChange(key,text,type,direction,large,out var changed);
                GUI.enabled=enabled&&!Composing&&available;
                float x=i<2?rect.x+i*(button+gap):rect.xMax-(4-i)*(button+gap)+gap;
                string arrow=i==0?"<<":i==1?"<":i==2?">":">>";
                if(GUI.Button(new Rect(x,rect.y,button,rect.height),arrow,numberArrowStyle))
                {text=changed;GUI.FocusControl(null);}
            }
            GUI.enabled=enabled;return text;
        }

        static decimal NumberStep(decimal number,bool integer,bool large)
        {
            decimal magnitude=Math.Abs(number),unit=integer?1m:.01m;
            // Stable 1/2/5 steps per decade. Use finer decimals only for tiny values.
            if(!integer&&magnitude>0)while(magnitude<unit)unit/=10m;
            while(unit<=magnitude/1000m)unit*=10m;
            decimal step=magnitude>=unit*500m?unit*5m:magnitude>=unit*200m?unit*2m:unit;
            return large?step*10m:step;
        }

        bool TryNumberChange(string key,string text,Type type,int direction,bool large,out string result)
        {
            result=text;
            try
            {
                // Never discard an unfinished or invalid typed value to make a step possible.
                object current=Numbers.Parse(text,type);
                if(!decimal.TryParse(Numbers.Normalize(text),NumberStyles.Float,Numbers.Culture,out var number))return false;
                bool integer=type==typeof(int)||type==typeof(uint);
                decimal amount=NumberStep(number,integer,large);
                // Snap toward the requested direction, removing old fractional tails.
                decimal next=(direction>0?Math.Floor(number/amount)+1:Math.Ceiling(number/amount)-1)*amount;
                decimal minimum=type==typeof(uint)?0:decimal.MinValue;
                if(key=="price")minimum=0;
                if(IsResistanceKey(key))minimum=0;
                var stat=Labels.Stats.FirstOrDefault(d=>d.Field==key);
                if(stat!=null)minimum=(decimal)stat.Min;
                var slot=selected?.Slots?.FirstOrDefault(s=>s.Key==key);
                if(slot?.ComponentType==typeof(DamageMultiplier))minimum=0;
                if(slot!=null&&slot.Label.Contains("冷却"))minimum=0;
                if(slot?.FieldName=="magSize")minimum=1;
                if(slot!=null&&slot.Minimum.HasValue)minimum=(decimal)slot.Minimum.Value;
                if(key.EndsWith("/@count")){minimum=0;next=Math.Min(next,128);}
                if(selected?.Constants?.Any(s=>s.Key==key&&s.Readable)==true)minimum=0;
                next=Math.Max(next,minimum);
                if(direction*(next-number)<=0)return false;
                var parsed=Numbers.Parse(next.ToString(Numbers.Culture),type);
                if(Convert.ToDouble(parsed)==Convert.ToDouble(current))return false;
                result=next.ToString("0.############################",Numbers.Culture);
                return string.IsNullOrEmpty(FieldIssue(key,result,type));
            }
            catch(FormatException){return false;}
            catch(OverflowException){return false;}
        }
    }
}
