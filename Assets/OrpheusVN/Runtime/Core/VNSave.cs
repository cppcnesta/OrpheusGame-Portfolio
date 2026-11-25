using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Orpheus.VN
{
    [Serializable]
    public class VNStageState
    {
        public string background = "";
        public string bgm = "";
        public string cg = "";
        public List<ActorState> actors = new List<ActorState>();
        public List<VNPair> expressions = new List<VNPair>();

        [Serializable]
        public class ActorState { public string name, expression, slot; }

        public void Clear()
        {
            background = bgm = cg = "";
            actors.Clear();
            expressions.Clear();
        }

        public void Record(string command, string[] args)
        {
            string a0 = args.Length > 0 ? args[0] : "", a1 = args.Length > 1 ? args[1] : "";
            switch (command)
            {
                case "背景": background = a0; break;
                case "BGM": bgm = a0 == VNCommand.None ? "" : a0; break;
                case "CG": cg = a0 == VNCommand.None ? "" : a0; break;
                case "登场":
                {
                    var actor = actors.Find(x => x.name == a0);
                    if (actor == null) actors.Add(actor = new ActorState { name = a0 });
                    actor.slot = args.Length > 2 && !string.IsNullOrEmpty(args[2]) ? args[2] : "中";
                    if (a1 != "" && a1 != "-") { actor.expression = a1; SetExpression(a0, a1); }
                    break;
                }
                case "退场":
                    if (a0 == "全部") actors.Clear(); else actors.RemoveAll(x => x.name == a0);
                    break;
                case "表情": SetExpression(a0, a1); break;
                case "说":
                case "接着说":
                    if (a1 != "") SetExpression(a0, a1);
                    break;
            }
        }

        void SetExpression(string name, string expression)
        {
            var pair = expressions.Find(x => x.key == name);
            if (pair == null) expressions.Add(pair = new VNPair { key = name });
            pair.value = expression;
            var actor = actors.Find(x => x.name == name);
            if (actor != null) actor.expression = expression;
        }

        public VNStageState Copy() => JsonUtility.FromJson<VNStageState>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public class VNPair { public string key, value; }

    [Serializable]
    public class VNSaveData
    {
        public string script;
        public string label;
        public int offset;
        [Tooltip("[存档 标题] 里写的标题，读档界面显示它")]
        public string title;
        public string savedAt;
        public List<VNPair> variables = new List<VNPair>();
        public VNStageState stage = new VNStageState();
        public List<VNPair> extras = new List<VNPair>();

        public string GetExtra(string key) => extras.Find(x => x.key == key)?.value;
        public void SetExtra(string key, string value)
        {
            var pair = extras.Find(x => x.key == key);
            if (pair == null) extras.Add(pair = new VNPair { key = key });
            pair.value = value;
        }
    }

    public static class VNSaveStore
    {
        static string path;
        public static string Path
        {
            get { return path ?? System.IO.Path.Combine(Application.persistentDataPath, "save.json"); }
            set { path = value; }
        }
        public static bool Exists => File.Exists(Path);

        public static void Write(VNSaveData data)
        {
            File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        }

        public static bool TryRead(out VNSaveData data)
        {
            data = null;
            if (!Exists) return false;
            try { data = JsonUtility.FromJson<VNSaveData>(File.ReadAllText(Path)); }
            catch (Exception e) { Debug.LogWarning("[VN] 存档读不出来：" + e.Message); }
            return data != null && !string.IsNullOrEmpty(data.label);
        }

        public static void Delete() { if (Exists) File.Delete(Path); }
    }

    public class SaveCommand : VNCommand
    {
        public override string Name { get { return "存档"; } }
        public override string Usage { get { return "[存档 标题]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            ctx.Runner.Save(Arg(args, 0));
            done();
        }
    }
}
