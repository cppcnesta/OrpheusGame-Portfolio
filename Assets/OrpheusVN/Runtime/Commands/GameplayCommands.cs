using System;
using UnityEngine;

namespace Orpheus.VN
{
    public class GameplayCommand : VNCommand
    {
        public const string CommandName = "玩法";

        public override string Name { get { return CommandName; } }
        public override bool EndsDialogueBlock { get { return true; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[玩法 类型 参数 变量]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            string type = args[0];
            string variable = Arg(args, 2, type);

            IGameplaySegment segment;
            if (!VNRegistry.TryGetSegment(type, out segment))
            {
                throw new InvalidOperationException("找不到玩法：" + type);
            }

            if (ctx.ActiveSegment != null) throw new InvalidOperationException("已有未结束的玩法");
            if (ctx.Dialogue != null) ctx.Dialogue.Hide();
            ctx.ActiveSegment = segment;
            ctx.LockInput();
            ctx.SegmentHoldsInputLock = true;

            int session = ctx.Session;
            bool completed = false;
            try
            {
                segment.Begin(Arg(args, 1), result =>
                {
                    if (completed || !ctx.IsCurrent(session) || ctx.ActiveSegment != segment) return;
                    completed = true;
                    ctx.Variables[variable] = result ?? "";
                    ctx.ActiveSegment = null;
                    ctx.ReleaseSegmentInputLock();
                    done();
                });
            }
            catch
            {
                completed = true;
                segment.Cancel();
                ctx.ActiveSegment = null;
                ctx.ReleaseSegmentInputLock();
                throw;
            }
        }
    }

    public class EffectCommand : VNCommand
    {
        public override string Name { get { return "效果"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[效果 名字 参数]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            if (VNRegistry.TryGetEffect(args[0], out var effect) && effect is IBlockingStageEffect blocking)
            {
                ctx.Runner.Run(WaitForEffect(blocking, ArgsFrom(args, 1), ctx, done));
                return;
            }
            VNEffects.Play(args[0], ArgsFrom(args, 1));
            done();
        }
        static System.Collections.IEnumerator WaitForEffect(IBlockingStageEffect effect, string[] args, VNContext ctx, Action done)
        {
            int session=ctx.Session;
            ctx.LockInput();
            try { yield return effect.PlayRoutine(args); }
            finally { if(ctx.IsCurrent(session))ctx.UnlockInput(); }
            if(ctx.IsCurrent(session))done();
        }
    }
}
