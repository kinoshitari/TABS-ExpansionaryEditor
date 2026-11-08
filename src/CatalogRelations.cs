using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Landfall.TABS;
using UnityEngine;

namespace ExpansionaryEditor
{
    // Read only original prefab data, before any user rule clones are applied.
    // Names below bridge scripted transformations whose references live in code.
    static class CatalogRelations
    {
        static readonly Dictionary<string,string> ScriptParents=new Dictionary<string,string> {
            {"Expansionary/InvisibleVehicleRider","Wraithwyrm|Submarine|F22 Jet"},
            {"Expansionary/InstantDeadUnit","Totem"},{"Expansionary/GoblinSpawned","Goblins"},
            {"Expansionary/LilSnakewestern","Snakeeye"},{"Expansionary/Snakewestern","Snakeeye"},
            {"Expansionary/ReanimatedSkeleton","Gravedigger"},{"Expansionary/FrontierSoldiers","Sergeant"},
            {"Expansionary/BackwardHumanoid","Wretched Emissary"},{"Expansionary/SubanoidHumanoid","Wretched Emissary"},
            {"Expansionary/GravebatsSkeleton","Gravedigger"},{"Expansionary/Gravebats","Gravedigger"},
            {"Expansionary/AirhoundPilot","Airhound"},{"Expansionary/Huntervulture","Bounty Hunter"},
            {"Expansionary/Mongol Archer","Mongolian"},{"Expansionary/Headless Rider","Headless Horseman"},
            {"Expansionary/HorsePhantomon","Headless Horseman"},{"Expansionary/HelpingHand","Unnamed Peasant"},
            {"Expansionary/McDeth","Dark Huntsman"},{"Expansionary/PuppetCorpse1","Puppeteer"},
            {"Expansionary/PuppetCorpse2","Puppeteer"},{"Expansionary/DementedJuliet","Romeo & Juliet"},
            {"ChineseMyth/齐天大圣-分身","齐天大圣"},{"ChouChou/尸鬼灵魂","尸鬼"},
            {"ChouChou/吸血鬼伯爵-蝙蝠","吸血鬼伯爵"},{"ChouChou/深红高塔","深红之王"},
            {"ChouChou/深红传送门","深红之王"},{"ChouChou/深红之王-观察者","深红之王"},
            {"ChouChou/蠕虫蓝图32","WORM"},{"ChouChou/蠕虫人32","WORM"},{"ChouChou/蠕虫巨人","WORM"},
            {"HiddenUnits/BoxCannonRider","HIDDEN_BOXCANNON"},{"HiddenUnits/MagRodRider","HIDDEN_MAGNETICROD"},
            {"HiddenUnits/BehemothRider","HIDDEN_IRONBEHEMOTH"},{"HiddenUnits/FurnaceRider","HIDDEN_SMOKESTACK"},
            {"HiddenUnits/WarhornGhost","HIDDEN_WARHORN"},{"HiddenUnits/Tower Archer","HIDDEN_TOWER"},
            {"HiddenUnits/Deceased Tribesman","HIDDEN_WITCHDOCTOR"},{"HiddenUnits/Scorpion","HIDDEN_SELKET"},
            {"HiddenUnits/Ra Bird","HIDDEN_WARRIOROFRA"},{"HiddenUnits/Mantiman","HIDDEN_MANTICORE"},
            {"HiddenUnits/Pegaman","HIDDEN_PEGASUS"},{"HiddenUnits/Heli Pilot","HIDDEN_HELICOPTER"},
            {"HiddenUnits/Prospector","HIDDEN_PROSPECTOR"},{"HiddenUnits/Warlord","HIDDEN_WARLORD"},
            {"HiddenUnits/Beastmaster","HIDDEN_BEASTMASTER"},{"HiddenUnits/Mummy","HIDDEN_MUMMY"},
            {"HiddenUnits/Ice Golem","HIDDEN_ODIN"},{"HiddenUnits/Nile Queen","HIDDEN_NILEQUEEN"},
            {"HiddenUnits/TheQueen","HIDDEN_THEQUEEN"},{"HiddenUnits/Hadez","HIDDEN_HADEZ"},
            {"HiddenUnits/Cerberus","HIDDEN_HADEZ"},{"HiddenUnits/SamuraiGhost","HIDDEN_THESAGE"},
            {"Holiday/JingleBellElf","Jingle Bells"},{"Holiday/Garland Thrower","Tree Basher"},
            {"Holiday/SantaElf","Gift Bringer"},{"Holiday/MrClaus","Gift Bringer"},
            {"Holiday/Rudolph","Gift Bringer"},{"Holiday/Reindeer","Gift Bringer"},
            {"Holiday/ToyRobotElf","Jolly-Bot"},{"Holiday/SnowElf","Snow Cannon"},
            {"Holiday/SnowmanHip","Snowman"},{"Holiday/SnowmanHead","Snowman"},{"Holiday/SnowmanTorso","Snowman"},
            {"Vanilla/DaVinci","UNIT_RENAISSANCE_DAVINCITANK"},
            {"Vanilla/CatapultRider","UNIT_MEDIEVAL_CATAPULT"},{"Vanilla/SafeRider","UNIT_SECRET_SAFE_THROWER"},
            {"Vanilla/SafeRiderBack","UNIT_SECRET_SAFE_THROWER"},{"Vanilla/SpookycatapultRider","UNIT_HALLOWEEN_SPOOKYCATAPULT"},
            {"Vanilla/TankRiderMid","UNIT_LEGACY_LEG_TANK"},{"Vanilla/TankRider","UNIT_LEGACY_LEG_TANK"},
            {"Vanilla/TankRiderBack","UNIT_LEGACY_LEG_TANK"},{"Vanilla/HwachaRider","UNIT_ASIA_HWACHA"},
            {"Vanilla/GatlingGunRider","UNIT_SECRET_GATLINGGUN"},{"Vanilla/Clam","UNIT_SECRET_THROWER_CLAM"},
            {"Vanilla/CannonRider","UNIT_PIRATE_CANNON"},{"Vanilla/BallistaRider","UNIT_ANCIENT_BALLISTA"},
            {"Vanilla/Snake","UNIT_ANCIENT_ARCHER_SNAKE"},{"Vanilla/DragonCarrier","UNIT_ASIA_DRAGON"},
            {"Vanilla/DragonCarrierFront","UNIT_ASIA_DRAGON"},{"Vanilla/BombCannonRiderFront","UNIT_SECRET_BOMBCANNON"},
            {"Vanilla/BombCannonRider","UNIT_SECRET_BOMBCANNON"},{"Vanilla/Thrower_WheelbarrowApple","UNIT_FARMER_WHEELBARROW"},
            {"Vanilla/Pitchfork_WheelBarrow","UNIT_FARMER_WHEELBARROW"},{"Vanilla/VikingBoatRider","UNIT_VIKING_VIKINGBOAT"},
            {"Vanilla/VikingBoatCarrier","UNIT_VIKING_VIKINGBOAT"},{"Vanilla/LassoRider","UNIT_WESTERN_LASSO"},
            {"Vanilla/Raptorrider","UNIT_SECRET_RAPTORWITHRIDER"},{"Vanilla/ChariotRider","UNIT_LEGACY_CHARIOT"},
            {"Vanilla/Horse_Chariot","UNIT_LEGACY_CHARIOT"},{"Vanilla/Witch_Spawned","UNIT_SECRET_WITCH_BROOM"},
            {"Vanilla/Witch_Broom_Spawned","UNIT_SECRET_WITCH_BROOM"},{"Vanilla/Witch","UNIT_SECRET_WITCH_BROOM"},
            {"Vanilla/VampBat","UNIT_HALLOWEEN_VAMPIREBITER"},{"Vanilla/Crow","UNIT_FARMER_SCARECROW"},
            {"Vanilla/Jouster","UNIT_RENAISSANCE_JOUSTERONHORSE"},{"Vanilla/Cavalry_Knight","UNIT_SECRET_CAVALRYONHORSE"},
            {"Vanilla/DarkKingTransformed","UNIT_FANTASYEVIL_DARKKING"},{"Vanilla/TimeNinjaTransformed","UNIT_FANTASYGOOD_TIMENINJA"}
        };
        static readonly HashSet<string> Templates=new HashSet<string> {"Primal Wobbler","Goblin","Ents","Vulcanic Wraith","BirdDroneDebug","空单位311446"};
        public static void Build(List<UnitEntry> entries)
        {
            var byId=entries.GroupBy(e=>e.Blueprint.Entity.GUID).ToDictionary(g=>g.Key,g=>g.First());
            var incoming=entries.ToDictionary(e=>e,e=>new HashSet<UnitEntry>());
            var reader=new PrefabReferences();
            foreach(var parent in entries)
            {
                var refs=new HashSet<UnitBlueprint>();
                if(parent.Blueprint.UnitRiders!=null)refs.UnionWith(parent.Blueprint.UnitRiders.Where(b=>b));
                foreach(var root in new[]{parent.Base,parent.Right,parent.Left}.Concat(parent.Props??new GameObject[0]).Concat(parent.Children??new GameObject[0]))
                    if(root)refs.UnionWith(reader.Read(root));
                foreach(var bp in refs)if(bp&&byId.TryGetValue(bp.Entity.GUID,out var child)&&child!=parent)incoming[child].Add(parent);
            }
            foreach(var child in entries.Where(e=>e.IsDerived))
            {
                if(ScriptParents.TryGetValue(child.Module+"/"+child.AssetName,out var names))
                    foreach(var name in names.Split('|'))foreach(var parent in entries.Where(e=>e.Module==child.Module&&(e.Name==name||e.AssetName==name)))
                        if(parent!=child)incoming[child].Add(parent);
            }
            foreach(var child in entries.Where(e=>e.IsDerived))
            {
                child.Parents.Clear();var seen=new HashSet<UnitEntry>{child};var queue=new Queue<UnitEntry>(incoming[child]);
                while(queue.Count>0)
                {
                    var parent=queue.Dequeue();if(!seen.Add(parent))continue;
                    if(!parent.IsDerived&&parent.Faction!="FACTION_WIP")child.Parents.Add(parent);
                    else foreach(var next in incoming[parent])queue.Enqueue(next);
                }
                child.ParentText=child.Parents.Count>0?"主兵种："+string.Join("／",child.Parents.OrderBy(e=>e.Module).ThenBy(Labels.UnitName).Select(Labels.UnitName).Distinct()):
                    Templates.Contains(child.AssetName)||child.Name?.StartsWith("UNIT_WIP_")==true||child.AssetName?.StartsWith("Custom")==true?"内部模板":"主兵种：未找到引用";
            }
            foreach(var entry in entries.Where(e=>!e.IsDerived&&e.Faction=="FACTION_WIP"))entry.ParentText="内部模板";
        }

