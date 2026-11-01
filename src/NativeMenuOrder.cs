using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Landfall.TABS;
using Landfall.TABS.GameState;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExpansionaryEditor
{
    sealed class MenuOrderData
    {
        public int Version=1;
        public Dictionary<string,List<string>> FactionUnits=new Dictionary<string,List<string>>();
    }

    // Display order is independent of blueprint assets, numeric rules and balance presets.
    sealed class NativeMenuOrder
    {
        static readonly FieldInfo SelectedFaction=AccessTools.Field(typeof(PlacementUI),"m_selectedFaction");
        static readonly FieldInfo UnitLayout=AccessTools.Field(typeof(PlacementUI),"unitLayoutGroup");
        static readonly FieldInfo ExpandedUI=AccessTools.Field(typeof(PlacementUI),"expandedFactionUI");
        static readonly FieldInfo ExpandedOpen=AccessTools.Field(typeof(ExpandedFactionUI),"open");
        static readonly FieldInfo ExpandedPlacement=AccessTools.Field(typeof(ExpandedFactionUI),"placementUI");
        static readonly FieldInfo GameState=AccessTools.Field(typeof(PlacementUI),"m_gameStateManager");
        static readonly FieldInfo FarLeft=AccessTools.Field(typeof(UILayoutGroup),"m_farLeftIndex");
        static readonly FieldInfo UnitIds=AccessTools.Field(typeof(PlacementUI),"m_unitIDs");
        static readonly FieldInfo LastUnit=AccessTools.Field(typeof(PlacementUI),"m_lastSelectedUnit");
        static readonly FieldInfo LastIndex=AccessTools.Field(typeof(PlacementUI),"m_lastSelectedUnitIndex");
        static readonly MethodInfo Redraw=AccessTools.Method(typeof(PlacementUI),"RedrawFactionUnits");
        readonly string path;
        readonly Action<string> report;
        MenuOrderData data=new MenuOrderData();
        bool readError;
        internal NativeUnitDrag Drag;

        internal NativeMenuOrder(EditorPlugin plugin,Action<string> report)
        {
            this.report=report;path=Path.Combine(plugin.DataDirectory,"菜单排序.json");
            if(File.Exists(path))
            {
                try
                {
                    var loaded=JsonConvert.DeserializeObject<MenuOrderData>(File.ReadAllText(path));
                    if(loaded==null||loaded.Version!=1||loaded.FactionUnits==null||
                        loaded.FactionUnits.Any(p=>string.IsNullOrEmpty(p.Key)||p.Value==null||p.Value.Any(string.IsNullOrEmpty)))
                        throw new FormatException("排序格式无效");
                    data=loaded;
                }
                catch(Exception ex){readError=true;report("菜单排序未加载，原文件保留："+ex.Message);}
            }
            Drag=plugin.gameObject.AddComponent<NativeUnitDrag>();Drag.Order=this;
        }

        internal void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Faction),"GetRearrangedUnits"),
                postfix:new HarmonyMethod(typeof(NativeMenuOrder),nameof(OrderedUnits)));
            harmony.Patch(AccessTools.Method(typeof(UnitButton),"SetupUnit",new[]{typeof(UnitButton.UnitButtonData)}),
                postfix:new HarmonyMethod(typeof(NativeMenuOrder),nameof(SetupHandle)));
            foreach(string name in new[]{"OnOpen","OnClose","ForceOpenState"})
                harmony.Patch(AccessTools.Method(typeof(ExpandedFactionUI),name),
                    postfix:new HarmonyMethod(typeof(NativeMenuOrder),nameof(RefreshHandles)));
        }

        static NativeMenuOrder Current=>EditorPlugin.Instance?.MenuOrder;
        static string Id(DatabaseID id)=>id.m_modID+":"+id.m_ID;
        internal static Faction FactionOf(PlacementUI owner)=>owner?SelectedFaction.GetValue(owner) as Faction:null;
        internal static UILayoutGroup LayoutOf(PlacementUI owner)=>owner?UnitLayout.GetValue(owner) as UILayoutGroup:null;

        bool MenuOpen(PlacementUI owner)
        {
            if(readError||!owner||!owner.isActiveAndEnabled)return false;
            var expanded=ExpandedUI.GetValue(owner) as ExpandedFactionUI;
            return expanded&&expanded.gameObject.activeInHierarchy&&(bool)ExpandedOpen.GetValue(expanded);
        }

        internal bool CanOrder(PlacementUI owner)
        {
            if(!MenuOpen(owner)||!EditorPlugin.Instance||EditorPlugin.Instance.Visible)return false;
            var state=GameState.GetValue(owner) as GameStateManager;
            return state!=null&&state.GameState==Landfall.TABS.GameState.GameState.PlacementState;
        }

        static void OrderedUnits(Faction __instance,ref UnitBlueprint[] __result)
        {
            var order=Current;
            if(order==null||__result==null||!order.data.FactionUnits.TryGetValue(Id(__instance.Entity.GUID),out var saved))return;
            var ranks=new Dictionary<string,int>();
            for(int i=0;i<saved.Count;i++)if(!ranks.ContainsKey(saved[i]))ranks.Add(saved[i],i);
            // OrderBy is stable: unseen units retain their native order after the saved ones.
            __result=__result.OrderBy(u=>u&&ranks.TryGetValue(Id(u.Entity.GUID),out var rank)?rank:int.MaxValue).ToArray();
        }

        static void SetupHandle(UnitButton __instance,UnitButton.UnitButtonData __0)
        {
            var order=Current;var owner=__0.placementUI as PlacementUI;
            if(order==null||!owner||__instance.GetComponentInParent<UILayoutGroup>()!=LayoutOf(owner))return;
            var handle=__instance.GetComponent<UnitOrderHandle>()??__instance.gameObject.AddComponent<UnitOrderHandle>();
            handle.Button=__instance;handle.Owner=owner;handle.enabled=order.MenuOpen(owner);
        }

        static void RefreshHandles(ExpandedFactionUI __instance)
        {
            var order=Current;if(order==null)return;
            var owner=ExpandedPlacement.GetValue(__instance) as PlacementUI;
            var layout=LayoutOf(owner);if(!layout)return;
            bool enabled=order.MenuOpen(owner);
            foreach(var handle in layout.GetComponentsInChildren<UnitOrderHandle>(true))handle.enabled=enabled;
            if(!enabled&&order.Drag)order.Drag.Cancel();
        }

        internal bool MoveUnit(Faction faction,DatabaseID source,DatabaseID target,bool after)
        {
            var live=faction.GetRearrangedUnits().Where(u=>u).Select(u=>Id(u.Entity.GUID)).Distinct().ToList();
            string from=Id(source),to=Id(target);
            if(from==to||!live.Contains(from)||!live.Contains(to))return false;
            var moved=new List<string>(live);moved.Remove(from);moved.Insert(moved.IndexOf(to)+(after?1:0),from);
            if(moved.SequenceEqual(live))return false;
            string key=Id(faction.Entity.GUID);
            if(data.FactionUnits.TryGetValue(key,out var previous))moved.AddRange(previous.Where(id=>!moved.Contains(id)));
            var next=EditData.Clone(data);next.FactionUnits[key]=moved;Save(next);return true;
        }

        void Save(MenuOrderData next)
        {
            if(readError)throw new IOException("菜单排序文件读取失败，原文件未覆盖");
            EditData.AtomicWrite(path,JsonConvert.SerializeObject(next,Formatting.Indented),true);data=next;
        }

        internal void ResetUnits()
        {
            Drag.Cancel();Save(new MenuOrderData());
            if(PlacementUI.Instance)RefreshUnits(PlacementUI.Instance);
        }

        internal void RefreshUnits(PlacementUI owner)
        {
            var faction=FactionOf(owner);var layout=LayoutOf(owner);if(!faction||!layout)return;
            int first=(int)FarLeft.GetValue(layout);
            Redraw.Invoke(owner,new object[]{faction});
            var ids=(List<DatabaseID>)UnitIds.GetValue(owner);
            LastIndex.SetValue(owner,ids.IndexOf((DatabaseID)LastUnit.GetValue(null)));
            owner.StartCoroutine(RestoreScroll(owner,faction,layout,first));
        }

        IEnumerator RestoreScroll(PlacementUI owner,Faction faction,UILayoutGroup layout,int first)
        {
            // Let the native layout rebuild before returning to the same visible page.
            yield return null;
            if(!owner||!layout||FactionOf(owner)!=faction)yield break;
            try
            {
                int count=((List<DatabaseID>)UnitIds.GetValue(owner)).Count;
                int offset=Math.Min(first,Math.Max(0,count-layout.elementsShown));
                if(offset>0)layout.MoveList(true,offset);
            }
            catch(Exception ex){Report("排序已保存，列表滚动未恢复："+ex.Message);}
        }

        internal void Report(string message)=>report(message);
        internal void Dispose(){if(Drag){Drag.Cancel();UnityEngine.Object.Destroy(Drag);}}
    }

    public sealed class UnitOrderHandle:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        internal UnitButton Button;
        internal PlacementUI Owner;
        public void OnBeginDrag(PointerEventData e)
        {
            var order=EditorPlugin.Instance?.MenuOrder;
            if(order!=null&&order.Drag.Begin(Button,Owner,e))
            {
                e.eligibleForClick=false;
                // The source may scroll off-screen and be destroyed by the native layout.
                e.pointerDrag=order.Drag.gameObject;
            }
        }
        public void OnDrag(PointerEventData e){EditorPlugin.Instance?.MenuOrder?.Drag.OnDrag(e);}
        public void OnEndDrag(PointerEventData e){EditorPlugin.Instance?.MenuOrder?.Drag.OnEndDrag(e);}
    }

    public sealed class NativeUnitDrag:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        internal NativeMenuOrder Order;
        PlacementUI owner;
        Faction faction;
        UILayoutGroup layout;
        DatabaseID source,target;
        bool dragging,hasTarget,after,ownsFade;
        Camera eventCamera;
        CanvasGroup fade;
        float oldAlpha,nextScroll;
        GameObject visuals;
        RectTransform ghost,marker;
        readonly Vector3[] corners=new Vector3[4];

        internal bool Begin(UnitButton button,PlacementUI placement,PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||!Order.CanOrder(placement)||!button)return false;
            var current=NativeMenuOrder.FactionOf(placement);
            if(!current||current.GetRearrangedUnits().Count(u=>u)<2)return false;
            Cancel();owner=placement;faction=current;layout=NativeMenuOrder.LayoutOf(owner);source=button.UnitID;
            eventCamera=e.pressEventCamera;dragging=true;nextScroll=Time.unscaledTime+.35f;
            try
            {
                fade=button.GetComponent<CanvasGroup>();ownsFade=!fade;
                if(!fade)fade=button.gameObject.AddComponent<CanvasGroup>();oldAlpha=fade.alpha;fade.alpha=.35f;
                visuals=new GameObject("Unit order drag",typeof(RectTransform),typeof(Canvas));
                var canvas=visuals.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
                ghost=CreateImage("Unit",visuals.transform,new Color(1,1,1,.85f));
                var icon=button.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.gameObject.activeInHierarchy&&i.sprite&&i!=button.GetComponent<Image>());
                if(icon){var image=ghost.GetComponent<Image>();image.sprite=icon.sprite;image.preserveAspect=true;}
                else ghost.GetComponent<Image>().color=new Color(0,.42f,.96f,.7f);
                var rect=ScreenRect(button.transform as RectTransform);float size=Mathf.Clamp(Mathf.Min(rect.width,rect.height),48,128);
                ghost.sizeDelta=new Vector2(size,size);
                marker=CreateImage("Insert",visuals.transform,new Color(0,.48f,1));
                Track(e.position);return true;
            }
            catch(Exception ex){Cancel();Order.Report("无法开始兵种排序："+ex.Message);return false;}
        }

        static RectTransform CreateImage(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            return (RectTransform)go.transform;
        }

        Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var a=RectTransformUtility.WorldToScreenPoint(eventCamera,corners[0]);
            var b=RectTransformUtility.WorldToScreenPoint(eventCamera,corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        }

        bool Track(Vector2 position)
        {
            if(!dragging||!ghost||!marker)return false;
            ghost.position=position;hasTarget=false;Rect bounds=new Rect();bool haveBounds=false;
            float distance=float.MaxValue;Rect targetRect=new Rect();
            foreach(var rect in layout.GetAllButtonRectTransforms())
            {
                if(!rect||!rect.gameObject.activeInHierarchy)continue;
                var button=rect.GetComponent<UnitButton>();if(!button)continue;
                var screen=ScreenRect(rect);
                if(!haveBounds){bounds=screen;haveBounds=true;}
                else bounds=Rect.MinMaxRect(Mathf.Min(bounds.xMin,screen.xMin),Mathf.Min(bounds.yMin,screen.yMin),Mathf.Max(bounds.xMax,screen.xMax),Mathf.Max(bounds.yMax,screen.yMax));
                if(button.UnitID==source)continue;
                float d=Mathf.Abs(position.x-screen.center.x);
                if(d<distance){distance=d;target=button.UnitID;targetRect=screen;hasTarget=true;}
            }
            hasTarget=hasTarget&&haveBounds&&position.y>=bounds.yMin-24&&position.y<=bounds.yMax+24&&
                position.x>=bounds.xMin-80&&position.x<=bounds.xMax+80;
            marker.gameObject.SetActive(hasTarget);
            if(hasTarget)
            {
                after=position.x>targetRect.center.x;
                marker.sizeDelta=new Vector2(4,targetRect.height+8);
                marker.position=new Vector2(after?targetRect.xMax:targetRect.xMin,targetRect.center.y);
                if(Input.GetMouseButton(0)&&Time.unscaledTime>=nextScroll)
                {
                    int direction=position.x<bounds.xMin+24?-1:position.x>bounds.xMax-24?1:0;
                    if(direction!=0&&layout.CanMoveListMore(direction>0))
                    {layout.MoveList(direction>0,1);nextScroll=Time.unscaledTime+.35f;}
                }
            }
            return hasTarget;
        }

        public void OnBeginDrag(PointerEventData e){}
        public void OnDrag(PointerEventData e)
        {if(dragging){e.eligibleForClick=false;if(!Valid())Cancel();else Track(e.position);}}
        public void OnEndDrag(PointerEventData e)
        {
            if(!dragging)return;
            bool moved=false;
            try{if(Valid()&&Track(e.position))moved=Order.MoveUnit(faction,source,target,after);}
            catch(Exception ex){Order.Report("兵种排序未保存："+ex.Message);}
            var placement=owner;Cancel();
            if(moved)
                try{Order.RefreshUnits(placement);}
                catch(Exception ex){Order.Report("排序已保存，列表未刷新："+ex.Message);}
        }

        bool Valid()=>Order.CanOrder(owner)&&layout&&NativeMenuOrder.FactionOf(owner)==faction;
        void Update()
        {
            if(!dragging)return;
            if(!Valid()||Input.GetKeyDown(KeyCode.Escape)){Cancel();return;}
            Track(Input.mousePosition);
        }
        void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        void OnDisable(){Cancel();}
        void OnDestroy(){Cancel();}
        internal void Cancel()
        {
            dragging=false;hasTarget=false;
            if(fade){fade.alpha=oldAlpha;if(ownsFade)Destroy(fade);}
            if(visuals)Destroy(visuals);
            visuals=null;ghost=marker=null;fade=null;owner=null;faction=null;layout=null;
        }
    }
}
