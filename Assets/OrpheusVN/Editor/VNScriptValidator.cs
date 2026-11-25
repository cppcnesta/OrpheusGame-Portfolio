using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Orpheus.VN.EditorTools
{
    public static class VNScriptValidator
    {
        [MenuItem("Orpheus/检查所有剧本")]
        static void ValidateAll()
        {
            var characters = LoadFirst<CharacterDatabase>();
            var library = LoadFirst<VNAssetLibrary>();
            if (characters == null) Debug.LogWarning("[剧本检查] 没找到 CharacterDatabase，跳过角色检查");
            if (library == null) Debug.LogWarning("[剧本检查] 没找到 VNAssetLibrary，跳过资源检查");

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:VNScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<VNScript>(path);
                if (script == null) continue;
                foreach (string problem in Problems(script, characters, library))
                {
                    Debug.LogWarning("[剧本检查] " + path + "  " + problem, script);
                    count++;
                }
            }

            if (count == 0) Debug.Log("[剧本检查] 没有发现问题");
            else Debug.LogWarning("[剧本检查] 一共发现 " + count + " 个问题，详情见上面的报告");
        }

        public static List<string> Problems(VNScript script, CharacterDatabase characters, VNAssetLibrary library)
        {
            var problems = new List<string>();
            foreach (string error in script.errors) problems.Add("导入错误：" + error);
            foreach (var entry in script.entries)
            {
                string problem = Check(entry, characters, library);
                if (problem != null) problems.Add("第" + entry.sourceLine + "行：" + problem);
            }
            return problems;
        }

        public static bool IsPendingAsset(string problem) => problem.Contains("登记了名字，但没有拖");

        static string Check(VNEntry entry, CharacterDatabase characters, VNAssetLibrary library)
        {
            string a0 = Arg(entry, 0);
            string a1 = Arg(entry, 1);

            switch (entry.command)
            {
                case "说":
                case "接着说":
                    return CheckCharacter(characters, a0, a1, false);
                case "登场":
                    return CheckCharacter(characters, a0, a1 == "-" ? "" : a1, true);
                case "表情":
                    return CheckCharacter(characters, a0, a1, true);
                case "CG":
                case "BGM":
                    if (a0 == VNCommand.None) return null;
                    return CheckAsset(library, entry.command, a0);
                case "背景":
                case "音效":
                    return CheckAsset(library, entry.command, a0);
            }
            return null;
        }

        static string CheckCharacter(CharacterDatabase characters, string name, string expression, bool mustExist)
        {
            if (characters == null || characters.IsHiddenName(name)) return null;

            CharacterData character = characters.Find(name);
            if (character == null) return mustExist ? "角色表里没有「" + name + "」" : null;
            if (string.IsNullOrEmpty(expression) || expression == "-") return null;
            var mapped = character.expressions.Find(x => x.key == expression);
            if (mapped == null) return "角色「" + name + "」没有登记表情「" + expression + "」";
            if (mapped.sprite == null || mapped.provisional) return "角色「" + name + "」的表情「" + expression + "」还没有立绘（现在用默认立绘）";
            return null;
        }

        static string CheckAsset(VNAssetLibrary library, string kind, string name)
        {
            if (library == null) return null;
            string problem = library.Problem(kind, name);
            return problem == null ? null : kind + "「" + name + "」" + problem;
        }

        static string Arg(VNEntry entry, int i)
        {
            return entry.args != null && i < entry.args.Length ? entry.args[i] : "";
        }

        static T LoadFirst<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
