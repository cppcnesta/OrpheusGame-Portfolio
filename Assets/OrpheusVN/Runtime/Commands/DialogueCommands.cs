using System;
using System.Collections;
using UnityEngine;

namespace Orpheus.VN
{
    public class SayCommand : VNCommand
    {
        public const string NewBlock = "说";
        public const string SameBlock = "接着说";

        public override string Name { get { return NewBlock; } }
        public override int MinArgs { get { return 3; } }
        public override string Usage { get { return "（由导入器自动生成：说话人、表情、台词）"; } }

        protected virtual bool AppendToBlock { get { return false; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            string speaker = args[0];
            string expression = args[1];
            string text = args[2];

            CharacterData character = ctx.Characters != null ? ctx.Characters.Find(speaker) : null;
            string displayName = character != null ? character.displayName : speaker;
            if (ctx.Characters != null && ctx.Characters.IsHiddenName(speaker)) displayName = "";

            if (character != null && !string.IsNullOrEmpty(expression) && ctx.Actors != null)
                ctx.Actors.SetExpression(character, expression);

            ctx.Dialogue.PrepareSpeaker(character, expression);
            bool append = AppendToBlock && ctx.Dialogue.CanAppend(displayName);
            if (append) ctx.Dialogue.Append(text);
            else ctx.Dialogue.Show(displayName, text);

            if (ctx.Runner.AutoAdvance) { ctx.Dialogue.CompleteTyping(); done(); return; }
            VNEntry next = ctx.Runner.PeekNextLine();
            if (next == null || next.command == NewBlock)
            {
                ctx.Runner.WaitForAdvance(done);
            }
            else if (next.command == WaitClickCommand.CommandName && WaitClickCommand.WillWait(next.args, ctx))
            {
                ctx.Runner.Run(ContinueAfterTyping(ctx, false, done));
            }
            else
            {
                ctx.Runner.Run(ContinueAfterTyping(ctx, true, done));
            }
        }

        static IEnumerator ContinueAfterTyping(VNContext ctx, bool pause, Action done)
        {
            while (ctx.Dialogue.IsTyping) yield return null;

            float waited = 0f;
            while (pause && waited < ctx.LineGap && !ctx.Dialogue.Rushing)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            done();
        }
    }

    public class ContinueSayCommand : SayCommand
    {
        public override string Name { get { return SameBlock; } }
        protected override bool AppendToBlock { get { return true; } }
    }

    public class WaitClickCommand : VNCommand
    {
        public const string CommandName = "等点击";
        public const string FromBlankLine = "空行";

        public override string Name { get { return CommandName; } }
        public override string Usage { get { return "[等点击]"; } }

        public static bool WillWait(string[] args, VNContext ctx)
        {
            bool blankLine = args != null && args.Length > 0 && args[0] == FromBlankLine;
            return !blankLine || ctx.PauseAtBlankLines;
        }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            if (Arg(args, 0) == FromBlankLine && ctx.Dialogue != null) ctx.Dialogue.BreakParagraph();

            if (WillWait(args, ctx)) ctx.Runner.WaitForAdvance(done);
            else done();
        }
    }

    public class WaitCommand : VNCommand
    {
        public override string Name { get { return "等待"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[等待 秒数]"; } }

        public override string Validate(string[] args)
        {
            string error = base.Validate(args);
            if (error != null) return error;
            if (FloatArg(args, 0, -1f) < 0f) return "秒数要写成数字。用法：" + Usage;
            return null;
        }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            ctx.Runner.Run(Wait(ctx.Runner.AutoAdvance ? 0 : FloatArg(args, 0, 1f), done));
        }

        static IEnumerator Wait(float seconds, Action done)
        {
            yield return new WaitForSeconds(seconds);
            done();
        }
    }
}
