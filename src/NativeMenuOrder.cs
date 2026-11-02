using System;
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
        static readonly FieldInfo UnitPlacement=AccessTools.Field(typeof(UnitButton),"m_placementUI");
        static readonly FieldInfo GameState=AccessTools.Field(typeof(PlacementUI),"m_gameStateManager");
        static readonly FieldInfo Elements=AccessTools.Field(typeof(UILayoutGroup),"m_elements");
        static readonly FieldInfo SpawnedElements=AccessTools.Field(typeof(UILayoutGroup),"m_spawnedElements");
        static readonly FieldInfo UnitIds=AccessTools.Field(typeof(PlacementUI),"m_unitIDs");
        static readonly FieldInfo LastUnit=AccessTools.Field(typeof(PlacementUI),"m_lastSelectedUnit");
        static readonly FieldInfo LastIndex=AccessTools.Field(typeof(PlacementUI),"m_lastSelectedUnitIndex");
        static readonly FieldInfo SelectedButton=AccessTools.Field(typeof(PlacementUI),"m_selectedButton");
        static readonly MethodInfo UpdatedList=AccessTools.Method(typeof(UILayoutGroup),"UpdatedListPos");
        readonly string path;
        readonly Action<string> report;
        readonly Action<string> trace;
        MenuOrderData data=new MenuOrderData();
        bool readError;
        internal NativeUnitDrag Drag;

        internal NativeMenuOrder(EditorPlugin plugin,Action<string> report,Action<string> trace)
        {
            this.report=report;this.trace=trace;path=Path.Combine(plugin.DataDirectory,"菜单排序.json");
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
            harmony.Patch(AccessTools.Method(typeof(UnitButton),"OnPointerClick",new[]{typeof(PointerEventData)}),
                prefix:new HarmonyMethod(typeof(NativeMenuOrder),nameof(AllowUnitClick)));
        }

        static NativeMenuOrder Current=>EditorPlugin.Instance?.MenuOrder;
        static string Id(DatabaseID id)=>id.m_modID+":"+id.m_ID;
        internal static Faction FactionOf(PlacementUI owner)=>owner?SelectedFaction.GetValue(owner) as Faction:null;
        internal static UILayoutGroup LayoutOf(PlacementUI owner)=>owner?UnitLayout.GetValue(owner) as UILayoutGroup:null;

        internal static PlacementUI OwnerOf(UnitButton button)=>button?UnitPlacement.GetValue(button) as PlacementUI:null;

        bool MenuOpen(PlacementUI owner)
        {
            if(readError||!owner||!owner.gameObject.activeInHierarchy)return false;
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

        static bool AllowUnitClick(UnitButton __instance,PointerEventData __0)
        {
            var order=Current;
            return order==null||!order.Drag||!order.Drag.BlocksClick(__instance,__0);
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
            var next=EditData.Clone(data);next.FactionUnits[key]=moved;Save(next);
            trace("ORDER SAVED "+key+": "+from+(after?" after ":" before ")+to);return true;
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
            var elements=(List<UILayoutGroup.ElementDataWrapper>)Elements.GetValue(layout);
            if(elements==null||elements.Count==0)return;
            var ranks=new Dictionary<string,int>();
            foreach(var unit in faction.GetRearrangedUnits())
                if(unit&&!ranks.ContainsKey(Id(unit.Entity.GUID)))ranks.Add(Id(unit.Entity.GUID),ranks.Count);
            // Keep the native unlock/campaign filtering and button data. Only their
            // order changes; rebuilding the row destroys its live, paged buttons.
            var sorted=elements.OrderBy(e=>e.unitData?.unit&&ranks.TryGetValue(Id(e.unitData.unit.Entity.GUID),out var rank)?rank:int.MaxValue).ToArray();
            var selected=(DatabaseID)LastUnit.GetValue(null);
            var previous=SelectedButton.GetValue(owner) as UnitButton;
            if(previous)previous.Deselect();
            SelectedButton.SetValue(owner,null);
            for(int i=0;i<sorted.Length;i++)elements[i]=sorted[i];
            var ids=(List<DatabaseID>)UnitIds.GetValue(owner);
            ids.Clear();ids.AddRange(sorted.Where(e=>e.unitData?.unit).Select(e=>e.unitData.unit.Entity.GUID));
            LastIndex.SetValue(owner,ids.IndexOf(selected));
            UnitButton selectedButton=null;
            var spawned=(List<UILayoutGroup.SpawnedElementWrapper>)SpawnedElements.GetValue(layout);
            if(spawned!=null)foreach(var slot in spawned)
            {
                if(!slot.gameObject||slot.dataIndex<0||slot.dataIndex>=elements.Count)continue;
                var button=slot.gameObject.GetComponent<UnitButton>();if(!button)continue;
                var entry=elements[slot.dataIndex].unitData;if(entry==null||!entry.unit)continue;
                if(button.UnitID!=entry.unit.Entity.GUID)
                {
                    // Cancel only this reused button's old highlight/selection
                    // animation before binding its new native UnitButtonData.
                    button.StopAllCoroutines();button.Deselect();button.SetupUnit(entry);
                }
                if(button.UnitID==selected&&button.Unlocked)selectedButton=button;
            }
            if(selectedButton){selectedButton.OnEnter();selectedButton.SelectUnit(false);selectedButton.OnExit();}
            // Existing slot indices, positions and scroll offset remain intact,
            // including an edge-scroll animation already in progress.
            if(spawned!=null&&spawned.Count>0)UpdatedList.Invoke(layout,null);
            trace("ORDER REFRESHED "+Id(faction.Entity.GUID)+": "+ids.Count+" units");
        }

        internal void Report(string message)=>report(message);
        internal void Trace(string message)=>trace(message);
        internal void Dispose(){if(Drag){Drag.Cancel();UnityEngine.Object.Destroy(Drag);}}
    }

    // One input owner for the whole unit row. No per-button enabled state or popup
    // reverse reference is required, and paging can destroy the original button safely.
    public sealed class NativeUnitDrag:MonoBehaviour
    {
        enum Phase { Idle,Pressed,Dragging }
        internal NativeMenuOrder Order;
        Phase phase;
        PlacementUI owner,suppressedOwner;
        Faction faction;
        UILayoutGroup layout;
        UnitButton pressedButton;
        DatabaseID source,target;
        Vector2 pressPosition;
        float dragThreshold;
        int suppressClickThrough=-1;
        bool hasTarget,after,ownsFade;
        Camera eventCamera;
        CanvasGroup fade;
        float oldAlpha,nextScroll;
        GameObject visuals;
        RectTransform ghost,marker;
        readonly Vector3[] corners=new Vector3[4];
        readonly List<RaycastResult> hits=new List<RaycastResult>();

        void TryPress()
        {
            var events=EventSystem.current;
            if(!events||!EditorPlugin.Instance||EditorPlugin.Instance.Visible)return;
            var pointer=new PointerEventData(events){position=Input.mousePosition,button=PointerEventData.InputButton.Left};
            hits.Clear();events.RaycastAll(pointer,hits);
            if(hits.Count==0)return;
            // Only the top hit counts: never drag a unit behind another open UI panel.
            var hit=hits[0];var button=hit.gameObject?hit.gameObject.GetComponentInParent<UnitButton>():null;
            var placement=NativeMenuOrder.OwnerOf(button);
            if(!button||!Order.CanOrder(placement))return;
            var current=NativeMenuOrder.FactionOf(placement);
            if(!current||current.GetRearrangedUnits().Count(u=>u)<2)return;
            owner=placement;faction=current;layout=NativeMenuOrder.LayoutOf(owner);
            if(!layout){owner=null;faction=null;return;}
            pressedButton=button;source=button.UnitID;pressPosition=pointer.position;
            dragThreshold=Mathf.Max(4,events.pixelDragThreshold);
            eventCamera=hit.module?hit.module.eventCamera:null;phase=Phase.Pressed;
        }

        bool Begin()
        {
            if(!pressedButton)return false;
            phase=Phase.Dragging;SuppressRelease();nextScroll=Time.unscaledTime+.35f;
            try
            {
                fade=pressedButton.GetComponent<CanvasGroup>();ownsFade=!fade;
                if(!fade)fade=pressedButton.gameObject.AddComponent<CanvasGroup>();oldAlpha=fade.alpha;fade.alpha=.35f;
                visuals=new GameObject("Unit order drag",typeof(RectTransform),typeof(Canvas));
                var canvas=visuals.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
                ghost=CreateImage("Unit",visuals.transform,new Color(1,1,1,.85f));
                var icon=pressedButton.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.gameObject.activeInHierarchy&&i.sprite&&i!=pressedButton.GetComponent<Image>());
                if(icon){var image=ghost.GetComponent<Image>();image.sprite=icon.sprite;image.preserveAspect=true;}
                else ghost.GetComponent<Image>().color=new Color(0,.42f,.96f,.7f);
                var rect=ScreenRect(pressedButton.transform as RectTransform);float size=Mathf.Clamp(Mathf.Min(rect.width,rect.height),48,128);
                ghost.sizeDelta=new Vector2(size,size);
                marker=CreateImage("Insert",visuals.transform,new Color(0,.48f,1));
                Order.Trace("ORDER DRAG "+source.m_modID+":"+source.m_ID);
                return true;
            }
            catch(Exception ex){Cancel();Order.Report("无法开始兵种排序："+ex.Message);return false;}
        }

        internal bool BlocksClick(UnitButton button,PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return false;
            var placement=NativeMenuOrder.OwnerOf(button);
            if(phase==Phase.Dragging&&placement==owner)return true;
            // EventSystem.Update may run before this controller on the release frame.
            if(phase==Phase.Pressed&&placement==owner&&PastThreshold(e.position))return true;
            return placement&&placement==suppressedOwner&&Time.frameCount<=suppressClickThrough;
        }

        void SuppressRelease(){suppressedOwner=owner;suppressClickThrough=Time.frameCount+1;}
        bool PastThreshold(Vector2 position)=>(position-pressPosition).sqrMagnitude>=dragThreshold*dragThreshold;

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
            if(phase!=Phase.Dragging||!ghost||!marker)return false;
            ghost.position=position;hasTarget=false;Rect bounds=new Rect();bool haveBounds=false;
            float distance=float.MaxValue;Rect targetRect=new Rect();
            var rects=layout.GetAllButtonRectTransforms();
            if(rects==null){marker.gameObject.SetActive(false);return false;}
            foreach(var rect in rects)
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

        void Finish(Vector2 position)
        {
            bool moved=false;
            try{if(Valid()&&Track(position))moved=Order.MoveUnit(faction,source,target,after);}
            catch(Exception ex){Order.Report("兵种排序未保存："+ex.Message);}
            var placement=owner;Cancel();
            if(moved)
                try{Order.RefreshUnits(placement);}
                catch(Exception ex){Order.Report("排序已保存，列表未刷新："+ex.Message);}
        }

        bool Valid()=>Order.CanOrder(owner)&&layout&&NativeMenuOrder.FactionOf(owner)==faction;
        void Update()
        {
            if(Input.GetMouseButtonDown(0))
            {
                Cancel();suppressedOwner=null;suppressClickThrough=-1;TryPress();
            }
            if(phase==Phase.Idle)return;
            if(!Valid()||Input.GetKeyDown(KeyCode.Escape)){Cancel();return;}
            Vector2 position=Input.mousePosition;
            if(phase==Phase.Pressed&&PastThreshold(position))
            {
                if(!Begin()){Cancel();return;}
            }
            if(Input.GetMouseButtonUp(0))
            {
                if(phase==Phase.Dragging)Finish(position);else Cancel();
                return;
            }
            if(!Input.GetMouseButton(0)){Cancel();return;}
            if(phase==Phase.Dragging)Track(position);
        }
        void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        void OnDisable(){Cancel();}
        void OnDestroy(){Cancel();}
        internal void Cancel()
        {
            if(phase==Phase.Dragging)SuppressRelease();
            phase=Phase.Idle;hasTarget=false;
            if(fade){fade.alpha=oldAlpha;if(ownsFade)Destroy(fade);}
            if(visuals)Destroy(visuals);
            visuals=null;ghost=marker=null;fade=null;pressedButton=null;owner=null;faction=null;layout=null;
        }
    }
}
