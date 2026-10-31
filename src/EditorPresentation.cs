using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpansionaryEditor
{
    public enum FieldVisualState { Default, Saved, Draft, Restore, Error }
    public static class EditPresentation
    {
        public static FieldVisualState FieldState(bool enabled, string value, bool savedEnabled, string savedValue, bool error = false)
        {
            if (error) return FieldVisualState.Error;
            if (enabled != savedEnabled || (enabled && value != savedValue)) return enabled ? FieldVisualState.Draft : FieldVisualState.Restore;
            return enabled ? FieldVisualState.Saved : FieldVisualState.Default;
        }
        public static string StateLabel(FieldVisualState state)
        {
            switch (state) { case FieldVisualState.Saved: return "已修改"; case FieldVisualState.Draft: return "未保存"; case FieldVisualState.Restore: return "待恢复"; case FieldVisualState.Error: return "需修正"; default: return ""; }
        }
        public static string Category(string field, string label)
        {
            if (new[] { "ignoreTeamMates", "alwaysHitTeamMates", "onlyTeamMates", "ignoreRoot", "teamDamage" }.Contains(field)) return "目标与友军";
            if (new[] { "force", "forceMultiplier", "shootRecoil", "torsoRecoil", "onImpactForce", "impactMultiplier", "minVelocity", "curveForce", "defaultDrag", "defaultAngularDrag" }.Contains(field)) return "击退与物理";
            if (new[] { "magSize", "reloadTime", "internalCooldown", "numberOfObjects", "extraCDInMelee", "spawnDelay", "delayPerSpawn" }.Contains(field)) return "攻击与装填";
            if (field.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0 || new[] { "heal", "healAmount", "allowedHits", "allowRepeat", "destroyOnHit", "stopOnMapHit", "blockPoweredNeeded" }.Contains(field)) return "伤害与命中";
            if (field.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0 || field.IndexOf("spread", StringComparison.OrdinalIgnoreCase) >= 0 || new[] { "radius", "distance", "maxAngle" }.Contains(field)) return "范围与散布";
            if (field.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0 || new[] { "duration", "delay", "time", "onlyCountWhenUnitInRange", "startOnCD", "value", "extraRandomCooldown" }.Contains(field)) return "触发与时序";
            if (new[] { "amountToSpawn", "numberOfSpawns", "ammoClose", "ammoFar", "@count" }.Contains(field) || label.Contains("生成数量") || label.Contains("召唤数量")) return "生成与数量";
            if (field.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0 || field == "usedAsMovement") return "动作与移动";
            return "其他设置";
        }
        public static string BasicCategory(string field)
        {
            if(new[]{"health","_healthMultiplier","_damageMultiplaier"}.Contains(field))return "生命与伤害";
            if(new[]{"_attackSpeedMultiplier","movementSpeedMuiltiplier","_rangeMultiplier","turnSpeed","removeCloseRangeMiss"}.Contains(field))return "攻击与移动";
            if(new[]{"sizeMultiplier","minSizeRandom","maxSizeRandom","scaleWeapons","weaponSeparation"}.Contains(field))return "体型与外观";
            if(field=="targetingPriorityMultiplier")return "目标选择";
            return "物理与动作";
        }
    }
    public sealed class FoldoutState
    {
        readonly Dictionary<string, bool> normal = new Dictionary<string, bool>(), search = new Dictionary<string, bool>();
        string query = "";
        public void SetQuery(string value)
        { value = (value ?? "").Trim(); if (query != value) { query = value; search.Clear(); } }
        Dictionary<string, bool> Active => query.Length == 0 ? normal : search;
        public bool IsOpen(string key, bool initial = true) => Active.TryGetValue(key, out var value) ? value : query.Length > 0 || initial;
        public void SetOpen(string key, bool value) => Active[key] = value;
        public void SetAll(IEnumerable<string> keys, bool value) { foreach (var key in keys) SetOpen(key, value); }
    }
}
