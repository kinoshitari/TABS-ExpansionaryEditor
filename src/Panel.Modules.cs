using System.Linq;
using UnityEngine;

namespace ExpansionaryEditor
{
    public sealed partial class EditorPlugin
    {
        readonly string[] moduleIds={"Vanilla","HiddenUnits","ChouChou","Expansionary","Holiday","AnimalKingdom","ChineseMyth"};
        string moduleFilter="Expansionary",pendingModule;
        System.Collections.Generic.IEnumerable<UnitEntry> ModuleEntries()=>Entries.Where(e=>e.Module==moduleFilter);
        void DrawModules(Rect rect)
        {
            float width=(rect.width-118-(moduleIds.Length-1)*4)/moduleIds.Length;
            for(int i=0;i<moduleIds.Length;i++)
            {
                string id=moduleIds[i],label=SourceName(id);
                if(GUI.Button(new Rect(rect.x+i*(width+4),rect.y,width,44),label,moduleFilter==id?activeStyle:buttonStyle)&&moduleFilter!=id)
                {if(dirty){pendingModule=id;confirm="module";}else SwitchModule(id);}
            }
            if(GUI.Button(new Rect(rect.xMax-110,rect.y,110,44),hideNavigation?"目录":"收起",buttonStyle))hideNavigation=!hideNavigation;
        }
        void SwitchModule(string id)
        {
            moduleViews[moduleFilter]=new ModuleView{Search=search,Selected=selected?.Key,Filter=unitFilter,Scroll=unitsScroll};
            moduleFilter=id;paramSearch="";batch.Clear();InvalidateBatch();
            if(moduleViews.TryGetValue(id,out var view))
            {search=view.Search;unitFilter=view.Filter==2?0:view.Filter;unitsScroll=view.Scroll;Select(ModuleEntries().FirstOrDefault(e=>e.Key==view.Selected)??ModuleEntries().FirstOrDefault());}
            else {search="";unitFilter=0;unitsScroll=Vector2.zero;Select(ModuleEntries().FirstOrDefault());}
        }
    }
}
