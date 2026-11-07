using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Landfall.TABS;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpansionaryEditor
{
    // Only prefab/serialized combat data is traversed. Live units, services and Unity assets are not followed.
    public sealed class CombatGraph
    {
        public readonly List<ValueSlot> Slots = new List<ValueSlot>();
        public readonly List<ConstantSlot> Constants = new List<ConstantSlot>();
        public readonly List<AbilityInfo> Abilities = new List<AbilityInfo>();
        public readonly List<string> Notes = new List<string>();
        readonly string unitKey;
        readonly UnitRule rule;
        readonly Transform host;
        readonly bool clone;
        readonly HashSet<string> constantKeys = new HashSet<string>();
        Dictionary<int, GameObject> visited;
        int nodes;
        string abilityKey;

        static readonly Dictionary<string, HashSet<string>> Allowed = new Dictionary<string, HashSet<string>> {
            {"Weapon", Set("internalCooldown,maxRange,maxAngle,startOnCooldown,randomCooldown,defaultDrag,defaultAngularDrag")},
            {"RangeWeapon", Set("objectToSpawn,unitToSpawn,magSize,reloadTime,numberOfObjects,spawnDelay,useSpawnDelayForEveryShot,delayPerSpawn,useRandomDelay,minRandom,maxRandom,spread,projectileSpeedSpread,force,clampRange,shootRecoil,torsoRecoil,extraSpreadInMelee,extraCDInMelee,fallTime")},
            {"MeleeWeapon", Set("curveForce,yTargetOffset,canDealDamageOutSideOfSwing,requiredPowerToParry")},
            {"CollisionWeapon", Set("damage,selfDamageMultiplier,impactMultiplier,enemyMassPow,minVelocity,onImpactForce,massCap,ignoreTeamMates,teamDamage,staticDamageValue,onlyOncePerData,cooldown")},
            {"ProjectileHit", Set("damage,force,allowedHits,allowRepeat,destroyOnHit,ignoreTeamMates,alwaysHitTeamMates,blockPoweredNeeded,stopOnMapHit,objectsToSpawn,hitEffects,hitEvents,onUnitsEvent,stopAditionalHitsOnArmor")},
            {"Explosion", Set("damage,percentDamage,radius,force,minMassCap,stopAttacksFor,ignoreTeamMates,onlyTeamMates,ignoreRoot,onlyRoot,effects")},
            {"DodgeMove", Set("animationSpeed,animationSpeedWhenPositiveCurve,forceMultiplier,divideForceByMass,cancelSelf,usedAsMovement,minRange,maxRange,moves")},
            {"CombatMove", Set("cooldown,randomCooldown,m_cooldown,m_randomCooldown,conditions,moves,move")},
            {"ConditionalEvent",Set("events,extraRange,failChancePer100Hp")},
            {"ConditionalEventInstance",Set("conditions,delay,moves")},
            {"EventCondition",Set("value,extraRandomCooldown,startOnCD,cooldownRange,onlyCountWhenUnitInRange")},
            {"Cooldown",Set("cooldown")},
            {"SpawnObject",Set("objectToSpawn,numberToSpawn,timeBetweenSpawns,spread,maxRange")},
            {"UnitSpawner",Set("unitBlueprints,unitsToSpawn,unitBlueprint,unitToSpawn,amountToSpawn,numberOfSpawns")},
            {"MoveTransform", Set("speed,gravity,drag")},
            {"AddForce", Set("force,multiplier")},
            {"ObjectToSpawn", Set("objectToSpawn,spawnedObject,spawnObject,delay,numberOfSpawns")}
        };
        static HashSet<string> Set(string value) => new HashSet<string>(value.Split(','));
        public CombatGraph(string key, UnitRule settings = null, Transform parent = null)
        { unitKey = key; rule = settings; host = parent; clone = parent != null; }

        public GameObject Root(GameObject original, string key, string label)
        {
            if (!original) return null;
            visited = new Dictionary<int, GameObject>();
            nodes = 0;
            abilityKey=key.StartsWith("C")?key:null;
            if(abilityKey!=null)Abilities.Add(Labels.Ability(key,original));
            return Visit(original, key, label, 0);
        }

        GameObject Visit(GameObject source, string path, string group, int depth)
        {
            if (!source) return null;
            int id = source.GetInstanceID();
            if (visited.TryGetValue(id, out var existing)) return existing;
            if (++nodes > 256 || depth > 7)
            {
                Notes.Add(group + "：部分深层引用超过遍历上限，未纳入编辑");
                return source;
            }
            GameObject target = clone ? Object.Instantiate(source, host, false) : source;
            if (clone)
            {
                target.name = source.name;
                var tag = target.GetComponent<OwnerTag>() ?? target.AddComponent<OwnerTag>();
                tag.UnitKey = unitKey;
                tag.RootPath = path;
            }
            visited[id] = target;
            WalkChildren(target.transform, target.transform, path, group, depth);
            return target;
        }

        void WalkChildren(Transform t, Transform root, string path, string group, int depth)
        {
            var types = new Dictionary<Type, int>();
            foreach(var c in t.GetComponents<Component>())
            {
                if (!c) continue;
                Type type = c.GetType();
                if (!types.ContainsKey(type)) types[type] = 0;
                int ordinal = types[type]++;
                if (!IsCombatType(type)) continue;
                string cp = path + "/" + EditorPlugin.ComponentId(type) + "[" + ordinal + "]";
                string componentAbility=abilityKey;
                if(abilityKey==null&&IsAddon(type))
                {
                    var intrinsic=Labels.IntrinsicAbility(type,cp,group);
                    if(intrinsic!=null){Abilities.Add(intrinsic);componentAbility=cp;}
                }
                if (c is Behaviour behaviour && (IsAddon(type) || type.Name == "DodgeMove" || type.Name == "CombatMove"))
                {
                    var slot = new ValueSlot {Key=cp+"/@enabled",Group=group,Label="启用此组件／技能",Path=t.name,
                        ComponentName=Labels.Component(type),ComponentType=type,FieldName="enabled",Type=typeof(bool),Original=Numbers.Text(behaviour.enabled),Assign=s=>behaviour.enabled=bool.Parse(s)};
                    Add(slot);
                }
                WalkFields(c, type, cp, group, t.name, root, depth, 0,type);
                if (IsAddon(type)) AddConstants(type, group,componentAbility);
            }
            for(int i=0; i<t.childCount; i++) WalkChildren(t.GetChild(i),root,path+"/child["+i+"]",group,depth);
        }

        static bool IsAddon(Type type) => type.Namespace == "AddonExpansionary"||EditorPlugin.CombatAssemblies.Contains(type.Assembly);
        static bool IsCombatType(Type type) => IsAddon(type) || Allowed.ContainsKey(type.Name) || type.Name.Contains("CombatMove");
        static bool Serialized(FieldInfo f) => !f.IsStatic && !f.IsInitOnly && !f.IsNotSerialized &&
            (f.IsPublic || f.IsDefined(typeof(SerializeField), true));
        static bool AllowedField(Type type, FieldInfo f)
        {
            if (Labels.PrivateSetting(type,f.Name))return true;
            if (!Serialized(f)) return false;
            if (Allowed.TryGetValue(f.DeclaringType.Name, out var allow)) return allow.Contains(f.Name);
            if (IsAddon(type) || IsAddon(f.DeclaringType))
            {
                string n=f.Name.ToLowerInvariant();
                return !new[]{"counter","sinces","since","current","cached","lifetime"}.Any(n.Contains) &&
                    n != "unit" && n != "ownunit" && n != "data" && n != "target";
            }
            return type.Name.Contains("CombatMove") || (!typeof(Component).IsAssignableFrom(type) && type.Assembly == typeof(Unit).Assembly);
        }

        void WalkFields(object obj, Type type, string path, string group, string objectName, Transform root, int depth, int nesting,Type ownerType)
        {
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(f=>f.Name,StringComparer.Ordinal))
            {
                if (!AllowedField(type,field)) continue;
                object value=field.GetValue(obj);
                string key=path+"/"+field.Name;
                if (Numbers.Scalar(field.FieldType))
                {
                    var slot=new ValueSlot {Key=key,Group=group,Path=objectName,ComponentName=Labels.Component(type),FieldName=field.Name,
                        Label=Labels.ReadableLabel(field.Name,type),ComponentType=ownerType,Type=field.FieldType,Original=Numbers.Text(value),Assign=s=>field.SetValue(obj,Numbers.Parse(s,field.FieldType)),Readable=Labels.ReadableField(field.Name,type)};
                    if(obj is EventCondition condition)DescribeCondition(slot,condition);
                    Add(slot);
                }
                else if (field.FieldType == typeof(UnitBlueprint[]) && value is UnitBlueprint[] units && units.Length > 0)
                {
                    var slot=new ValueSlot {Key=key+"/@count",Group=group,Path=objectName,ComponentName=Labels.Component(type),FieldName=field.Name,
                        Label="召唤数量",ComponentType=ownerType,Type=typeof(int),Original=units.Length.ToString(),Readable=true,Assign=s=> {
                            int count=(int)Numbers.Parse(s,typeof(int));
                            if(count<0 || count>128) throw new FormatException("召唤数量需要在 0–128 之间");
                            field.SetValue(obj, Enumerable.Range(0,count).Select(i=>units[i%units.Length]).ToArray());
                        }};
                    Add(slot);
                }
                else if (value != null && nesting < 3)
                {
                    var next=WalkReference(value,field.FieldType,key,group,objectName,root,depth,nesting+1,ownerType);
                    if (clone && !ReferenceEquals(value,next)) field.SetValue(obj,next);
                    else if (clone && field.FieldType.IsValueType) field.SetValue(obj,next);
                }
            }
        }

        object WalkReference(object value, Type type, string path, string group, string objectName, Transform root, int depth, int nesting,Type ownerType)
        {
            GameObject go = value as GameObject;
            if (value is Transform tr) go=tr.gameObject;
            if (go)
            {
                if (go.transform == root || go.transform.IsChildOf(root)) return value;
                var next=Visit(go,path+"->",group,depth+1);
                return value is Transform ? (object)next.transform : next;
            }
            if (value is Object || type == typeof(string) || typeof(Delegate).IsAssignableFrom(type)) return value;
            if (value is Array array)
            {
                for(int i=0;i<array.Length;i++)
                {
                    var item=array.GetValue(i);
                    if(item==null)continue;
                    var next=WalkReference(item,item.GetType(),path+"["+i+"]",group,objectName,root,depth,nesting,ownerType);
                    if(clone)array.SetValue(next,i);
                }
            }
            else if (type.Assembly==typeof(Unit).Assembly && nesting<3)
                WalkFields(value,type,path,group,objectName,root,depth,nesting,ownerType);
            return value;
        }

        void Add(ValueSlot slot)
        {
            if(slot.FieldName=="enabled")slot.Readable=true;
            if(string.IsNullOrEmpty(slot.Note))slot.Note=slot.Label;
            Slots.Add(slot);
            if (clone && rule != null && rule.Components.TryGetValue(slot.Key,out var input))
                slot.Assign(input);
        }

        void AddConstants(Type type, string group,string componentAbility)
        {
            var methods=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(type.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)
                    .SelectMany(t=>t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)));
            foreach(var method in methods)
            {
                if (method.GetMethodBody()==null || method.IsConstructor)continue;
                int n=0;
                foreach(var instruction in PatchProcessor.GetOriginalInstructions(method))
                {
                    if(instruction.opcode!=System.Reflection.Emit.OpCodes.Ldc_R4 && instruction.opcode!=System.Reflection.Emit.OpCodes.Ldc_R8)continue;
                    string key=ConstantPatcher.Key(method,n++);
                    if(!constantKeys.Add(key))continue;
                    string friendly=Labels.NamedConstant(type,method.Name,n-1);
                    Constants.Add(new ConstantSlot {Key=key,Group=group,Method=method,Double=instruction.operand is double,AbilityKey=componentAbility,
                        Label=friendly??Labels.Component(type)+" · "+method.Name+" · 参数 "+n,Original=Numbers.Text(instruction.operand),Readable=friendly!=null,
                        Note=friendly==null?null:"技能内部计时已映射为秒；保存后重新摆放生效。"});
                }
            }
        }

        static void DescribeCondition(ValueSlot slot,EventCondition condition)
        {
            slot.Readable=false;
            string kind=condition.conditionType.ToString(),bound=condition.valueType.ToString()=="Max"?"上限":"下限";
            if(kind=="Cooldown")
            {
                switch(slot.FieldName) {
                    case "value":slot.Label="触发冷却（秒）";slot.Readable=true;slot.Note="此触发环节两次发动的间隔；0 表示不等待，其他触发条件仍需满足。";break;
                    case "extraRandomCooldown":slot.Label="额外随机冷却（秒）";slot.Readable=true;slot.Note="在基础冷却上增加随机等待；设为 0 可关闭随机量。";break;
                    case "startOnCD":slot.Label="开场先等待冷却";slot.Readable=true;break;
                    case "onlyCountWhenUnitInRange":slot.Label="仅接敌时计算冷却";slot.Readable=true;break;
                }
            }
            else if(slot.FieldName=="value")
            {
                switch(kind) {
                    case "UnitDistanceToTarget":slot.Label="触发距离"+bound;slot.Readable=true;break;
                    case "UnitHealth":slot.Label="自身剩余生命比例上限（0–1）";slot.Readable=true;break;
                    case "Chance":slot.Label="触发概率（0–1）";slot.Readable=true;break;
                    case "ChancePerSecond":slot.Label="每秒触发率";slot.Readable=true;break;
                }
            }
        }
    }
}
