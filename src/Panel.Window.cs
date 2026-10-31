using BepInEx.Configuration;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        ConfigEntry<float> windowX,windowY,windowWidth,windowHeight,windowZoom;
        ConfigEntry<int> windowScaleVersion;
        bool migrateWindowScale;
        Rect editorRect;
        float uiZoom=1,uiScale=1;
        int lastScreenWidth,lastScreenHeight,windowDragControl;
        bool resizing;
        bool sizingByClick;
        Vector2 dragStart;
        Rect dragRect;
        const float MinWidth=1120,MinHeight=720;

        void InitWindowSettings()
        {
            windowX=Config.Bind("界面","横向位置",.5f,"窗口在可用屏幕内的相对位置");
            windowY=Config.Bind("界面","纵向位置",.5f,"窗口在可用屏幕内的相对位置");
            windowWidth=Config.Bind("界面","窗口宽度",1240f);
            windowHeight=Config.Bind("界面","窗口高度",800f);
            windowZoom=Config.Bind("界面","界面缩放",1f,"实际像素缩放；1 表示 100%。屏幕放不下时临时缩小，不改已记住的比例。");
            windowScaleVersion=Config.Bind("界面","缩放规则版本",0,"内部迁移标记，请勿手动修改。");
            migrateWindowScale=windowScaleVersion.Value<1;
            uiZoom=Mathf.Clamp(windowZoom.Value,.65f,migrateWindowScale?1.4f:2f);
        }
        float FitScale()=>Mathf.Min((Screen.width-24)/MinWidth,(Screen.height-24)/MinHeight);
        float RecommendedScale()=>Mathf.Min(Screen.width/1320f,Screen.height/860f,1.5f);
        void PrepareWindow()
        {
            if(migrateWindowScale)
            {
                // Preserve the old appearance once, then remember an absolute scale.
                uiZoom=Mathf.Clamp(Mathf.Min(RecommendedScale()*uiZoom,FitScale()),.65f,2f);
                windowZoom.Value=uiZoom;windowScaleVersion.Value=1;Config.Save();migrateWindowScale=false;
            }
            uiScale=Mathf.Min(uiZoom,FitScale());
            float sw=Screen.width/uiScale,sh=Screen.height/uiScale;
            if(lastScreenWidth!=Screen.width||lastScreenHeight!=Screen.height)
            {
                editorRect=new Rect(0,0,Mathf.Clamp(windowWidth.Value,MinWidth,sw-16),Mathf.Clamp(windowHeight.Value,MinHeight,sh-16));
                editorRect.x=8+(sw-16-editorRect.width)*Mathf.Clamp01(windowX.Value);
                editorRect.y=8+(sh-16-editorRect.height)*Mathf.Clamp01(windowY.Value);
                lastScreenWidth=Screen.width;lastScreenHeight=Screen.height;
            }
            ClampWindow(sw,sh);
        }
        void ClampWindow(float sw,float sh)
        {
            editorRect.width=Mathf.Clamp(editorRect.width,MinWidth,sw-16);
            editorRect.height=Mathf.Clamp(editorRect.height,MinHeight,sh-16);
            editorRect.x=Mathf.Clamp(editorRect.x,8,sw-editorRect.width-8);
            editorRect.y=Mathf.Clamp(editorRect.y,8,sh-editorRect.height-8);
        }
        void SaveWindow()
        {
            float sw=Screen.width/uiScale,sh=Screen.height/uiScale;
            windowX.Value=(editorRect.x-8)/Mathf.Max(1,sw-16-editorRect.width);
            windowY.Value=(editorRect.y-8)/Mathf.Max(1,sh-16-editorRect.height);
            windowWidth.Value=editorRect.width;windowHeight.Value=editorRect.height;windowZoom.Value=uiZoom;
            Config.Save();
        }
        void ZoomWindow(float amount)
        {
            var center=editorRect.center*uiScale;
            uiZoom=Mathf.Clamp(uiScale+amount,.65f,2f);PrepareWindow();
            editorRect.center=center/uiScale;ClampWindow(Screen.width/uiScale,Screen.height/uiScale);SaveWindow();
        }
        void FitWindow()
        {
            uiZoom=Mathf.Clamp(RecommendedScale(),.65f,2f);windowX.Value=.5f;windowY.Value=.5f;windowWidth.Value=1240;windowHeight.Value=800;
            lastScreenWidth=0;PrepareWindow();SaveWindow();
        }
        void HandleWindow()
        {
            int id=GUIUtility.GetControlID("ExpansionaryEditor.Window".GetHashCode(),FocusType.Passive);
            var ev=Event.current;var grip=new Rect(editorRect.xMax-28,editorRect.yMax-28,28,28);
            var header=new Rect(editorRect.x,editorRect.y,editorRect.width-680,80);
            if(!string.IsNullOrEmpty(confirm))return;
            if(sizingByClick)
            {
                editorRect.width=ev.mousePosition.x-editorRect.x+12;editorRect.height=ev.mousePosition.y-editorRect.y+12;
                ClampWindow(Screen.width/uiScale,Screen.height/uiScale);
                if(ev.type==EventType.MouseDown){sizingByClick=false;SaveWindow();ev.Use();}
                return;
            }
            if(ev.type==EventType.MouseDown&&ev.button==0&&(grip.Contains(ev.mousePosition)||header.Contains(ev.mousePosition)))
            {
                windowDragControl=id;GUIUtility.hotControl=id;resizing=grip.Contains(ev.mousePosition);
                dragStart=ev.mousePosition;dragRect=editorRect;GUI.FocusControl(null);ev.Use();
            }
            else if(GUIUtility.hotControl==id&&windowDragControl==id)
            {
                if(ev.type==EventType.MouseDrag)
                {
                    Vector2 delta=ev.mousePosition-dragStart;
                    if(resizing){editorRect.width=dragRect.width+delta.x;editorRect.height=dragRect.height+delta.y;}
                    else editorRect.position=dragRect.position+delta;
                    ClampWindow(Screen.width/uiScale,Screen.height/uiScale);ev.Use();
                }
                else if(ev.type==EventType.MouseUp){GUIUtility.hotControl=0;windowDragControl=0;sizingByClick=resizing&&(ev.mousePosition-dragStart).sqrMagnitude<9;SaveWindow();ev.Use();}
            }
        }
        void DrawWindowButtons(Rect panel)
        {
            float x=panel.xMax-610,y=panel.y+22;bool enabled=GUI.enabled;
            GUI.enabled=enabled&&uiScale>.655f;
            if(GUI.Button(new Rect(x,y,40,42),"−",buttonStyle))ZoomWindow(-.1f);
            GUI.enabled=enabled;GUI.Label(new Rect(x+48,y,76,42),Mathf.RoundToInt(uiScale*100)+"%",columnStyle);
            GUI.enabled=enabled&&uiScale<Mathf.Min(2f,FitScale())-.005f;
            if(GUI.Button(new Rect(x+128,y,40,42),"+",buttonStyle))ZoomWindow(.1f);
            GUI.enabled=enabled;
            if(GUI.Button(new Rect(x+180,y,116,42),"适应窗口",buttonStyle))FitWindow();
            GUI.Label(new Rect(panel.xMax-25,panel.yMax-28,22,24),"⌟",subStyle);
        }
    }
}
