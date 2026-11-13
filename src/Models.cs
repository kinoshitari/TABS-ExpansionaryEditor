using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed class UnitEntry
    {
        public string Key, Name, Faction, Module, Source, AssetName;
        public UnitBlueprint Blueprint;
        public uint OriginalPrice;
        public Dictionary<string, object> OriginalStats = new Dictionary<string, object>();
        public GameObject Base, Right, Left;
        public GameObject[] Props, Children;
        public List<ValueSlot> Slots;
        public List<ConstantSlot> Constants;
        public List<AbilityInfo> Abilities;
        public readonly List<UnitEntry> Parents = new List<UnitEntry>();
        public bool IsDerived => Faction=="FACTION_SUBUNITS"||Faction=="附加／召唤兵种"||Name?.StartsWith("UNIT_SUBUNITS_")==true;
        public string ParentText;
    }

    public sealed class StatDef
    {
        public string Field, Label, Note;
        public double Min;
        public StatDef(string field, string label, double min = 0, string note = "")
        { Field = field; Label = label; Min = min; Note = note; }
    }

    public sealed class ValueSlot
    {
        public string Key, Group, Label, Path, ComponentName, FieldName, Original;
        public string Note;
        public string EffectKey, EffectName;
        public double? Minimum;
        public bool Readable;
        public Type Type, ComponentType;
        public Action<string> Assign;
        public bool IsBool => Type == typeof(bool);
    }

    public sealed class ConstantSlot
    {
        public string Key, Group, Label, Original;
        public string AbilityKey, Note;
        public bool Readable;
        public MethodBase Method;
        public bool Double;
    }

    public sealed class OwnerTag : MonoBehaviour
    {
        public string UnitKey;
        public string RootPath;
        public Component[] DisabledWeaponEffects = new Component[0];
    }

    public static partial class Labels
    {
        public static readonly StatDef[] Stats = {
            new StatDef("health", "基础生命值", 0.001, "最终生命值还会受体型随机值和生命倍率影响"),
            new StatDef("_healthMultiplier", "生命值倍率", 0.001),
            new StatDef("_damageMultiplaier", "伤害倍率", 0.001, "0 在游戏内部表示默认倍率；禁用伤害请修改武器伤害为 0"),
            new StatDef("_attackSpeedMultiplier", "攻击速度倍率", 0.001, "越大越快；不是攻击间隔"),
            new StatDef("movementSpeedMuiltiplier", "移动速度倍率"),
            new StatDef("_rangeMultiplier", "攻击距离倍率", 0.001),
            new StatDef("sizeMultiplier", "体型倍率", 0.01),
            new StatDef("massMultiplier", "质量倍率", 0.001),
            new StatDef("balanceMultiplier", "平衡能力倍率"),
            new StatDef("balanceForceMultiplier", "平衡支撑力倍率"),
            new StatDef("animationMultiplier", "动作速度倍率", 0.001),
            new StatDef("stepMultiplier", "步幅倍率", 0.001),
            new StatDef("dragMultiplier", "物理阻力倍率", 0.001),
            new StatDef("turnSpeed", "转身速度"),
            new StatDef("targetingPriorityMultiplier", "被优先攻击的权重"),
            new StatDef("minSizeRandom", "随机体型下限", 0.01),
            new StatDef("maxSizeRandom", "随机体型上限", 0.01),
            new StatDef("scaleWeapons", "武器随体型缩放"),
            new StatDef("weaponSeparation", "双手武器间距"),
            new StatDef("removeCloseRangeMiss", "取消近距离故意打偏")
        };
        static readonly Dictionary<string, string> Fields = new Dictionary<string, string> {
            {"damage","伤害"},{"percentDamage","百分比伤害"},{"radius","作用半径"},{"force","力度／击退力"},
            {"internalCooldown","攻击间隔（秒）"},{"maxRange","最大攻击距离"},{"maxAngle","最大攻击角度"},
            {"startOnCooldown","开场进入冷却"},{"randomCooldown","随机攻击间隔"},
            {"reloadTime","装填时间（秒）"},{"magSize","弹匣容量"},{"numberOfObjects","每次发射数量"},
            {"spawnDelay","发射前摇（秒）"},{"delayPerSpawn","连发间隔（秒）"},{"spread","散布"},
            {"projectileSpeedSpread","弹速随机幅度"},{"clampRange","射程上限"},
            {"shootRecoil","射击后坐力"},{"torsoRecoil","躯干后坐力"},{"extraCDInMelee","近战额外冷却"},
            {"extraSpreadInMelee","近战额外散布"},{"numberOfSpawns","生成数量"},
            {"ignoreTeamMates","不伤害友军"},{"alwaysHitTeamMates","始终命中友军"},
            {"onlyTeamMates","只影响友军"},{"ignoreRoot","忽略来源单位"},
            {"allowedHits","可命中次数"},{"allowRepeat","可重复命中同一目标"},{"destroyOnHit","命中后消失"},
            {"blockPoweredNeeded","格挡所需强度"},{"stopOnMapHit","命中地形后停止"},
            {"stopAttacksFor","打断攻击时间（秒）"},{"impactMultiplier","碰撞伤害倍率"},
            {"onImpactForce","命中击退力"},{"minVelocity","触发伤害的最低速度"},
            {"staticDamageValue","使用固定伤害"},{"teamDamage","友军伤害"},{"cooldown","冷却（秒）"},
            {"minRange","触发距离下限"},{"animationSpeed","技能动作速度"},
            {"forceMultiplier","技能作用力倍率"},{"usedAsMovement","用作移动技能"},
            {"decreaseAmm","生命流失量"},{"delayPerhurt","持续伤害间隔（秒）"},
            {"speed","速度"},{"moveSpeed","移动速度"},{"duration","持续时间（秒）"},
            {"delay","延迟（秒）"},{"amount","数量／强度"},{"heal","治疗量"},{"healAmount","治疗量"},
            {"damageMultiplier","伤害倍率"},{"healthMultiplier","生命倍率"},
            {"curveForce","挥击力度"},{"selfDamageMultiplier","自伤倍率"},
            {"distance","距离"},{"time","时间（秒）"},{"chance","概率"},{"enabled","启用此组件／技能"},
            {"m_cooldown","技能冷却（秒）"},{"m_randomCooldown","随机冷却幅度"}
            ,{"defaultDrag","武器线性阻力"},{"defaultAngularDrag","武器旋转阻力"},{"fallTime","下坠时间"}
            ,{"minRandom","随机延迟下限"},{"maxRandom","随机延迟上限"},{"CDRanged","远程模式冷却（秒）"},{"CDMelee","近战模式冷却（秒）"}
            ,{"ammoClose","近距离发射数量"},{"ammoFar","远距离发射数量"},{"rangeDetection","近远程切换距离"}
        };
        public static string Field(string name) => Fields.TryGetValue(name, out var label) ? label : Translate(name);
        static Func<string,string> localize;
        static bool translationInitialized;
        public static string Localize(string text)
        {
            if(string.IsNullOrEmpty(text))return text??"";
            if(DerivedNames.TryGetValue(text??"",out var translated))return translated;
            if(ModuleNames.TryGetValue(text,out translated))return translated;
            var gameLanguage=HarmonyLib.AccessTools.Field(typeof(Localizer),"m_currentLanguage").GetValue(null) as Dictionary<string,string>;
            if(gameLanguage!=null&&gameLanguage.TryGetValue(text,out var phrase)&&HasChinese(phrase))return phrase;
            if(UnitTranslations.TryGetValue(text,out translated))return translated;
            if(!translationInitialized)
            {
                translationInitialized=true;
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("CN_AllInOne.AllInOnePatchPlugin")).FirstOrDefault(t=>t!=null);
                if(type!=null)
                {
                    var method=HarmonyLib.AccessTools.Method(type,"TranslateSilent",new[]{typeof(string)});
                    if(method!=null)localize=(Func<string,string>)Delegate.CreateDelegate(typeof(Func<string,string>),method);
                }
            }
            return localize==null?text:(localize(text)??text);
        }
        static readonly KeyValuePair<string,string>[] Words = new Dictionary<string,string> {
            {"Multiplier","倍率"},{"Damage","伤害"},{"Health","生命"},{"Cooldown","冷却"},
            {"Duration","持续时间"},{"Chance","概率"},{"Spawn","生成"},{"Count","数量"},
            {"Amount","数值"},{"Speed","速度"},{"Force","力度"},{"Range","距离"},
            {"Radius","半径"},{"Delay","延迟"},{"Time","时间"},{"Limit","上限"},
            {"Max","最大"},{"Min","最小"},{"Heal","治疗"},{"Scale","缩放"},
            {"Distance","距离"},{"Size","大小"},{"Attack","攻击"},{"Knockback","击退"},
            {"Projectile","弹丸"},{"Explosion","爆炸"},{"Dodge","闪避"},{"Move","移动"}
        }.OrderByDescending(x=>x.Key.Length).ToArray();
        static string Translate(string name)
        {
            string result = name;
            foreach(var pair in Words)
                result = System.Text.RegularExpressions.Regex.Replace(result, pair.Key, pair.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return result == name ? "扩展参数 · " + name : result;
        }
        public static string Component(Type t)
        {
            switch(t.Name) {
                case "RangeWeapon": return "远程武器"; case "MeleeWeapon": return "近战武器";
                case "CollisionWeapon": return "近战命中"; case "ProjectileHit": return "弹丸命中";
                case "Explosion": return "爆炸"; case "DodgeMove": return "闪避／动作技能";
                case "CombatMove": return "技能触发"; default: return Translate(t.Name);
            }
        }
        public static string Faction(string name)
        {
            switch(name) {
                case "Tropical":return "热带";case "Great Forest":return "大森林";case "Primeval Kingdom":return "远古王国";
                case "Far North":return "极北";case "Forgotten Communion":return "遗忘教团";case "Dark Age":return "黑暗时代";
                case "Retropolis":return "复古都市";case "Chaotic Line":return "混沌战线";case "Experiment":return "实验";
                case "Great Dynasty":return "大王朝";case "Underrealm":return "冥界";case "Aoranos":return "奥拉诺斯";
                case "FACTION_SUBUNITS":return "衍生／召唤兵种";
                case "Clash Siege":return "攻城战线";case "Dystopian":return "反乌托邦";case "Modern Warfare":return "现代战争";
                case "FACTION_ANCIENT":return "古代";case "FACTION_ASIA":return "王朝";
                case "FACTION_FANTASYEVIL":return "邪恶幻想";case "FACTION_FANTASYGOOD":return "善良幻想";
                case "FACTION_HALLOWEEN":return "万圣节";case "FACTION_RENAISSANCE":return "文艺复兴";
                case "FACTION_TRIBAL":return "部落";case "FACTION_VIKING":return "维京";
                case "FACTION_WESTERN":return "西部";
                default:return Localize(name);
            }
        }
    }
}
