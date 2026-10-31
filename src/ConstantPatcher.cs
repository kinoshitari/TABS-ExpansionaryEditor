using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ExpansionaryEditor
{
    public static class ConstantPatcher
    {
        static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();
        public static string Key(MethodBase method,int index) => EditorPlugin.ComponentId(method.DeclaringType)+"|"+method.MetadataToken+"|"+index;
        public static void Ensure(IEnumerable<ConstantSlot> slots,UnitRule rule,Harmony harmony)
        {
            foreach(var slot in slots)
                if(rule.Constants.ContainsKey(slot.Key) && Patched.Add(slot.Method))
                    harmony.Patch(slot.Method,transpiler:new HarmonyMethod(typeof(ConstantPatcher),nameof(Rewrite)));
        }
        static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {
            int index=0;
            foreach(var code in instructions)
            {
                bool single=code.opcode==OpCodes.Ldc_R4;
                if(single || code.opcode==OpCodes.Ldc_R8)
                {
                    string key=Key(__originalMethod,index++);
                    var load=new CodeInstruction(code) {opcode=OpCodes.Ldarg_0,operand=null};
                    yield return load;
                    yield return new CodeInstruction(OpCodes.Ldstr,key);
                    yield return new CodeInstruction(code.opcode,code.operand);
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ConstantPatcher),single?nameof(Single):nameof(Double)));
                }
                else yield return code;
            }
        }
        public static float Single(object owner,string key,float original) => (float)Read(owner,key,original);
        public static double Double(object owner,string key,double original) => Read(owner,key,original);
        static double Read(object owner,string key,double original)
        {
            var plugin=EditorPlugin.Instance;
            if(plugin==null || !plugin.Settings.Enabled)return original;
            string unit=FindOwner(owner,0);
            if(unit!=null && plugin.Settings.Units.TryGetValue(unit,out var rule) && rule.Constants.TryGetValue(key,out var value))
                return double.Parse(value,Numbers.Culture);
            return original;
        }
        static string FindOwner(object owner,int depth)
        {
            if(owner==null || depth>2)return null;
            if(owner is Component component && component)
            {
                for(var t=component.transform;t!=null;t=t.parent)
                {var tag=t.GetComponent<OwnerTag>();if(tag)return tag.UnitKey;}
                return null;
            }
            // Compiler-generated coroutine state machines keep the originating MonoBehaviour here.
            var outer=AccessTools.Field(owner.GetType(),"<>4__this");
            return outer==null?null:FindOwner(outer.GetValue(owner),depth+1);
        }
    }
}