        sealed class PrefabReferences
        {
            readonly Dictionary<int,HashSet<UnitBlueprint>> cache=new Dictionary<int,HashSet<UnitBlueprint>>();
            readonly Dictionary<Type,FieldInfo[]> fields=new Dictionary<Type,FieldInfo[]>();
            public HashSet<UnitBlueprint> Read(GameObject root)
            {
                if(cache.TryGetValue(root.GetInstanceID(),out var saved))return saved;
                var result=new HashSet<UnitBlueprint>();var visited=new HashSet<int>();var objects=new HashSet<object>();var queue=new Queue<GameObject>();queue.Enqueue(root);
                while(queue.Count>0)
                {
                    var go=queue.Dequeue();if(!go||!visited.Add(go.GetInstanceID()))continue;
                    foreach(var c in go.GetComponentsInChildren<Component>(true).Where(c=>c&&!(c is Unit)&&!(c is Transform)))Scan(c,result,queue,objects);
                }
                cache[root.GetInstanceID()]=result;return result;
            }
            void Scan(object value,HashSet<UnitBlueprint> result,Queue<GameObject> queue,HashSet<object> visited)
            {
                if(value==null||!visited.Add(value))return;
                if(value is UnitBlueprint bp){if(bp)result.Add(bp);return;}
                if(value is GameObject go){if(go)queue.Enqueue(go);return;}
                if(value is Transform tr){if(tr)queue.Enqueue(tr.gameObject);return;}
                if(value is IList list){foreach(var item in list)Scan(item,result,queue,visited);return;}
                var type=value.GetType();
                if(type.IsPrimitive||type.IsEnum||value is string||value is Delegate||value is Unit)return;
                if(type.Assembly!=typeof(Unit).Assembly&&!EditorPlugin.CombatAssemblies.Contains(type.Assembly))return;
                if(!fields.TryGetValue(type,out var members))fields[type]=members=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                    .Where(f=>!f.IsStatic&&!f.IsNotSerialized&&(f.IsPublic||f.IsDefined(typeof(SerializeField),true))).ToArray();
                foreach(var field in members)Scan(field.GetValue(value),result,queue,visited);
            }
        }
    }
}
