using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExpansionaryEditor
{
    // Reapply user settings after initialization; several Expansionary skills assign their parameters in Start.
    public static class RuntimeFields
    {
        static readonly HashSet<MethodBase> Patched=new HashSet<MethodBase>();
        public static void Ensure(IEnumerable<ValueSlot> slots,UnitRule rule,Harmony harmony)
        {
            foreach(var type in slots.Where(s=>rule.Components.ContainsKey(s.Key)&&s.ComponentType!=null).Select(s=>s.ComponentType).Distinct())
            {
                var method=AccessTools.Method(type,"Start",Type.EmptyTypes);
                if(method!=null && !method.IsAbstract && method.GetMethodBody()!=null && Patched.Add(method))
                    harmony.Patch(method,prefix:new HarmonyMethod(typeof(RuntimeFields),nameof(AfterStart)),postfix:new HarmonyMethod(typeof(RuntimeFields),nameof(AfterStart)));
            }
        }
        static void AfterStart(Component __instance)
        {
            var p=EditorPlugin.Instance;
            if(p==null || !p.Settings.Enabled)return;
            OwnerTag tag=null;
            var route=new List<int>();
            for(var t=__instance.transform;t!=null;t=t.parent)
            {
                tag=t.GetComponent<OwnerTag>();if(tag)break;
                route.Add(t.GetSiblingIndex());
            }
            if(!tag || !p.Settings.Units.TryGetValue(tag.UnitKey,out var rule))return;
            route.Reverse();
            string path=tag.RootPath+string.Concat(route.Select(i=>"/child["+i+"]"));
            var type=__instance.GetType();
            int ordinal=Array.IndexOf(__instance.GetComponents(type),__instance);
            path+="/"+EditorPlugin.ComponentId(type)+"["+ordinal+"]/";
            foreach(var pair in rule.Components)
            {
                if(!pair.Key.StartsWith(path,StringComparison.Ordinal))continue;
                string name=pair.Key.Substring(path.Length);
                if(name=="@enabled" && __instance is Behaviour b)b.enabled=bool.Parse(pair.Value);
                else AssignPath(__instance,name.Split('/'),0,pair.Value);
            }
        }
        static void AssignPath(object owner,string[] parts,int index,string value)
        {
            if(owner==null||index>=parts.Length)return;
            var token=parts[index];int bracket=token.IndexOf('[');string name=bracket<0?token:token.Substring(0,bracket);
            var field=AccessTools.Field(owner.GetType(),name);if(field==null)return;
            if(index==parts.Length-1&&Numbers.Scalar(field.FieldType))
            {field.SetValue(owner,Numbers.Parse(value,field.FieldType));return;}
            object next=field.GetValue(owner);
            if(bracket>=0&&next is Array array&&int.TryParse(token.Substring(bracket+1).TrimEnd(']'),out var n)&&n<array.Length)next=array.GetValue(n);
            AssignPath(next,parts,index+1,value);
        }
    }
}
