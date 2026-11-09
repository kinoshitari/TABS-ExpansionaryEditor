using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        public bool Visible;
        UnitEntry selected,pendingSelection;
        UnitRule draft=new UnitRule();
        bool dirty;
        string search="",paramSearch="",bulkFactor="1.5",error="",confirm="",priceInput;
        int tab,bulkKind;
        const int StatsTab=0,ResistanceTab=1,RightHandTab=2,LeftHandTab=3,AbilitiesTab=4,BatchTab=5,SummaryTab=6,LibraryTab=7;
        Vector2 unitsScroll,valuesScroll;
        readonly HashSet<string> batch=new HashSet<string>();
        readonly string[] tabs={"属性","承伤","右手","左手","技能","批量","改动","预设"};
        bool ComponentTab=>tab>=RightHandTab&&tab<=AbilitiesTab;
        float rowLabelWidth=400;
        bool clearInputFocus;
        string focusedInput="";
        IMECompositionMode previousIme;
        int lastImeFrame=-10;
        readonly List<string> inputOrder=new List<string>();
        readonly Dictionary<string,string> fieldErrors=new Dictionary<string,string>();
        readonly HashSet<string> pendingTextCommit=new HashSet<string>();
        bool Composing=>!string.IsNullOrEmpty(Input.compositionString)||Time.frameCount-lastImeFrame<=1;
        GUIStyle titleStyle,subStyle,labelStyle,inputStyle,invalidInputStyle,previewStyle,buttonStyle,activeStyle;
        GUIStyle smallStyle,sectionStyle,checkStyle,valueLabelStyle,columnStyle;
        GUISkin editorSkin;
        GUIStyle surfaceStyle,sidebarStyle,modifiedInputStyle;
        Texture2D background,sidebarBackground,lineTexture,modalShade;
        Texture2D controlTexture,hoverTexture,selectedTexture,fieldTexture;
        Texture2D primaryTexture;
        Font chineseFont;
        float oldTimeScale=1;
        bool oldCursorVisible,oldActionsEnabled;
        CursorLockMode oldCursorLock;

        void Update()
        {
            if(Visible&&!string.IsNullOrEmpty(Input.compositionString))lastImeFrame=Time.frameCount;
            if(Input.GetKeyDown(KeyCode.F6)&&!Composing)Toggle();
            if(!Visible)return;
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            if(Input.GetKeyDown(KeyCode.Escape)&&!Composing)
            {
                if(sizingByClick){sizingByClick=false;editorRect=dragRect;SaveWindow();}
                else if(showSettings)showSettings=false;
                else if(!string.IsNullOrEmpty(confirm))confirm="";
                else if(!string.IsNullOrEmpty(focusedInput)){clearInputFocus=true;focusedInput="";}
                else Toggle();
            }
        }
        void LateUpdate(){if(Visible)Time.timeScale=0;}
        void Toggle()
        {
            Visible=!Visible;
            if(Visible)
            {
                previousIme=Input.imeCompositionMode;Input.imeCompositionMode=IMECompositionMode.On;
                oldTimeScale=Time.timeScale;oldCursorVisible=Cursor.visible;oldCursorLock=Cursor.lockState;
                oldActionsEnabled=Landfall.TABS_Input.PlayerActions.Instance.Enabled;
                Landfall.TABS_Input.PlayerActions.Instance.Enabled=false;
                Time.timeScale=0;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            }
            else
            {
                confirm="";showSettings=false;Input.imeCompositionMode=previousIme;focusedInput="";Time.timeScale=oldTimeScale;Cursor.visible=oldCursorVisible;Cursor.lockState=oldCursorLock;
                StartCoroutine(RestoreActions());
            }
        }
        System.Collections.IEnumerator RestoreActions()
        {yield return null;while(Input.GetKey(KeyCode.F6)||Input.GetKey(KeyCode.Escape))yield return null;if(!Visible)Landfall.TABS_Input.PlayerActions.Instance.Enabled=oldActionsEnabled;}
        static Texture2D Texture(Color color)
        {var t=new Texture2D(1,1){hideFlags=HideFlags.HideAndDontSave};t.SetPixel(0,0,color);t.Apply();return t;}
        static Texture2D RoundedTexture(Color color)
        {
            const int size=48;const float radius=12;
            var texture=new Texture2D(size,size){hideFlags=HideFlags.HideAndDontSave};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(radius-x,0,x-(size-1-radius)),dy=Mathf.Max(radius-y,0,y-(size-1-radius));
                var pixel=color;pixel.a*=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy));texture.SetPixel(x,y,pixel);
            }
            texture.Apply();return texture;
        }
        void EnsureStyles()
        {
            // GUIStyle's native background references alone do not retain runtime textures
            // through UnloadUnusedAssets. Keep every owned texture alive on this component.
            if(titleStyle!=null&&chineseFont&&editorSkin&&background&&sidebarBackground&&lineTexture&&modalShade&&
                controlTexture&&hoverTexture&&selectedTexture&&fieldTexture&&primaryTexture&&!refreshTheme)return;
            ReleaseUiResources();InitStyles();refreshTheme=false;
        }
        void ReleaseUiResources()
        {
            foreach(var resource in new UnityEngine.Object[]{background,sidebarBackground,lineTexture,modalShade,
                controlTexture,hoverTexture,selectedTexture,fieldTexture,primaryTexture,chineseFont,editorSkin})
                if(resource)Destroy(resource);
            background=sidebarBackground=lineTexture=modalShade=null;
            controlTexture=hoverTexture=selectedTexture=fieldTexture=primaryTexture=null;
            chineseFont=null;editorSkin=null;titleStyle=null;
        }
        GUIStyle Style(int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            // Game skin offsets/fixed heights are incompatible with dynamically loaded CJK font metrics.
            var s=new GUIStyle {font=chineseFont,fontSize=size,alignment=alignment,richText=false,
                clipping=TextClipping.Clip,contentOffset=Vector2.zero,padding=new RectOffset(0,0,4,4),
                margin=new RectOffset(0,0,0,0),fixedHeight=0,stretchWidth=true,wordWrap=false};
            foreach(var state in new[]{s.normal,s.hover,s.active,s.focused,s.onNormal,s.onHover,s.onActive,s.onFocused})state.textColor=color;
            return s;
        }
        void InitStyles()
        {
            bool classic=ClassicUI;
            chineseFont=Font.CreateDynamicFontFromOSFont(classic?new[]{"Microsoft YaHei","SimHei"}:new[]{"Segoe UI","Microsoft YaHei UI","Microsoft YaHei"},22);
            chineseFont.hideFlags=HideFlags.HideAndDontSave;
            var text=classic?new Color(.93f,.91f,.87f):new Color(.114f,.114f,.122f);
            var secondary=classic?new Color(.68f,.72f,.76f):new Color(.40f,.40f,.43f);
            var accent=classic?new Color(.90f,.77f,.55f):new Color(0,.42f,.96f);
            background=ThemeTexture(classic?new Color(.125f,.14f,.16f):Color.white);
            sidebarBackground=ThemeTexture(classic?new Color(.15f,.17f,.19f):new Color(.955f,.955f,.968f));
            lineTexture=Texture(classic?new Color(.23f,.26f,.29f):new Color(.88f,.88f,.90f));
            modalShade=Texture(new Color(0,0,0,classic?.70f:.28f));
            controlTexture=ThemeTexture(classic?new Color(.19f,.21f,.24f):new Color(.94f,.94f,.956f));
            hoverTexture=ThemeTexture(classic?new Color(.25f,.28f,.31f):new Color(.90f,.93f,.98f));
            selectedTexture=ThemeTexture(classic?new Color(.27f,.25f,.20f):new Color(.87f,.925f,1));
            fieldTexture=ThemeTexture(classic?new Color(.095f,.11f,.13f):new Color(.955f,.955f,.968f));
            primaryTexture=ThemeTexture(accent);
            surfaceStyle=new GUIStyle{border=ThemeBorder()};surfaceStyle.normal.background=background;
            sidebarStyle=new GUIStyle{border=ThemeBorder()};sidebarStyle.normal.background=sidebarBackground;
            titleStyle=Style(28,text);titleStyle.fontStyle=FontStyle.Bold;
            labelStyle=Style(20,text);labelStyle.wordWrap=true;
            smallStyle=Style(20,secondary);smallStyle.wordWrap=true;
            subStyle=Style(20,accent);subStyle.wordWrap=true;
            columnStyle=Style(20,secondary);sectionStyle=Style(22,classic?accent:text);sectionStyle.fontStyle=FontStyle.Bold;sectionStyle.wordWrap=true;
            buttonStyle=Style(20,text,TextAnchor.MiddleCenter);buttonStyle.padding=new RectOffset(12,12,6,6);buttonStyle.border=ThemeBorder();
            buttonStyle.normal.background=controlTexture;buttonStyle.hover.background=hoverTexture;buttonStyle.active.background=selectedTexture;
            foreach(var state in new[]{buttonStyle.onNormal,buttonStyle.onHover,buttonStyle.onActive,buttonStyle.onFocused}){state.background=selectedTexture;state.textColor=accent;}
            numberArrowStyle=new GUIStyle(buttonStyle);numberArrowStyle.padding=new RectOffset(2,2,6,6);
            activeStyle=new GUIStyle(buttonStyle);activeStyle.normal.background=selectedTexture;activeStyle.normal.textColor=accent;
            primaryStyle=new GUIStyle(buttonStyle);primaryStyle.fontStyle=FontStyle.Bold;
            foreach(var state in new[]{primaryStyle.normal,primaryStyle.hover,primaryStyle.active,primaryStyle.focused,primaryStyle.onNormal,primaryStyle.onHover,primaryStyle.onActive,primaryStyle.onFocused})
            {state.background=primaryTexture;state.textColor=classic?new Color(.125f,.14f,.16f):Color.white;}
            inputStyle=Style(22,text);inputStyle.padding=new RectOffset(14,12,6,6);inputStyle.border=ThemeBorder();
            inputStyle.normal.background=fieldTexture;inputStyle.hover.background=controlTexture;inputStyle.focused.background=classic?controlTexture:selectedTexture;
            modifiedInputStyle=new GUIStyle(inputStyle);modifiedInputStyle.normal.textColor=StateColor(FieldVisualState.Saved);modifiedInputStyle.fontStyle=FontStyle.Bold;
            invalidInputStyle=new GUIStyle(inputStyle);invalidInputStyle.normal.textColor=StateColor(FieldVisualState.Error);invalidInputStyle.focused.textColor=invalidInputStyle.normal.textColor;
            previewStyle=new GUIStyle(inputStyle);previewStyle.normal.textColor=secondary;
            valueLabelStyle=new GUIStyle(labelStyle);
            checkStyle=Style(20,accent,TextAnchor.MiddleCenter);checkStyle.border=ThemeBorder();checkStyle.padding=new RectOffset();
            checkStyle.normal.background=controlTexture;checkStyle.hover.background=hoverTexture;
            foreach(var state in new[]{checkStyle.onNormal,checkStyle.onHover,checkStyle.onActive,checkStyle.onFocused}){state.background=selectedTexture;state.textColor=accent;}
            listStyle=Style(20,text);listStyle.wordWrap=true;listStyle.padding=new RectOffset(7,7,2,2);listStyle.border=ThemeBorder();listStyle.hover.background=hoverTexture;
            selectedListStyle=new GUIStyle(listStyle);selectedListStyle.normal.textColor=StateColor(FieldVisualState.Saved);selectedListStyle.fontStyle=FontStyle.Bold;
            groupStyle=Style(20,secondary);groupStyle.padding=new RectOffset(6,6,4,4);
            stateBadgeStyle=Style(20,accent,TextAnchor.MiddleCenter);
            neutralBoolStyle=new GUIStyle(buttonStyle);
            foreach(var state in new[]{neutralBoolStyle.onNormal,neutralBoolStyle.onHover,neutralBoolStyle.onActive,neutralBoolStyle.onFocused}){state.background=controlTexture;state.textColor=text;}
            editorSkin=Instantiate(GUI.skin);editorSkin.hideFlags=HideFlags.HideAndDontSave;editorSkin.font=chineseFont;
            editorSkin.verticalScrollbar.fixedWidth=classic?18:14;editorSkin.verticalScrollbar.margin=new RectOffset(8,0,0,0);
            editorSkin.verticalScrollbar.normal.background=classic?sidebarBackground:null;
            editorSkin.verticalScrollbarThumb.normal.background=classic?hoverTexture:controlTexture;editorSkin.verticalScrollbarThumb.hover.background=hoverTexture;
            editorSkin.verticalScrollbarThumb.active.background=selectedTexture;editorSkin.verticalScrollbarThumb.fixedWidth=classic?18:14;
            editorSkin.verticalScrollbarThumb.fixedHeight=0;editorSkin.verticalScrollbarThumb.padding=new RectOffset();
            editorSkin.verticalScrollbarThumb.border=classic?new RectOffset():new RectOffset(12,12,12,12);
        }
        void OnGUI()
        {
            if(!Visible)return;
            EnsureStyles();
            var previousMatrix=GUI.matrix;var previousSkin=GUI.skin;bool previousEnabled=GUI.enabled;
            var previousColor=GUI.color;var previousBackground=GUI.backgroundColor;var previousContent=GUI.contentColor;
            PrepareWindow();float scale=uiScale;
            string visibleError=!string.IsNullOrEmpty(error)?error:fileError?"配置错误":"";
            float leftWidth=hideNavigation?0:ClassicUI?300:304,gap=hideNavigation?0:ClassicUI?18:28,padding=ClassicUI?20:28;
            var panel=editorRect;float width=panel.width,height=panel.height,x=panel.x,y=panel.y;
            var left=new Rect(x+padding,y+154,leftWidth,height-194);
            var right=new Rect(left.xMax+gap,y+152,width-padding*2-leftWidth-gap,height-246-(string.IsNullOrEmpty(visibleError)?0:60));
            var actions=new Rect(right.x,y+height-78,right.width,46);
            try
            {
                GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));GUI.skin=editorSkin;GUI.depth=-10000;
                GUI.color=Color.white;GUI.backgroundColor=Color.white;GUI.contentColor=Color.white;
                HandleWindow();HandleEditorKeys();inputOrder.Clear();
                if(clearInputFocus){GUI.FocusControl(null);clearInputFocus=false;}
                GUI.DrawTexture(new Rect(0,0,Screen.width/scale,Screen.height/scale),modalShade);GUI.Box(panel,GUIContent.none,surfaceStyle);
                bool modal=showSettings||!string.IsNullOrEmpty(confirm);GUI.enabled=previousEnabled&&!modal&&!sizingByClick;
                GUI.Label(new Rect(x+padding,y+22,width-768,42),"数值编辑器",titleStyle);
                if(GUI.Button(new Rect(panel.xMax-714,y+22,88,42),"设置",buttonStyle)){showSettings=true;GUI.FocusControl(null);}
                if(GUI.Button(new Rect(panel.xMax-270,y+22,138,42),Settings.Enabled?"修改已启用":"修改已停用",Settings.Enabled?activeStyle:buttonStyle))Guard(ToggleOverrides);
                if(GUI.Button(new Rect(panel.xMax-116,y+22,88,42),"关闭",buttonStyle))Toggle();
                DrawWindowButtons(panel);
                if(Ready)DrawModules(new Rect(x+padding,y+88,width-padding*2,44));
                if(Ready)
                {
                    if(!hideNavigation){GUI.Box(left,GUIContent.none,sidebarStyle);GUILayout.BeginArea(new Rect(left.x+10,left.y+10,left.width-20,left.height-20));DrawUnits(left.height-20,left.width-20);GUILayout.EndArea();}
                    GUILayout.BeginArea(right);DrawDetails(right.height,right.width);GUILayout.EndArea();
                    GUILayout.BeginArea(actions);DrawActions();GUILayout.EndArea();
                }
                else GUI.Label(right,Status.StartsWith("目录加载失败")?"加载失败":"正在加载",sectionStyle);
                if(!string.IsNullOrEmpty(visibleError))GUI.Label(new Rect(right.x,actions.y-62,right.width,54),visibleError,labelStyle);
                GUI.enabled=previousEnabled;if(showSettings)DrawEditorSettings(panel);else if(!string.IsNullOrEmpty(confirm))DrawConfirm(panel);
                focusedInput=GUI.GetNameOfFocusedControl()??"";
                if(Event.current.type==EventType.MouseDown||Event.current.type==EventType.MouseUp||Event.current.type==EventType.ScrollWheel)Event.current.Use();
            }
            finally{GUI.enabled=previousEnabled;GUI.skin=previousSkin;GUI.matrix=previousMatrix;GUI.color=previousColor;GUI.backgroundColor=previousBackground;GUI.contentColor=previousContent;}
        }
        void HandleEditorKeys()
        {
            var ev=Event.current;
            if(ev.type!=EventType.KeyDown||Composing||showSettings||!string.IsNullOrEmpty(confirm))return;
            if((ev.control||ev.command)&&ev.keyCode==KeyCode.S){Guard(SaveDraft);ev.Use();}
            else if(ev.keyCode==KeyCode.Return||ev.keyCode==KeyCode.KeypadEnter){GUI.FocusControl(null);ev.Use();}
            else if(ev.keyCode==KeyCode.Tab&&inputOrder.Count>0)
            {int index=inputOrder.IndexOf(GUI.GetNameOfFocusedControl());index=(index+(ev.shift?-1:1)+inputOrder.Count)%inputOrder.Count;GUI.FocusControl(inputOrder[index]);ev.Use();}
        }

        public void Select(UnitEntry entry)
        {
            selected=entry;draft=entry!=null&&Settings.Units.TryGetValue(entry.Key,out var rule)?JsonConvert.DeserializeObject<UnitRule>(JsonConvert.SerializeObject(rule)):new UnitRule();
            dirty=false;error="";priceInput=null;fieldErrors.Clear();pendingTextCommit.Clear();valuesScroll=Vector2.zero;GUI.FocusControl(null);
            if(ComponentTab)LoadSelectedGraph();
        }
        void LoadSelectedGraph(){if(selected!=null&&selected.Slots==null)Guard(()=>EnsureGraph(selected));}
        void DrawDetails(float height,float width)
        {
            if(selected==null)Select(ModuleEntries().FirstOrDefault());
            GUILayout.BeginHorizontal(GUILayout.Height(44));
            GUILayout.Label(selected==null?SourceName(moduleFilter):Labels.UnitName(selected),titleStyle,GUILayout.ExpandWidth(true),GUILayout.Height(44));
            Settings.Units.TryGetValue(selected?.Key??"",out var currentRule);
            string state=fieldErrors.Count>0?"待修正":dirty?"未保存":OverrideCount(currentRule)>0?"已修改":"";
            GUILayout.Label(state,subStyle,GUILayout.Width(100),GUILayout.Height(44));GUILayout.EndHorizontal();
            string parentRemark=RemarkText(selected?.ParentText);
            float parentHeight=parentRemark.Length>0?Mathf.Max(36,smallStyle.CalcHeight(new GUIContent(parentRemark),width)):0;
            if(parentHeight>0)GUILayout.Label(parentRemark,smallStyle,GUILayout.Height(parentHeight));
            GUILayout.Space(12);int next=GUILayout.Toolbar(tab,tabs,buttonStyle,GUILayout.Height(42));
            if(next!=tab){tab=next;valuesScroll=Vector2.zero;paramSearch="";if(ComponentTab)LoadSelectedGraph();if(tab==LibraryTab)Guard(RefreshLibrary);}
            GUILayout.Space(18);bool filtered=ComponentTab;
            if(filtered)
            {
                GUI.SetNextControlName("parameter-search");paramSearch=GUILayout.TextField(paramSearch,inputStyle,GUILayout.Height(40));
                if(string.IsNullOrEmpty(paramSearch)&&Event.current.type==EventType.Repaint){var rect=GUILayoutUtility.GetLastRect();rect.x+=14;GUI.Label(rect,"搜索",columnStyle);}
                GUILayout.Space(12);
            }
            bool columns=tab<=AbilitiesTab;
            if(columns)DrawColumns();parameterFolds.SetQuery(paramSearch);rowLabelWidth=Mathf.Max(120,width-574);
            valuesScroll=GUILayout.BeginScrollView(valuesScroll,false,true,GUILayout.Height(Mathf.Max(100,height-128-parentHeight-(filtered?52:0)-(columns?34:0))));
            if(tab==LibraryTab)DrawLibrary();else if(selected==null)GUILayout.Label("未安装",sectionStyle);
            else if(tab==StatsTab)DrawStats();else if(tab==ResistanceTab)DrawResistances();else if(tab==BatchTab)DrawBatch();else if(tab==SummaryTab)DrawSummary();else if(tab==AbilitiesTab)DrawAbilities();else DrawComponents();
            GUILayout.EndScrollView();
        }
        void DrawActions()
        {
            if(selected==null)return;
            GUILayout.BeginHorizontal();bool enabled=GUI.enabled;
            if(GUILayout.Button("还原",buttonStyle,GUILayout.Width(96),GUILayout.Height(44)))confirm="unit";
            GUILayout.FlexibleSpace();GUI.enabled=enabled&&dirty;
            if(GUILayout.Button("撤回",buttonStyle,GUILayout.Width(96),GUILayout.Height(44)))Select(selected);
            GUILayout.Space(12);GUI.enabled=enabled&&dirty&&!Composing;
            if(GUILayout.Button(dirty?"保存":"已保存",primaryStyle,GUILayout.Width(130),GUILayout.Height(44)))Guard(SaveDraft);
            GUI.enabled=enabled;GUILayout.EndHorizontal();
        }
        void DrawColumns()
        {
            Rect r=GUILayoutUtility.GetRect(1,34,GUILayout.ExpandWidth(true));r.width-=22;
            GUI.Label(new Rect(r.x+10,r.y,r.width-552,34),"参数",columnStyle);
            GUI.Label(new Rect(r.xMax-532,r.y,100,34),"原值",columnStyle);
            GUI.Label(new Rect(r.xMax-324,r.y,132,34),"设置",columnStyle);
        }
        void DrawStats()
        {
            Settings.Units.TryGetValue(selected.Key,out var savedRule);
            bool on=draft.Price.HasValue,was=on;string value=priceInput??draft.Price?.ToString()??selected.OriginalPrice.ToString(),old=value;
            DrawFieldRow("price",ref on,ref value,"价格",selected.OriginalPrice.ToString(),typeof(int),"恢复基准会取消此项覆盖；保存后生效",savedRule?.Price.HasValue==true,savedRule?.Price?.ToString());
            if(on!=was||value!=old){draft.Price=on?(draft.Price??(int)selected.OriginalPrice):(int?)null;priceInput=on?value:null;RecheckDirty();}
            foreach(var group in Labels.Stats.GroupBy(d=>EditPresentation.BasicCategory(d.Field)))
            {
                int modified=group.Count(d=>savedRule?.Stats.ContainsKey(d.Field)==true);
                int pending=group.Count(d=>{var state=ValueState(draft.Stats,d.Field);return state==FieldVisualState.Draft||state==FieldVisualState.Restore;});
                bool initial=group.Key=="生命与伤害"||group.Key=="攻击与移动"||modified>0||pending>0;
                if(!DrawFold("stats/"+group.Key,group.Key,group.Count(),modified,pending,initial))continue;
                foreach(var def in group)DrawValue(draft.Stats,def.Field,def.Label,Numbers.Text(EffectiveStat(selected,def.Field)),AccessTools.Field(typeof(Landfall.TABS.UnitBlueprint),def.Field).FieldType,def.Note);
            }
        }
        void RecheckDirty()
        {
            Settings.Units.TryGetValue(selected.Key,out var saved);
            dirty=!EditData.Same(draft,saved??new UnitRule()) || (draft.Price.HasValue&&priceInput!=null&&priceInput!=draft.Price.Value.ToString());
        }
        static string DisplayValue(string value,Type type)=>type==typeof(bool)?(value=="true"?"开启":"关闭"):value=="Infinity"?"无限":value;
        void DrawFieldRow(string key,ref bool on,ref string value,string label,string original,Type type,string note,bool savedOn,string savedValue)
        {
            string control="value/"+key;inputOrder.Add(control);
            bool focused=GUI.GetNameOfFocusedControl()==control;string issue="";
            if(on&&!focused&&!Composing)
            {
                try{var parsed=Numbers.Parse(value,type);if(pendingTextCommit.Remove(key))value=Numbers.Text(parsed);issue=FieldIssue(key,value,type);}
                catch(Exception e){issue=e.Message;}
            }
            if(!string.IsNullOrEmpty(issue))fieldErrors[key]=issue;else fieldErrors.Remove(key);
            var state=EditPresentation.FieldState(on,value,savedOn,savedValue,issue.Length>0);
            var content=new GUIContent(label);float baseHeight=Mathf.Max(58,valueLabelStyle.CalcHeight(content,rowLabelWidth)+16);
            Rect r=GUILayoutUtility.GetRect(1,baseHeight+(issue.Length>0?34:0),GUILayout.ExpandWidth(true));
            if(state!=FieldVisualState.Default){Fill(r,StateBackground(state));Fill(new Rect(r.x,r.y+12,3,baseHeight-24),StateColor(state));}
            GUI.Label(new Rect(r.x+12,r.y+4,r.width-552,baseHeight-8),content,labelStyle);
            GUI.Label(new Rect(r.xMax-532,r.y+4,100,baseHeight-8),DisplayValue(original,type),columnStyle);
            var input=new Rect(r.xMax-420,r.y+(baseHeight-42)/2,324,42);
            if(focused)Input.compositionCursorPos=GUIUtility.GUIToScreenPoint(new Vector2(input.x,input.yMax));
            string old=value;
            if(type==typeof(bool))
            {GUI.SetNextControlName(control);bool b=value=="true";bool next=GUI.Toggle(new Rect(input.x+96,input.y,132,input.height),b,b?"开启":"关闭",neutralBoolStyle);value=next?"true":"false";}
            else
            {
                string display=!on&&!focused&&value=="Infinity"?"无限":value;
                string entered=DrawNumberInput(input,control,key,display,type,issue.Length>0?invalidInputStyle:on?modifiedInputStyle:inputStyle);
                if(entered!=display)value=entered;
            }
            if(value!=old){on=true;pendingTextCommit.Add(key);}
            bool enabled=GUI.enabled;GUI.enabled=enabled&&on;
            if(GUI.Button(new Rect(r.xMax-84,r.y+(baseHeight-42)/2,82,42),"还原",listStyle))
            {on=false;value=original;fieldErrors.Remove(key);GUI.FocusControl(null);}
            GUI.enabled=enabled;
            if(issue.Length>0)GUI.Label(new Rect(r.x+12,r.y+baseHeight,r.width-16,34),issue,labelStyle);
            GUI.DrawTexture(new Rect(r.x+12,r.yMax-1,r.width-24,1),lineTexture);
        }
        string FieldIssue(string key,string value,Type type)
        {
            if(type==typeof(bool))return "";
            double number=Convert.ToDouble(Numbers.Parse(value,type));
            if(key=="price")return number<0?"价格不能为负数":"";
            if(IsResistanceKey(key)&&number<0)return "承伤倍率不能为负数";
            var def=Labels.Stats.FirstOrDefault(d=>d.Field==key);
            if(def!=null)return number<def.Min?def.Label+"不能小于 "+def.Min:"";
            var slot=selected?.Slots?.FirstOrDefault(v=>v.Key==key);
            if(slot?.ComponentType==typeof(DamageMultiplier)&&number<0)return "承伤倍率不能为负数";
            if(slot!=null&&slot.Label.Contains("冷却")&&number<0)return "冷却不能为负数";
            if(slot!=null&&slot.FieldName=="magSize"&&number<1)return "弹匣容量至少为 1";
            if(key.EndsWith("/@count")&&(number<0||number>128))return "召唤数量需在 0–128 之间";
            if(selected?.Constants?.Any(v=>v.Key==key&&v.Readable)==true&&number<0)return "技能计时不能为负数";
            return "";
        }
        void DrawValue(Dictionary<string,string> values,string key,string label,string original,Type type,string note="")
        {
            bool on=values.TryGetValue(key,out var value);if(!on)value=original;bool oldOn=on;string old=value;
            var saved=SavedValues(values);string previous=null;bool savedOn=saved!=null&&saved.TryGetValue(key,out previous);
            DrawFieldRow(key,ref on,ref value,label,original,type,note,savedOn,previous);
            if(on!=oldOn||value!=old){if(on)values[key]=value;else values.Remove(key);RecheckDirty();}
        }
        bool Matches(string text)=>string.IsNullOrWhiteSpace(paramSearch)||text.IndexOf(paramSearch,StringComparison.OrdinalIgnoreCase)>=0;
        static string RemarkText(string text)=>string.IsNullOrEmpty(text)?"":"（"+text+"）";
        void DrawRemark(string text)=>GUILayout.Label(RemarkText(text),smallStyle);

        void Guard(Action action){try{action();error="";}catch(Exception ex){error=ex.Message;Logger.LogError(ex);}}
    }
}
