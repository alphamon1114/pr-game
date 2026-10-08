using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PrGame
{
    public static class DesktopBrowserDefaults
    {
        public const string HomeUrl = "https://www.naver.com/";
        public const string VarcoUrl = "https://3d.varco.ai/explore";
    }

    [Serializable]
    public sealed class DesktopFile
    {
        public string id, name, parent, content = "";
        public bool folder, deleted;
        public long modified;
    }

    [Serializable]
    public sealed class DesktopSave
    {
        public int version = 1;
        public string theme = "silver";
        public bool doNotDisturb;
        public int browserDefaultsVersion;
        public List<DesktopFile> files = new List<DesktopFile>();
        public List<string> history = new List<string>();
        public List<string> bookmarks = new List<string>();
    }

    // Virtual paths never resolve to the host computer's files.
    public sealed class DesktopFileSystem
    {
        public static string OverrideDirectory { get; set; }
        public DesktopSave Data { get; private set; }
        public string LastError { get; private set; }
        public event Action Changed;
        public bool Dirty { get; private set; }
        readonly string savePath;

        public DesktopFileSystem(string directory = null)
        {
            savePath = Path.Combine(directory ?? OverrideDirectory ?? Path.Combine(Application.persistentDataPath, "Desktop"), "session-v1.json");
            Data = Read(savePath) ?? Read(savePath + ".bak") ?? Seed();
            // Apply once to existing saves too, without restoring a bookmark the player later removes.
            if (Data.browserDefaultsVersion < 1)
            {
                if (!Data.bookmarks.Any(url => url?.TrimEnd('/') == DesktopBrowserDefaults.VarcoUrl))
                    Data.bookmarks.Add(DesktopBrowserDefaults.VarcoUrl);
                Data.browserDefaultsVersion = 1;
                Dirty = true;
            }
        }

        DesktopSave Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<DesktopSave>(File.ReadAllText(path));
                if (data == null || data.version != 1 || data.files == null || !data.files.Any(f => f.id == "documents"))
                    throw new InvalidDataException("Invalid desktop save");
                data.history = data.history ?? new List<string>();
                data.bookmarks = data.bookmarks ?? new List<string>();
                return data;
            }
            catch (Exception e) { LastError = "저장 파일을 불러오지 못했어요. " + e.Message; return null; }
        }

        static DesktopSave Seed()
        {
            var data = new DesktopSave();
            foreach (var entry in new[] { new[]{"desktop","바탕화면"}, new[]{"documents","문서"}, new[]{"downloads","다운로드"}, new[]{"pictures","사진"} })
                data.files.Add(new DesktopFile { id=entry[0], name=entry[1], parent="root", folder=true });
            foreach (var entry in new[] { new[]{"work","작업"}, new[]{"archive","보관함"}, new[]{"screenshots","스크린샷"} })
                data.files.Add(new DesktopFile { id=entry[0], name=entry[1], parent="documents", folder=true });
            data.files.Add(new DesktopFile { id="welcome-note", name="메모.txt", parent="documents", content="필요한 내용을 자유롭게 적어 두세요.", modified=DateTime.UtcNow.Ticks });
            data.bookmarks.AddRange(new[]{"local://documents", "local://archive"});
            return data;
        }

        public DesktopFile Get(string id) => Data.files.FirstOrDefault(f => f.id == id);
        public bool IsDeleted(DesktopFile file)
        {
            var current = file;
            for (int i=0; current != null && i<Data.files.Count; i++)
            { if (current.deleted) return true; current=Get(current.parent); }
            return false;
        }
        public IEnumerable<DesktopFile> Children(string parent) => Data.files.Where(f => f.parent == parent && !IsDeleted(f)).OrderByDescending(f=>f.folder).ThenBy(f=>f.name);
        public IEnumerable<DesktopFile> Search(string query) => Data.files.Where(f=>!IsDeleted(f) && f.name.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(f=>f.modified);
        public IEnumerable<DesktopFile> Notes => Data.files.Where(f=>!f.folder && !IsDeleted(f) && f.name.EndsWith(".txt",StringComparison.OrdinalIgnoreCase)).OrderByDescending(f=>f.modified);
        public string PathOf(DesktopFile file)
        {
            var parts=new List<string>(); var parent=Get(file.parent);
            for(int i=0; parent!=null && i<Data.files.Count; i++) { parts.Insert(0,parent.name);parent=Get(parent.parent); }
            return "내 컴퓨터"+(parts.Count>0 ? " › "+string.Join(" › ",parts) : "");
        }

        public DesktopFile Create(string parent, string name, bool folder=false)
        {
            if (Get(parent)?.folder != true || IsDeleted(Get(parent))) throw new InvalidOperationException("폴더를 찾을 수 없어요.");
            name=CleanName(name);
            string stem=folder ? name : Path.GetFileNameWithoutExtension(name), extension=folder ? "" : Path.GetExtension(name);
            for(int i=2; Children(parent).Any(f=>string.Equals(f.name,name,StringComparison.OrdinalIgnoreCase));i++) name=stem+" "+i+extension;
            var file=new DesktopFile { id=Guid.NewGuid().ToString("N"), name=name, parent=parent, folder=folder, modified=DateTime.UtcNow.Ticks };
            Data.files.Add(file);Touch();return file;
        }
        static string CleanName(string name)
        {
            name=(name??"").Trim();
            if(name.Length==0 || name=="." || name==".." || name.IndexOfAny(new[]{'/', '\\', ':', '*', '?', '"', '<', '>', '|','\n','\r'})>=0)
                throw new ArgumentException("사용할 수 없는 파일 이름이에요.");
            if(name.Length>100) throw new ArgumentException("이름은 100자 이내로 입력해 주세요.");
            return name;
        }
        public void Rename(string id,string name)
        {
            var file=Get(id); if(file==null || file.parent=="root")return;
            name=CleanName(name);
            if(Data.files.Any(f=>f.id!=id && f.parent==file.parent && !IsDeleted(f) && string.Equals(f.name,name,StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("같은 이름의 파일이 있어요.");
            file.name=name;file.modified=DateTime.UtcNow.Ticks;Touch();
        }
        public void Write(string id,string content)
        {
            var file=Get(id);if(file==null || file.folder || IsDeleted(file))return;
            file.content=content;file.modified=DateTime.UtcNow.Ticks;Touch();
        }
        public void Delete(string id) { var f=Get(id);if(f==null || f.parent=="root")return;f.deleted=true;Touch(); }
        public void Restore(string id)
        {
            var f=Get(id);if(f==null)return;
            if(Get(f.parent)==null || IsDeleted(Get(f.parent))) f.parent="documents";
            var peers=Children(f.parent).ToArray();string initial=f.name;int suffix=2;
            while(peers.Any(p=>p.id!=id && string.Equals(p.name,f.name,StringComparison.OrdinalIgnoreCase)))
                f.name=Path.GetFileNameWithoutExtension(initial)+" "+(suffix++)+Path.GetExtension(initial);
            f.deleted=false;Touch();
        }
        public DesktopFile Paste(string id,string parent,bool move)
        {
            var source=Get(id);var target=Get(parent);
            if(source==null || source.parent=="root" || IsDeleted(source) || target?.folder!=true || IsDeleted(target)) throw new InvalidOperationException("붙여넣을 파일이나 폴더가 없어요.");
            for(var p=target;p!=null;p=Get(p.parent))if(p.id==source.id)throw new InvalidOperationException("자기 폴더 안으로 이동할 수 없어요.");
            if(move)
            {
                if(Children(parent).Any(f=>f.id!=id && string.Equals(f.name,source.name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("같은 이름의 파일이 있어요.");
                source.parent=parent;Touch();return source;
            }
            var children=Children(source.id).ToArray();var clone=Create(parent,source.name,source.folder);clone.content=source.content;
            foreach(var child in children)Paste(child.id,clone.id,false);
            Touch();return clone;
        }
        public void Touch() { Dirty=true;Changed?.Invoke(); }
        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                var temp=savePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Data,true));
                if(File.Exists(savePath)) File.Replace(temp,savePath,savePath+".bak"); else File.Move(temp,savePath);
                Dirty=false;LastError=null;return true;
            }
            catch(Exception e) { LastError="저장하지 못했어요. "+e.Message;return false; }
        }
    }
}
