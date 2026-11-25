using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Orpheus.VN.EditorTools
{
    [ScriptedImporter(6, "orph")]
    public class OrpheusScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var script = ScriptableObject.CreateInstance<VNScript>();
            var errors = new List<string>();
            var warnings = new List<string>();
            string[] lines = File.ReadAllLines(ctx.assetPath);

            string speaker = "";
            string expression = "";

            bool inBlock = false;
            int blankAt = -1;
            int blankLine = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string s = lines[i].Trim();

                if (s.StartsWith("*")) continue;

                if (s.Length == 0)
                {
                    if (inBlock && blankAt < 0)
                    {
                        blankAt = script.entries.Count;
                        blankLine = lineNo;
                    }
                    continue;
                }

                if (s.StartsWith("#"))
                {
                    AddLabel(script, s.TrimStart('#').Trim().TrimEnd('：', ':').Trim(), lineNo, errors);
                    speaker = "";
                    expression = "";
                    EndBlock(ref inBlock, ref blankAt);
                    continue;
                }

                if (s.StartsWith("[") && !s.EndsWith("]"))
                {
                    errors.Add("第" + lineNo + "行：命令少了右括号 ]");
                    continue;
                }
                if (s.StartsWith("[") && s.EndsWith("]"))
                {
                    string[] parts = s.Substring(1, s.Length - 2)
                        .Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                    {
                        errors.Add("第" + lineNo + "行：空的命令 []");
                        continue;
                    }

                    string name = parts[0];
                    string[] args = new string[parts.Length - 1];
                    Array.Copy(parts, 1, args, 0, args.Length);

                    VNCommand command;
                    if (!VNCommands.TryGet(name, out command))
                    {
                        errors.Add("第" + lineNo + "行：不认识的命令「" + name + "」");
                        continue;
                    }
                    string error = command.Validate(args);
                    if (error != null)
                    {
                        errors.Add("第" + lineNo + "行：" + error);
                        continue;
                    }

                    script.Add(name, args, lineNo);
                    if (command.EndsDialogueBlock) EndBlock(ref inBlock, ref blankAt);
                    continue;
                }

                Match match = VNSyntax.SpeakerLine.Match(s);
                if (match.Success)
                {
                    speaker = match.Groups["name"].Value.Trim();
                    expression = match.Groups["expr"].Success ? match.Groups["expr"].Value.Trim() : "";
                    EndBlock(ref inBlock, ref blankAt);
                    continue;
                }

                if (speaker == "") warnings.Add("第" + lineNo + "行：这句台词前面没有说话人");
                if (inBlock)
                {
                    if (blankAt >= 0)
                        script.Insert(blankAt, WaitClickCommand.CommandName, new[] { WaitClickCommand.FromBlankLine }, blankLine);
                    script.Add(SayCommand.SameBlock, new[] { speaker, expression, s }, lineNo);
                }
                else
                {
                    script.Add(SayCommand.NewBlock, new[] { speaker, expression, s }, lineNo);
                }
                inBlock = true;
                blankAt = -1;
                expression = "";
            }

            for (int i = script.entries.Count - 1; i >= 0; i--)
            {
                var entry = script.entries[i];
                VNCommand command;
                if (!VNCommands.TryGet(entry.command, out command)) continue;
                string target = command.LabelArg(entry.args);
                if (string.IsNullOrEmpty(target) || script.FindLabel(target) >= 0) continue;

                errors.Add("第" + entry.sourceLine + "行：找不到标签「" + target + "」");
                RemoveEntry(script, i);
            }

            errors.Sort(CompareByLine);
            script.errors.AddRange(errors);

            foreach (string error in errors)
                Debug.LogError("[剧本导入] " + ctx.assetPath + "  " + error + "（这条已跳过）");
            foreach (string warning in warnings)
                Debug.LogWarning("[剧本导入] " + ctx.assetPath + "  " + warning);

            ctx.AddObjectToAsset("script", script);
            ctx.SetMainObject(script);
        }

        static void EndBlock(ref bool inBlock, ref int blankAt)
        {
            inBlock = false;
            blankAt = -1;
        }
        static void AddLabel(VNScript script, string label, int lineNo, List<string> errors)
        {
            if (script.FindLabel(label) >= 0)
            {
                errors.Add("第" + lineNo + "行：标签「" + label + "」重复了，跳转只会跳到第一个");
                return;
            }
            script.AddLabel(label);
        }

        static void RemoveEntry(VNScript script, int index)
        {
            script.entries.RemoveAt(index);
            foreach (var label in script.labels)
            {
                if (label.index > index) label.index--;
            }
        }

        static int CompareByLine(string a, string b)
        {
            return LineOf(a).CompareTo(LineOf(b));
        }

        static int LineOf(string message)
        {
            Match m = Regex.Match(message, @"第(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue;
        }
    }
}
