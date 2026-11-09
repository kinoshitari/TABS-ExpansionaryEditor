using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        sealed class DamageAnnotation
        {
            internal Type Owner;
            internal readonly HashSet<string> Methods=new HashSet<string>();
            internal readonly HashSet<UnitEntry> Units=new HashSet<UnitEntry>();
        }
        // Display metadata only. The persisted resistance keys and routing stay in DamageSources.
        static readonly Dictionary<string,List<DamageAnnotation>> DamageAnnotations=new Dictionary<string,List<DamageAnnotation>>();
        static void RecordDamageAnnotation(string key,Type owner,MethodBase method)
        {
            if(!DamageAnnotations.TryGetValue(key,out var entries))DamageAnnotations[key]=entries=new List<DamageAnnotation>();
            var entry=entries.FirstOrDefault(e=>e.Owner==owner);
            if(entry==null){entry=new DamageAnnotation{Owner=owner};entries.Add(entry);}
            entry.Methods.Add(method.DeclaringType.FullName+"."+method.Name);
        }
        internal static void RecordDamageSourceUnit(UnitEntry unit,IEnumerable<Type> componentTypes)
        {
            var types=componentTypes.ToList();
            foreach(var entry in DamageAnnotations.Values.SelectMany(e=>e))
                if(types.Any(t=>entry.Owner.IsAssignableFrom(t)))entry.Units.Add(unit);
        }
        static string DamageModName(Assembly assembly)
        {
            if(assembly==typeof(Unit).Assembly)return "TABS 原版";
            switch(SourceAssemblyName(assembly))
            {
                case "扩张伤害":return "扩张（Expansionary）";
                case "隐藏伤害":return "隐藏（HiddenUnits）";
                case "圣诞节伤害":return "圣诞节（Holiday）";
                case "动物王国伤害":return "动物王国（AnimalKingdom）";
                case "中国神话伤害":return "中国神话（Chinese_myth）";
                case "臭臭伤害":return "臭臭（ChouChou）";
                case "TGCore 效果":return "TGCore 公共效果库";
                default:return assembly.GetName().Name;
            }
        }
        static readonly Dictionary<string,string> DamageMechanisms=new Dictionary<string,string> {
            {"TGCore.Library.Effect_Bleed","流血效果的持续扣血。"},
            {"TGCore.Library.Effect_Disintegrate","分解效果添加时产生的扣血。"},
            {"TGCore.Library.Effect_Lightning","雷电效果及其协程中的扣血。"},
            {"TGCore.Library.Effect_Transformation","变身流程中调用受伤入口的部分；不阻止变身本身。"},
            {"TGCore.Library.Effect_Zombie","僵尸效果检查流程中的扣血；不阻止替换或生成单位。"},
            {"TGCore.Library.ProjectileHitLeech","吸血弹丸的回血调用，使用负伤害表示治疗；正常回血不受承伤倍率影响。"},
            {"TGCore.Library.ProjectileHitPercentDamage","百分比弹丸算出伤害后传给受伤入口的数值。"},
            {"TGCore.Library.ProjectileDmgFromHealth","按生命计算伤害的弹丸，调整其最终传入受伤入口的伤害。"},
            {"TGCore.Library.CollisionWeaponToggleable","可开关碰撞武器的命中与自伤调用，不改变武器的开关状态。"},
            {"TGCore.Library.KillSelf","自伤脚本通过受伤入口造成的伤害，不覆盖直接死亡指令。"},
            {"TGCore.Library.SpikeBehavior","尖刺附着时的命中与持续扣血，不改变附着状态。"},
            {"TGCore.HarmonyPatches.WaterDamagePatch","TGCore 水域补丁产生的扣血，来源是补丁而非某个专属兵种。"},
            {"HiddenUnits.AngelBubble","气泡拉拽流程中的持续扣血，不改变拉力。"},
            {"HiddenUnits.BillyLaser","比利激光的扣血，不改变光束外观或射程。"},
            {"HiddenUnits.ChainUnits","锁链效果及锁链协程中的扣血。"},
            {"HiddenUnits.Effect_Garlic","大蒜效果脚本调用受伤入口的部分。"},
            {"HiddenUnits.Effect_Petrification","石化协程中的扣血；不改变石化控制。"},
            {"HiddenUnits.ChickenSticks","碰撞脚本的命中扣血，不改变碰撞反弹。"},
            {"HiddenUnits.HadezHands","冥王之手攻击协程中的持续扣血。"},
            {"HiddenUnits.HammerBounceRig","弹跳锤碰撞时的扣血，不改变弹跳力度。"},
            {"HiddenUnits.RootBehavior","根须附着时及附着后的扣血，不解除束缚。"},
            {"HiddenUnits.TriggerCollisionWeapon","触发器武器命中时的扣血。"},
            {"Holiday.BoulderBehavior","雪球碰撞时产生的伤害，不改变碰撞作用力。"},
            {"AnimalKingdom.Effect_Plague","动物王国瘟疫检查流程中的扣血；不阻止单位替换。"},
            {"Effect_Plague","臭臭瘟疫检查流程中的扣血；不阻止单位替换。"},
            {"ReaperWingsS","臭臭收割之翼延迟攻击协程中的扣血。"},
            {"EatMove","吞食脚本中的正伤害；回血、生命上限修改和销毁单位不受此项影响。"},
            {"TheCountAttack","伯爵脚本当前调用为负伤害治疗，正常回血不受承伤倍率影响。"},
            {"BatHealth","蝙蝠生命脚本当前调用为负伤害治疗，正常回血不受承伤倍率影响。"},
            {"TowerAttack","高塔攻击协程中的扣血。"},
            {"AddonExpansionary.BioChemicalEffectBase","生化效果更新时的扣血。"},
            {"AddonExpansionary.BloatedBlood","血液脚本含取负数后传入受伤入口的调用；治疗部分不缩放。"},
            {"AddonExpansionary.PrayethMechanics","祈祷机制更新时经受伤入口结算的正伤害。"},
            {"AddonExpansionary.HealthDegeneration","生命衰减脚本按时间结算的扣血。"},
            {"AddonExpansionary.HellbodyMethod","该命中效果中经受伤入口结算的正伤害；负伤害回血不缩放。"},
            {"AddonExpansionary.HungerDebuff","饥饿效果更新回调中的扣血，不改变饥饿状态。"},
            {"AddonExpansionary.StatueMimicryMovement","雕像模仿动作中经受伤入口结算的扣血。"},
            {"AddonExpansionary.ExpansionaryWaterModifier","扩张水域脚本含负伤害回血；正常治疗不受承伤倍率影响。"},
            {"AddonExpansionary.WendigoFoggyChaseMovement","雾中追踪脚本当前两处调用均为负数回血，正常治疗不受承伤倍率影响。"},
            {"AddonExpansionary.PuppetFunctionality","傀儡脚本更新时经受伤入口结算的扣血。"},
            {"AddonExpansionary.ComboSlashMovement","连斩动作中经受伤入口结算的命中伤害。"},
            {"AddonExpansionary.TimeDanceMovement","时间之舞动作协程中的扣血，不改变动作或时间控制。"},
            {"AddonExpansionary.MyconidEffectBase","蕈类效果启动时经受伤入口结算的扣血。"},
            {"AddonExpansionary.ElderBodyPassive","被动治疗函数将参数取负后传入受伤入口，正常回血不受承伤倍率影响。"},
            {"AddonExpansionary.HomeRun","本垒打挥击产生的扣血，不改变击飞作用力。"},
            {"AddonExpansionary.HangEntanglement","缠绕脚本更新时的扣血，不解除缠绕。"},
            {"AddonExpansionary.ClickAndDragProjectile","拖拽弹丸更新时的扣血，不改变拖拽控制。"},
            {"AddonExpansionary.AbsoluteEclipseTransformationMechanic","变身协程含补足生命的负伤害调用；治疗不缩放，也不阻止变身。"},
            {"AddonExpansionary.HealthCocoonBehaviour","生命茧脚本中的正伤害；同脚本的负伤害治疗不缩放。"},
            {"AddonExpansionary.BloodlordTributeMovement","血液融合与死亡锚定流程中的正伤害；同脚本的治疗不缩放。"},
            {"AddonExpansionary.HexEffect","诅咒调用中的正伤害；同脚本的负伤害治疗不缩放。"},
            {"AddonExpansionary.DoppelgangingFunction","分身协同斩击经受伤入口结算的伤害。"},
            {"AddonExpansionary.WyrmMovement","巨龙撕咬协程中经受伤入口结算的正伤害。"},
            {"AddonExpansionary.BlightenedBrideMovement","新娘脚本含向另一单位传入负伤害的调用，治疗部分不缩放。"},
            {"AddonExpansionary.DamageSwingCollision","挥击碰撞脚本经受伤入口结算的命中伤害。"},
            {"AddonExpansionary.WayangDamageCollision","皮影碰撞脚本经受伤入口结算的命中伤害。"},
            {"AddonExpansionary.CollisionDamage","扩张碰撞脚本的扣血，与原版碰撞来源分开。"},
            {"AddonExpansionary.AbsorbValhemionShield","护盾命中效果传入受伤入口的正伤害；治疗和护盾机制本身不缩放。"},
            {"AddonExpansionary.DeflectOnSwingBehaviour","挥击偏转脚本含取负数的受伤调用；治疗返还部分不缩放。"},
            {"AddonExpansionary.AcidEffectBase","酸蚀效果协程中的扣血；不改变附带控制。"},
            {"AddonExpansionary.RustedEffectBase","锈蚀效果触发时的扣血。"},
            {"AddonExpansionary.StonedEffect","石化协程中的扣血；不改变石化控制。"},
            {"AddonExpansionary.CritSlashEffectBase","暴击斩击调用受伤入口的部分，不改变暴击触发条件。"},
            {"AddonExpansionary.MageForceEffectBase","法师力场更新时的扣血，不改变力场推力。"},
            {"AddonExpansionary.NightstalkConfusion","暗袭混乱脚本更新时的扣血；不改变混乱状态。"},
            {"AddonExpansionary.PurifierEffect","净化打击协程中的扣血。"},
            {"AddonExpansionary.ChompingEffect","吞噬效果协程中的扣血；不取消附带控制。"},
            {"AddonExpansionary.RangeDamageSelf","自身伤害脚本调用受伤入口的部分。"},
            {"TakeDamagePercent","臭臭百分比伤害脚本计算后传入受伤入口的数值。"},
            {"NiuEngerMove","牛魔王狂暴当前传入 -500，表示治疗；此承伤项不改变这次回血。"},
            {"SuperStick","超级一棒攻击协程中的扣血，不改变金箍棒的运动。"},
            {"rwdseerfd.角色类.机械霸王龙.碰撞伤害脚本23","机械霸王龙烫伤触发时的扣血。"},
            {"rwdseerfd.角色类.机械霸王龙.霸王龙撕咬伤害脚本","机械霸王龙撕咬目标时的扣血。"},
            {"rwdseerfd.角色类.恶魔巫师.胸口雷电光线实例化","恶魔巫师胸口雷电光线检测逻辑中的扣血。"},
            {"rwdseerfd.角色类.小兵类.毒液佣兵.毒药叠加层数","毒液佣兵毒药叠层脚本受伤调用中的扣血，不改变层数。"},
            {"rwdseerfd.伤害计算事件","臭臭通用伤害事件经受伤入口结算的部分；绕过入口的其他分支不受此项影响。"},
            {"rwdseerfd.单位状态控制器","状态控制器对当前单位或其目标调用受伤入口的正伤害。"},
            {"rwdseerfd.位置召唤游戏物体324","物体召唤脚本更新时的扣血，不改变召唤本身。"},
            {"rwdseerfd.角色类.检测接口.触发器碰撞伤害基类","通用触发器碰撞检测产生的扣血。"},
            {"rwdseerfd.角色类.恶魔巫师.灵魂移动到物体","恶魔巫师灵魂移动脚本触发的扣血，不改变移动。"},
            {"rwdseerfd.角色类.恶魔巫师.触发器碰撞伤害","恶魔巫师触发器碰撞检测产生的扣血。"},
            {"rwdseerfd.unity脚本存储文件夹.雷电龙类.单位加血","雷电龙加血函数把参数取负传入受伤入口，正常治疗不受承伤倍率影响。"},
            {"rwdseerfd.unity脚本存储文件夹.雷电龙类.追踪抛射物简易","雷电龙追踪弹丸命中时的扣血，不改变追踪。"},
            {"rwdseerfd.unity脚本存储文件夹.脚本1.单位掉血掉到1","该脚本造成伤害函数中经受伤入口结算的部分；不覆盖直接写生命的分支。"}
        };
        static string DamageMechanism(string key,Type owner)
        {
            if(key=="Freeze")return "缩放新增冰冻积累，不改变冰箭直接伤害，也不清除已有冻结。";
            if(key=="Explosion")return "缩放这个实现产生的爆炸伤害，不改变爆炸半径、击退或附加效果。";
            if(key=="Burning")return "缩放原版持续扣血入口；复用该入口的非燃烧效果也会受到影响。";
            if(typeof(DamageOverTimeEffect).IsAssignableFrom(owner))return "继承原版持续扣血的模组效果；可与燃烧／持续承伤倍率同时相乘。";
            return DamageMechanisms.TryGetValue(owner.FullName,out var text)?text:
                "缩放此脚本在已列调用入口内产生的伤害；它是脚本来源，不是独立的原生伤害类型。";
        }
        void DrawDamageAnnotation(string key)
        {
            if(!DrawFold("resistance/note/"+key,"注释",0,0,0,false,2))return;
            GUILayout.BeginVertical();
            if(DamageAnnotations.TryGetValue(key,out var entries)&&entries.Count>0)
            {
                foreach(var entry in entries.OrderBy(e=>e.Owner.Assembly.GetName().Name).ThenBy(e=>e.Owner.FullName))
                {
                    DrawRemark("来源："+DamageModName(entry.Owner.Assembly));
                    DrawRemark(DamageMechanism(key,entry.Owner));
                    if(entry.Units.Count>0)
                        DrawRemark("预制体关联："+string.Join("、",entry.Units.OrderBy(u=>u.Module).ThenBy(Labels.UnitName)
                            .Select(u=>SourceName(u.Module)+" · "+Labels.UnitName(u)).Distinct()));
                    if(DrawFold("resistance/calls/"+key+"/"+entry.Owner.Assembly.GetName().Name+"/"+entry.Owner.FullName,"调用入口",0,0,0,false,3))
                    {
                        DrawRemark("程序集："+entry.Owner.Assembly.GetName().Name);
                        DrawRemark("组件："+entry.Owner.FullName);
                        foreach(var method in entry.Methods.OrderBy(m=>m,StringComparer.Ordinal))DrawRemark(method);
                    }
                    GUILayout.Space(8);
                }
                if(key!="Freeze")DrawRemark("仅调整送入原版受伤入口的正伤害，与基础类型倍率相乘；治疗、直接写生命和强制死亡不受此项影响。");
            }
            else if(key.StartsWith("source/",StringComparison.Ordinal))
            {
                var parts=key.Split('/');
                DrawRemark("来源程序集："+parts[1]);
                DrawRemark("组件："+parts[2]);
                DrawRemark(ResistanceLabels.ContainsKey(key)?"当前未记录成功接入的调用入口。":"当前来源未加载或未识别；保留已保存的数值，当前没有对应的来源入口生效。");
            }
            GUILayout.EndVertical();
        }
    }
}
