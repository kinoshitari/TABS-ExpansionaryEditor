using BepInEx.Configuration;
using UnityEngine;

namespace ExpansionaryEditor
{
    public enum EditorTheme { Light,Classic }

    public sealed partial class EditorPlugin
    {
        ConfigEntry<EditorTheme> windowTheme;
        bool showSettings,refreshTheme;
        GUIStyle numberArrowStyle;
        bool ClassicUI=>windowTheme!=null&&windowTheme.Value==EditorTheme.Classic;
        Texture2D ThemeTexture(Color color)=>ClassicUI?Texture(color):RoundedTexture(color);
        RectOffset ThemeBorder()=>ClassicUI?new RectOffset():new RectOffset(16,16,16,16);

        void SetTheme(EditorTheme theme)
        {
            if(windowTheme.Value==theme)return;
            windowTheme.Value=theme;refreshTheme=true;Config.Save();
        }

        void DrawEditorSettings(Rect panel)
        {
            GUI.DrawTexture(panel,modalShade);
            float width=Mathf.Min(560,panel.width-64),height=272;
            var dialog=new Rect(panel.center.x-width/2,panel.center.y-height/2,width,height);
            GUI.Box(dialog,GUIContent.none,surfaceStyle);
            GUI.Label(new Rect(dialog.x+32,dialog.y+22,width-64,44),"设置",titleStyle);
            GUI.Label(new Rect(dialog.x+32,dialog.y+76,width-64,34),"界面风格",columnStyle);
            float choice=(width-76)/2;
            if(GUI.Button(new Rect(dialog.x+32,dialog.y+116,choice,48),"浅色",ClassicUI?buttonStyle:activeStyle))
                Guard(()=>SetTheme(EditorTheme.Light));
            if(GUI.Button(new Rect(dialog.x+44+choice,dialog.y+116,choice,48),"经典",ClassicUI?activeStyle:buttonStyle))
                Guard(()=>SetTheme(EditorTheme.Classic));
            if(GUI.Button(new Rect(dialog.xMax-150,dialog.yMax-76,118,44),"完成",primaryStyle))showSettings=false;
        }
    }
}
