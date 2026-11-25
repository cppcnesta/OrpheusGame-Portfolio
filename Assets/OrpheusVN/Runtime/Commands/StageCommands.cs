using System;
using System.Collections;
using UnityEngine;

namespace Orpheus.VN
{
    public class BackgroundCommand : VNCommand
    {
        public override string Name { get { return "背景"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[背景 名字 淡入秒数]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            ctx.Background.Show(ctx.Library.GetBackground(args[0]), FloatArg(args, 1, 0.5f));
            done();
        }
    }

    public class ShowActorCommand : VNCommand
    {
        public override string Name { get { return "登场"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[登场 角色 表情 位置]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            CharacterData character = ctx.Characters.Find(args[0]);
            string expression = Arg(args, 1);
            if (expression == "-") expression = "";

            if (character == null) Debug.LogWarning("[VN] 没有角色「" + args[0] + "」");
            else ctx.Actors.Show(character, expression, Arg(args, 2, "中"));
            done();
        }
    }

    public class HideActorCommand : VNCommand
    {
        public override string Name { get { return "退场"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[退场 角色] 或 [退场 全部]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            if (args[0] == "全部")
            {
                ctx.Actors.HideAll();
            }
            else
            {
                CharacterData character = ctx.Characters.Find(args[0]);
                if (character != null) ctx.Actors.Hide(character.id);
            }
            done();
        }
    }

    public class ExpressionCommand : VNCommand
    {
        public override string Name { get { return "表情"; } }
        public override int MinArgs { get { return 2; } }
        public override string Usage { get { return "[表情 角色 表情]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            CharacterData character = ctx.Characters.Find(args[0]);
            if (character != null) ctx.Actors.SetExpression(character, args[1]);
            done();
        }
    }

    public class CGCommand : VNCommand
    {
        public override string Name { get { return "CG"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[CG 名字 淡入秒数] 或 [CG 名字 淡入秒数 单独展示秒数] 或 [CG 无 淡出秒数]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            float fade = FloatArg(args, 1, 0.5f);
            var video = args[0] == VNCommand.None ? null : ctx.Library.FindVideo(args[0]);
            if (video != null && video.clip != null)
            {
                ctx.CG.ShowVideo(video.clip, video.loop, fade);
                if (video.loop || ctx.Runner.AutoAdvance) { done(); return; }
                if (ctx.Dialogue != null) ctx.Dialogue.Hide();
                ctx.Runner.Run(WaitForVideo(ctx, (float)video.clip.length, done));
                return;
            }
            if (args[0] == VNCommand.None) ctx.CG.Hide(fade);
            else ctx.CG.Show(ctx.Library.GetCG(args[0]), fade);
            float alone = args[0] == VNCommand.None ? 0f : FloatArg(args, 2, 0f);
            if (alone <= 0f || ctx.Runner.AutoAdvance) { done(); return; }
            if (ctx.Dialogue != null) ctx.Dialogue.Hide();
            ctx.Runner.Run(ShowAlone(ctx, fade + alone, done));
        }

        static IEnumerator ShowAlone(VNContext ctx, float seconds, Action done)
        {
            int session = ctx.Session;
            ctx.LockInput();
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
            if (!ctx.IsCurrent(session)) yield break;
            ctx.UnlockInput();
            done();
        }

        static IEnumerator WaitForVideo(VNContext ctx, float length, Action done)
        {
            int session = ctx.Session;
            ctx.LockInput();
            for (float t = 0f; ctx.CG.PlayingVideo && t < length + 3f; t += Time.unscaledDeltaTime)
                yield return null;
            if (!ctx.IsCurrent(session)) yield break;
            ctx.UnlockInput();
            done();
        }
    }
}
