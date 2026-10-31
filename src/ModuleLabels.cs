using System.Collections.Generic;
using System.Linq;

namespace ExpansionaryEditor
{
    public static partial class Labels
    {
        internal static readonly Dictionary<string,string> ModuleNames=new Dictionary<string,string> {
            {"UNIT_WIP_ELEPHANTRIDER","大象骑手模板"},{"UNIT_WIP_CUSTOMHALFLING","自制半身人模板"},
            {"UNIT_WIP_CUSTOMCLAM","自制蛤蜊模板"},{"UNIT_WIP_CUSTOMSNAKE","自制蛇模板"},
            {"UNIT_WIP_CUSTOMSTIFFY","自制骷髅模板"},{"UNIT_WIP_CUSTOMRAPTOR","自制迅猛龙模板"},
            {"UNIT_WIP_CUSTOMHORSE","自制马匹模板"},{"UNIT_WIP_CUSTOMMINOTAUR","自制牛头怪模板"},
            {"UNIT_WIP_CUSTOMMAMMOTH","自制猛犸象模板"},{"UNIT_WIP_CUSTOMICEGIANT","自制冰霜巨人模板"},
            {"FACTION_WIP","内部／制作中"},{"Invisible","隐形附属单位"},{"Bird of Ra","拉神灵鸟"},
            {"Enlightened Ninja","悟道忍者"},{"Manticore Rider","蝎尾狮骑手"},{"Rune Beast","符文巨兽"},
            {"Warlord","战争领主"},{"Odin Golem","奥丁魔像"},{"The Queen","女王"},{"Spectral Shogun","幽魂将军"},
            {"Scallywag","无赖水手"},{"Pot Snake","罐中蛇"},{"Slave","奴仆"},{"BoxCannonRider","箱式大炮骑手"},
            {"MagRodRider","磁力杖骑手"},{"FurnaceRider","熔炉骑手"},{"BehemothRider","巨兽骑手"},
            {"Digger","掘地者"},{"Tower Archer","箭塔弓手"},{"Venom Archer","毒箭手"},{"Deceased Tribesman","亡故部落人"},
            {"Scorpion","蝎子"},{"Crusader","圣战士"},{"Marcher","号角行军兵"},{"Spartan","斯巴达战士"},
            {"RoyalExecutioner","王室刽子手"},{"NeonBoxer","霓虹拳手"},{"Wolf","狼"},{"Pegaman","天马人"},
            {"Camel","骆驼"},{"Heli Pilot","直升机驾驶员"},{"Soul","灵魂"},{"Prospector","淘金者"},
            {"Legionary","军团战士"},{"Roman Archer","罗马弓手"},{"Centurion","百夫长"},{"BountyHunter","赏金猎人"},
            {"Cerberus","三头地狱犬"},{"CustomWobbler","自制兵种模板"},{"F22 Jet","F22 喷气机"}
        };
        public static string UnitName(UnitEntry entry)
        {
            if(!string.IsNullOrEmpty(entry.Name))return Localize(entry.Name);
            var asset=Localize(entry.AssetName??"");
            if(asset.Any(c=>c>='\u4e00'&&c<='\u9fff'))return asset;
            return SourceCatalogName(entry.Module)+"附属单位（"+entry.Blueprint.Entity.GUID.m_ID+"）";
        }
        static string SourceCatalogName(string module)=>EditorPlugin.SourceName(module);
        static readonly Dictionary<string,string> ModuleMoves=new Dictionary<string,string> {
            {"Wings_Leg_SuperP","腿部飞行推进"},{"Wings_Leg_SuperP 1","腿部飞行推进"},{"DARK:xr","掷叉手闪避"},
            {"Move_CM_ExplodeAfterDeath","死亡爆发"},{"PlayWolfSound","狼嚎音效"},{"Move_CMM_Hover","鸡王悬浮"},
            {"Move_Sage_Death","忍宗临终技能"},{"Move_Cthulhu_Fog","克苏鲁迷雾"},
            {"Move_BusinessMan_CheckIfTargeting","锁敌检测"},{"RemoveHPBar","隐藏血条"},
            {"Move_Warhorn_Ghost","号角亡灵效果"},{"Move_Crusader_Launch","圣战士冲锋"},{"DontStopForTurns","转向移动辅助"},
            {"Move_Spirit_ExplodeAfterDeath","亡灵死亡爆发"},{"Spartan_Kick","斯巴达踢击"},
            {"Minotaur_headbutt","牛头撞击"},{"Vampire_Jump","吸血鬼扑击"},{"Kick_Harpooner_L","鱼叉兵左踢"},
            {"Spinner_Walk","旋转步态"},{"FireBreath_Wheelbarrow","独轮车喷火"},{"wheelbarrowdragon","独轮车龙头动作"},
            {"SkeletonGiant_Damage_L","骷髅巨人左拳"},{"SkeletonGiant_Damage_R","骷髅巨人右拳"},
            {"Kick_Blackbeard_L","黑胡子左踢"},{"SpinToWin_Blackbeard","黑胡子旋转挥击"},{"Block_Samuraigiant","巨武士格挡"},
            {"Move_tankrider","坦克骑手动作"},{"Move_Bombcannon_pirate","大炮骑手动作"},{"Move_Vamp","蝙蝠飞行"},
            {"BarrelArmor_ElbowRight","右肘护甲"},{"BarrelArmor_ElbowLeft","左肘护甲"},
            {"BarrelArmor_ArmLeft","左臂护甲"},{"BarrelArmor_ArmRight","右臂护甲"},
            {"Move_SpeedExplosions_Wheelbarrow","独轮车移动效果"},{"Move_SpeedExplosions_Raptorrider","迅猛龙骑手移动效果"},
            {"Move_SpeedExplosions_Lasso","套索兵移动效果"}
        };
    }
}
