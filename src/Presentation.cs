using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed class AbilityInfo
    {
        public string Key,Name,Description;
        public bool Auxiliary;
    }

    public static partial class Labels
    {
        public static readonly Dictionary<string,string> DerivedNames=new Dictionary<string,string> {
            {"InstantDeadUnit","图腾献祭替身"},{"InvisibleVehicleRider","载具隐形驾驶员"},
            {"GoblinSpawned","召唤哥布林"},{"LilSnakewestern","小型西部蛇"},
            {"Primal Wobbler","原初摇摆人"},{"Goblin","哥布林"},{"Ents","树人"},{"Vulcanic Wraith","火山幽魂"},
            {"Snakewestern","西部蛇"},{"ReanimatedSkeleton","复生骷髅"},{"FrontierSoldiers","空降援军"},
            {"BackwardHumanoid","背生人形体"},{"SubanoidHumanoid","附生人形体"},{"GravebatsSkeleton","墓蝠骷髅形态"},
            {"AirhoundPilot","空中猎犬驾驶员"},{"Mongol Archer","蒙古弓骑手"},{"Gravebats","掘墓人墓蝠形态"},
            {"Huntervulture","赏金猎人秃鹫"},
            {"Headless Rider","无头骑士骑手"},{"HorsePhantomon","无头骑士幽灵战马"},
            {"HelpingHand","援助之手"},{"McDeth","暗猎者变身形态"},
            {"PuppetCorpse1","傀儡尸体·剑士"},{"PuppetCorpse2","傀儡尸体·踢击者"},{"DementedJuliet","狂乱朱丽叶"},
            {"M0D","模零"}
        };
        public static readonly Dictionary<string,string> DerivedSources=new Dictionary<string,string> {
            {"InstantDeadUnit","图腾的献祭辅助单位"},{"InvisibleVehicleRider","怨魂巨龙、潜艇和喷气机共用的隐形驾驶单位"},
            {"GoblinSpawned","哥布林群召唤出的单个哥布林"},{"LilSnakewestern","蛇眼 → 西部蛇 → 分裂出的小蛇"},
            {"Snakewestern","蛇眼召唤的西部蛇"},{"ReanimatedSkeleton","墓蝠骷髅形态复生的骷髅"},
            {"FrontierSoldiers","军士呼叫的空降援军"},{"BackwardHumanoid","悲惨使者的附属身体"},{"SubanoidHumanoid","悲惨使者的附属身体"},
            {"GravebatsSkeleton","墓蝠转化的骷髅形态"},{"AirhoundPilot","空中猎犬的驾驶员；载具被毁后跳伞"},
            {"Mongol Archer","蒙古人的骑手"},{"Gravebats","掘墓人变成的墓蝠"},
            {"Headless Rider","无头骑士的骑手"},{"HorsePhantomon","无头骑士的幽灵坐骑／召唤战马"},
            {"McDeth","暗黑猎人的变身形态"},{"PuppetCorpse1","傀儡师控制的剑士尸体"},{"PuppetCorpse2","傀儡师控制的踢击尸体"},
            {"Huntervulture","赏金猎人派出的秃鹫"},
            {"DementedJuliet","罗密欧与朱丽叶的狂乱形态"}
            ,{"HelpingHand","无名农夫召唤的援助之手"}
            ,{"Primal Wobbler","内部身体模板，使用远古征服者的身体；未发现其他兵种的直接召唤引用"}
            ,{"Goblin","内部哥布林身体模板；哥布林群实际生成的是“召唤哥布林”"}
            ,{"Ents","内部树人模板，使用翠绿狂热者的身体；未发现直接召唤引用"}
            ,{"Vulcanic Wraith","内部火山幽魂模板，使用火神皇帝的身体；未发现直接召唤引用"}
        };
        public static string Origin(UnitEntry entry)=>entry.Module!="Expansionary"?"来源："+entry.Source+" · "+Faction(entry.Faction):
            DerivedSources.TryGetValue(entry.Name,out var source)?"来源："+source:
            "来源：扩张模组 · "+Faction(entry.Faction)+(entry.Faction=="FACTION_SUBUNITS"?"；内部衍生单位":"");

        // Player-facing names describe the move, not the implementation script.
        static readonly Dictionary<string,string> Moves=new Dictionary<string,string> {
            {"AbsorptionPassive","伤害吸收"},{"Ash_Fog","灰烬迷雾"},{"BarrelHide","藏身木桶"},
            {"Block_Energetic","能量格挡"},{"BodySuspension","悬吊身体"},{"Bubble_Ionic","离子屏障"},
            {"BunnyHop","兔跃践踏"},{"Burrow","遁地突袭"},{"CallSoldier","呼叫空降援军"},
            {"Chaotic_Dodge","混沌闪避"},{"CoffinFollows","棺材随行"},{"ComboSlash","连续斩击"},
            {"ConquerorWing","征服者之翼"},{"DiscordantLaugh","不谐狂笑"},{"Dispose_Dynamike","抛掷炸药"},
            {"ElderEntityAuraFog","远古威压"},{"Elder_Body","远古身躯"},{"EnemyBetrayal","敌方背叛"},
            {"Excalibur_Killaura","圣剑伤害光环"},{"Exotic_Flurry","异域连击"},{"ExpertDuelist","决斗大师"},
            {"Extra_Charge","额外冲锋"},{"FearHordes","恐惧马群"},{"Fiery_Fog","烈焰迷雾"},
            {"FinalMelody","终末旋律"},{"FoggyChase","雾中追猎"},{"ForstThunderEmerges","寒霜雷击"},
            {"Forstenad_Wings","寒霜双翼"},{"FreezingFog","冰冻迷雾"},{"FurnaceExpress","熔炉冲锋"},
            {"GiantSwordCast","召唤巨剑"},{"Gladiator_Prowess","角斗士威能"},{"GlitchedDeath","故障死亡"},
            {"GraveSwordDrag","墓剑牵引"},{"GraveWings","墓蝠飞翼"},{"GraveWingsDigger","掘墓人飞翼"},
            {"GreatMotivation","图腾激励"},{"GroundPunch","重拳砸地"},{"Ground_Shield","大地护盾"},
            {"Gundown","闪现枪舞"},{"HandCombatParry","徒手招架"},{"HangedBody","悬吊尸身"},
            {"Haste","急速突进"},{"HeadPumpkin_Follow","南瓜头随行"},{"HeadlessHorseCharge","无头战马冲锋"},
            {"HealthCocoon","生命之茧"},{"Hellbody","地狱之躯"},{"HomeRun","全垒打击"},
            {"IceAge","冰河时代"},{"Indicate_TotemInRange","图腾范围提示"},{"IntimidationEarl","伯爵威慑"},
            {"IonGround","离子地面"},{"KeepDistance","保持距离"},{"KeepStanding","保持站立"},
            {"LameDeFlamme","烈焰之刃"},{"MagmaticTrample","岩浆践踏"},{"Nemean_Slam","涅墨亚重击"},
            {"Nightstalk","暗夜突袭"},{"OnVehicleDeathParachute","载具摧毁后跳伞"},{"PainedScream","痛苦尖啸"},
            {"Parachute","降落伞"},{"Parry_Punk","赛博招架"},{"PhantomHover","幽灵悬浮"},
            {"PrimalDemise","原初毁灭"},{"ProjectionWall","投影屏障"},{"ProtectiveHolyShield","守护圣盾"},
            {"RafflesiaGas","大王花毒雾"},{"RecycleGuns","循环换枪"},{"RotatingHolyShield","旋转圣盾"},
            {"SkyStrike","天降雷击"},{"SniperLayDown","卧倒狙击"},{"SpawnGoblins","召唤哥布林"},
            {"SpawnPhantomHorseHordes","召唤幽灵马群"},{"Spawn_TrioAxes","三重飞斧"},{"StatueMimicry","石像伪装"},
            {"StereoDoppelganger","立体分身"},{"StormBreath","风暴吐息"},{"ThornsEnsnare","荆棘缠绕"},
            {"TimeDance","时间之舞"},{"TransformToDigger","变回掘墓人"},{"TransformToGraveBat","化身墓蝠"},
            {"TransformToSkeleton","化身骷髅"},{"TwilightSunlight","暮光日照"},{"UmbrellicBalleria","旋伞舞步"},
            {"Upgrade","战斗强化"},{"Water_Affinity","水域适应"},{"WildWestFog","西部烟雾"},
            {"AbsoluteEclipseTransformation","绝对日蚀变身"},{"AbsoluteEclipseWing1","日蚀之翼·左"},{"AbsoluteEclipseWing2","日蚀之翼·右"},
            {"AbyssFog","深渊迷雾"},{"Avenged_Tenfold","十倍复仇"},{"BlightenedBride","狂乱新娘"},{"BloatedBlood","膨胀之血"},
            {"Bloodlord_Tribute","血主献礼"},{"BloodyMary","血腥玛丽"},{"ColorlessGale","无色狂风"},{"ConflictingSpace","空间冲突"},
            {"DrunkAndDrunk","酩酊醉态"},{"HelpingHands","召唤援助之手"},{"KickDrunk","醉拳踢击"},{"Kiss","魅惑之吻"},
            {"Paintstrike","挥彩打击"},{"PastelHair","粉彩发饰"},{"SlaesCyflym","迅捷连斩"},{"Spawn_UnnamedCage","召唤无名牢笼"},
            {"SpinDodgeDrunk","醉步旋身闪避"},{"TempEnemy","临时敌对标记"},{"TrueDeath_Explode","真死爆发"},
            {"VoidOfSoul","灵魂虚空"},{"Vulture_Wings","秃鹫飞翼"},{"WhirlpoolSummon","召唤漩涡"},
            {"ForgottenSoulLeakLateHip","灵魂逸散·腰部"},{"ForgottenSoulLeakLateLeft","灵魂逸散·左侧延迟"},
            {"ForgottenSoulLeakLateRight","灵魂逸散·右侧延迟"},{"ForgottenSoulLeakLeft","灵魂逸散·左侧"},
            {"ForgottenSoulLeakRight","灵魂逸散·右侧"},{"ForgottenSoulLeakLeftFoot","灵魂逸散·左脚"},
            {"ForgottenSoulLeakRightFoot","灵魂逸散·右脚"},{"ForgottenSoulLeakLeftLateLeg","灵魂逸散·左腿"},
            {"ForgottenSoulLeakRightLateLeg","灵魂逸散·右腿"},{"ForgottenSoulLeakTorso","灵魂逸散·躯干"},
            {"Shogun_Dodge","将军闪避"},{"Dodge","侧身闪避"},{"Fencer_Dodge","击剑闪避"},{"Boxer_Dodge","拳击闪避"},
            {"ProjectileDodge_Cool2","投射物闪避"},{"ProjectileDodge_Superboxer","强化投射物闪避"},{"Quickdraw_Dodge","快枪闪避"},
            {"Jump_BerserkerShort","短距跳跃"},{"Halfling_Jump","半身人跳跃"},{"Ballooner_Jump","气球跳跃"},
            {"SwordArtParry","剑术招架"},{"SwordArtParry_Piratequeen","海盗女王招架"},{"SpinSlashKhopesh","旋身斩击"},
            {"SpinToWin_Mace","旋转挥砸"},{"MinotaurCharge","牛头冲锋"},{"Kick_Captain_R","右脚踢击"},
            {"BallAndChain_Jump","链球跳击"},{"JumpKick_Taekwondo_L","左侧跳踢"},{"Taekwondo_Damage_L","左腿踢击"},
            {"Taekwondo_Damage_R","右腿踢击"},{"Headbutter_Damage_Head","头槌"},{"BlockSlow","缓速格挡"},
            {"Head_up","抬头动作"},{"Halberd_Walk","长戟步态"},{"Sarissa_Walk","长矛步态"},{"Chukonu_Walk","弩手步态"},
            {"Banjo_Walk","班卓步态"},{"Hover","悬浮"},{"Small_Hover","轻度悬浮"},
            {"BarrelArmor_Hip","腰部木桶护甲"},{"BarrelArmor_Torso","躯干木桶护甲"},
            {"Move_SpeedExplosions_Cavalry","骑兵移动效果"},{"Move_SpeedExplosions_Jouster","骑枪移动效果"},
            {"Move_SpeedExplosions_Chariot","战车移动效果"},{"Move_SpeedExplosions_Chariot_Horse","战车马匹移动效果"}
        };
        static string MoveToken(string text)=>text.Replace("(Clone)","").Replace("Expansionary:Move_","").Trim();
        public static AbilityInfo Ability(string key,string objectName)
        {
            string token=MoveToken(objectName);
            string name=Moves.TryGetValue(token,out var title)?title:"特殊技能 "+(int.TryParse(key.Substring(1),out var i)?(i+1).ToString():"");
            bool auxiliary=token.EndsWith("_Walk")||token.StartsWith("Move_SpeedExplosions_")||new[]{"KeepStanding","Head_up","Hover","Small_Hover","Indicate_TotemInRange"}.Contains(token);
            return new AbilityInfo {Key=key,Name=name,Auxiliary=auxiliary,Description=AbilityDescription(token,auxiliary)};
        }
        public static AbilityInfo Ability(string key,GameObject prefab)
        {
            string token=prefab.name.Replace("(Clone)","").Trim();
            if(ModuleMoves.TryGetValue(token,out var moduleMove))return new AbilityInfo {Key=key,Name=moduleMove,
                Auxiliary=token.StartsWith("Move_SpeedExplosions_")||new[]{"PlayWolfSound","Move_CMM_Hover","Move_BusinessMan_CheckIfTargeting","RemoveHPBar","DontStopForTurns","Spinner_Walk","wheelbarrowdragon","Move_tankrider","Move_Bombcannon_pirate"}.Contains(token),
                Description="满足触发条件后自动施放；不同环节的冷却分别设置。"};
            var result=Ability(key,prefab.name);
            if(!result.Name.StartsWith("特殊技能"))return result;
            var info=prefab.GetComponent<Landfall.TABS.UnitEditor.SpecialAbility>();
            string name=info?Localize(info.Entity.Name):Localize(prefab.name.Replace("(Clone)",""));
            if(!string.IsNullOrEmpty(name)&&name.Any(c=>c>='\u4e00'&&c<='\u9fff'))result.Name=name;
            else
            {
                var names=prefab.GetComponentsInChildren<Component>(true).Where(c=>c).Select(c=>c.GetType().Name).ToArray();
                string action=names.Any(n=>n.IndexOf("Dodge",StringComparison.OrdinalIgnoreCase)>=0)?"闪避动作":
                    names.Any(n=>n.IndexOf("Parry",StringComparison.OrdinalIgnoreCase)>=0)?"招架动作":
                    names.Any(n=>n.IndexOf("Spawner",StringComparison.OrdinalIgnoreCase)>=0)?"召唤技能":
                    names.Contains("SpawnObject")?"释放技能":names.Contains("Wings")?"飞行技能":"条件触发技能";
                result.Name=action+" "+(int.TryParse(key.Substring(1),out var i)?(i+1).ToString():"");
            }
            return result;
        }
        static string AbilityDescription(string token,bool auxiliary)
        {
            switch(token) {
                case "TimeDance":return "靠近目标时施放时间之舞；冷却越小，重复施放越频繁。";
                case "CallSoldier":return "军士定期呼叫空降援军；这里的冷却控制重复呼叫的间隔。";
                case "SpawnGoblins":return "生成哥布林群；召唤物本身的生命和伤害在左侧“召唤哥布林”中修改。";
                case "StatueMimicry":return "石像伪装及接敌触发；触发距离和旋身斩击分开设置。";
                case "Nightstalk":return "学者的暗夜突袭；独立冷却控制再次发动的间隔。";
                case "BarrelHide":return "隐士的藏身木桶动作；独立冷却控制再次使用的间隔。";
                case "Gladiator_Prowess":return "赫拉克勒斯的角斗士威能；冷却、作用力与爆炸伤害分开设置。";
                case "Nemean_Slam":return "赫拉克勒斯的重击；冷却控制再次重击的间隔。";
                case "Gundown":return "连续闪现攻击；步间隔控制连招内部节奏，不是整套连招冷却。";
                case "RecycleGuns":return "按固定间隔切换武器。";
                case "TransformToGraveBat":case "TransformToDigger":case "TransformToSkeleton":return "触发变身；变身后的数值在左侧对应衍生兵种中修改。";
                default:return auxiliary?"辅助步态、姿态或视觉效果。":"满足触发条件后自动施放；不同环节的冷却分别设置。";
            }
        }

        public static bool ReadableField(string name)=>Fields.ContainsKey(name)||ExtraFields.ContainsKey(name)||name.Any(c=>c>='\u4e00'&&c<='\u9fff');
        public static string ReadableLabel(string name)=>name.Any(c=>c>='\u4e00'&&c<='\u9fff')?name:ExtraFields.TryGetValue(name,out var text)?text:Field(name);
        static readonly Dictionary<string,string> ExtraFields=new Dictionary<string,string> {
            {"range","触发距离"},{"activationRange","发动距离"},{"attackRange","攻击距离"},
            {"damageAmount","伤害"},{"amountToSpawn","召唤数量"},{"numberToSpawn","生成数量"},{"timeBetweenSpawns","连续生成间隔（秒）"},
            {"timeMax","效果持续时间（秒）"},{"flightForce","飞行作用力"},{"healThreshold","治疗触发阈值"},
            {"spinSpeed","旋转速度"},{"travelSpeed","移动速度"},{"sliceMax","连续斩击次数"},
            {"timeDelay","动作延迟（秒）"},{"enabledTime_Max","启用持续时间（秒）"},
            {"useSpawnDelayForEveryShot","每次射击都使用前摇"},{"useRandomDelay","随机连发延迟"}
        };
        public static string NamedConstant(Type type,string method,int index)
        {
            if(method!="Update")return null;
            switch(type.Name) {
                case "CallForSoldiers":return index==0?"援军呼叫间隔（秒）":null;
                case "GunsCycler":return index==0?"换枪间隔（秒）":null;
                case "RandomForstThunderMovement":return index==0?"雷击冷却（秒）":null;
                case "CerbericSlamMovement":return index==0?"重击冷却（秒）":null;
                case "SlaesCyflymMovement":return index==0?"连斩冷却（秒）":null;
                case "GundownMovement":return index==0?"连招内闪现步间隔（秒）":null;
                case "SubmarineTechnics":return index==2?"潜艇特殊动作间隔（秒）":null;
                default:return null;
            }
        }
        public static bool PrivateSetting(Type type,string field)=>
            field=="cooldown"&&new[]{"BarrelHideMechanic","NightstalkMovement","UmbrellicBalleriaMovement","GladiatorProwessMovement"}.Contains(type.Name)
            ||type.Name=="DistanceBasedAmmo"&&field=="CDRanged";

        static readonly Dictionary<string,string> Intrinsic=new Dictionary<string,string> {
            {"DeadLineAbility","子弹伤害增长"},{"AddDeflectOnSwing","挥击反弹"},
            {"DamageBasedUnitsWeapon","战场人数·武器增幅"},{"DamageBasedUnitsProjectile","战场人数·弹丸增幅"},
            {"RandomPotions","随机药水"},{"OnDeathExplosion","死亡爆炸"},{"DistanceBasedAmmo","远近攻击切换"},
            {"DamageSwingCollision","特殊挥击伤害"},{"BubbleBurst","气泡爆发"},{"HangEntanglement","绞索缠绕"},
            {"FrenzySwing","狂乱挥击"},{"HunterLongswordFunctionality","猎人长剑"},{"MarinaAnchorAbilities","船锚特技"},
            {"OnDeathSpawn","死亡召唤"},{"DistanceShootAmount","距离决定弹数"},{"MultiUnitSpawn","多单位生成"},
            {"PuppetFunctionality","傀儡控制"},{"SwingSummon","挥击召唤"},{"HomingProjCustomized","追踪弹丸"},
            {"HexEffect","诅咒效果"},{"GrappleStick","抓钩牵引"},{"BorealVortexSwing","寒地涡旋挥击"},
            {"OnDeathFreeze","死亡冻结"},{"TimeSlice","时间斩击"},{"PrayethMechanics","祈祷者特性"},
            {"RangeDamageSelf","射击自伤"},{"WyrmMovement","怨魂巨龙移动"},{"HungerDebuff","饥饿减益"},
            {"TrappedAndTormentedBody","受困躯体"},{"UnnamedWhisper","无名低语"},{"LingeringFlame","残留火焰"},
            {"AirhoundTechnics","空中猎犬特技"},{"ClusterMechanique","集束攻击"},{"SubmarineTechnics","潜艇特技"},
            {"F22Technics","喷气机特技"},{"TeleportProjectile","传送弹丸"},{"ProjToGold","点石成金"},
            {"RukyrionDivider","征服者分裂"},{"LightProjectionScan","光学扫描"},{"ProjectorFunction","投影特性"},
            {"RangedDuel","远程决斗"},{"GroundSkinner","地表效果"},{"VulcanConcussiveBlast","火神震荡爆破"}
        };
        public static AbilityInfo IntrinsicAbility(Type type,string path,string group)
        {
            if(!Intrinsic.TryGetValue(type.Name,out var title))return null;
            string source=group.StartsWith("右")?"右手":group.StartsWith("左")?"左手":"本体";
            return new AbilityInfo {Key=path,Name=source+" · "+title,Description="被动／武器特性；普通命中伤害在对应左右手页修改。"};
        }
    }
}
