using System;
using System.Linq;
using Orpheus.VN;

namespace Orpheus
{
    [Serializable]
    public struct RhythmLine
    {
        public string speaker, text;
        public RhythmLine(string speaker, string text) { this.speaker = speaker; this.text = text; }
    }

    public static class RhythmScriptContent
    {
        static readonly string[] PaperEffects = { "白纸", "白纸满屏", "白纸雨" };

        public static void Fill(ChapterDefinition chapter, VNScript script)
        {
            chapter.lines.Clear();
            chapter.papers.Clear();
            if (script == null || string.IsNullOrEmpty(chapter.scriptSection)) return;
            int start = script.FindLabel(chapter.scriptSection);
            if (start < 0) return;
            int end = script.labels.Where(l => l.index > start).Select(l => l.index).DefaultIfEmpty(script.entries.Count).Min();

            int rhythm = start;
            while (rhythm < end && !IsRhythm(script.entries[rhythm], chapter.segment)) rhythm++;
            if (chapter.linesAfterRhythm) ReadLines(chapter, script, rhythm + 1, end);
            else ReadLines(chapter, script, start, rhythm);

            for (int i = start; i < end; i++)
            {
                var e = script.entries[i];
                if (e.command != "效果" || e.args.Length < 2 || !PaperEffects.Contains(e.args[0])) continue;
                foreach (var note in string.Join(" ", e.args.Skip(1)).Split(';', '；'))
                    if (note.Trim().Length > 0) chapter.papers.Add(note.Trim());
            }
        }

        static bool IsRhythm(VNEntry e, string segment) =>
            e.command == GameplayCommand.CommandName && e.args.Length > 1 && e.args[0] == "rhythm" && e.args[1] == segment;

        static void ReadLines(ChapterDefinition chapter, VNScript script, int from, int to)
        {
            int blocks = 0;
            string speaker = null, text = null;
            void Flush() { if (text != null) chapter.lines.Add(new RhythmLine(speaker, text)); text = null; }
            for (int i = from; i < to; i++)
            {
                var e = script.entries[i];
                if (e.command == SayCommand.NewBlock)
                {
                    Flush();
                    bool wanted = chapter.lineSpeakers != null && chapter.lineSpeakers.Any(p => !string.IsNullOrEmpty(p) && e.args[0].StartsWith(p));
                    if (wanted && chapter.maxLineBlocks > 0 && blocks >= chapter.maxLineBlocks) return;
                    speaker = !wanted ? null : e.args[0] == "旁白" ? "" : e.args[0];
                    if (wanted) { blocks++; text = e.args[2]; }
                }
                else if (e.command == SayCommand.SameBlock && speaker != null)
                    text = text == null ? e.args[2] : text + "\n" + e.args[2];
                else if (e.command == WaitClickCommand.CommandName && e.args.Length > 0 && e.args[0] == WaitClickCommand.FromBlankLine)
                    Flush();
            }
            Flush();
        }
    }
}
