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
                int direction=i<2?-1:1;decimal rate=i==0||i==3?.10m:.01m;
                bool available=TryNumberChange(key,text,type,direction,rate,out var changed);
                GUI.enabled=enabled&&!Composing&&available;
                float x=i<2?rect.x+i*(button+gap):rect.xMax-(4-i)*(button+gap)+gap;
                string arrow=i==0?"<<":i==1?"<":i==2?">":">>";
                if(GUI.Button(new Rect(x,rect.y,button,rect.height),arrow,numberArrowStyle))
                {text=changed;GUI.FocusControl(null);}
            }
            GUI.enabled=enabled;return text;
        }

        bool TryNumberChange(string key,string text,Type type,int direction,decimal rate,out string result)
        {
            result=text;
            try
            {
                // Never discard an unfinished or invalid typed value to make a step possible.
                object current=Numbers.Parse(text,type);
                if(!decimal.TryParse(Numbers.Normalize(text),NumberStyles.Float,Numbers.Culture,out var number))return false;
                bool integer=type==typeof(int)||type==typeof(uint);
                // Relative tiers work for prices and multipliers without a fixed tick size.
                decimal amount=Math.Abs(number==0?1:number)*rate;
                if(integer)amount=Math.Max(1,Math.Round(amount,0,MidpointRounding.AwayFromZero));
                decimal next=number+direction*amount;
                decimal minimum=type==typeof(uint)?0:decimal.MinValue;
                if(key=="price")minimum=0;
                if(IsResistanceKey(key))minimum=0;
                var stat=Labels.Stats.FirstOrDefault(d=>d.Field==key);
                if(stat!=null)minimum=(decimal)stat.Min;
                var slot=selected?.Slots?.FirstOrDefault(s=>s.Key==key);
                if(slot?.ComponentType==typeof(DamageMultiplier))minimum=0;
                if(slot!=null&&slot.Label.Contains("冷却"))minimum=0;
                if(slot?.FieldName=="magSize")minimum=1;
                if(key.EndsWith("/@count")){minimum=0;next=Math.Min(next,128);}
                if(selected?.Constants?.Any(s=>s.Key==key&&s.Readable)==true)minimum=0;
                next=Math.Max(next,minimum);
                if(direction*(next-number)<=0)return false;
                var parsed=Numbers.Parse(next.ToString(Numbers.Culture),type);
                if(Convert.ToDouble(parsed)==Convert.ToDouble(current))return false;
                result=Numbers.Text(parsed);
                return string.IsNullOrEmpty(FieldIssue(key,result,type));
            }
            catch(FormatException){return false;}
            catch(OverflowException){return false;}
        }
    }
}
