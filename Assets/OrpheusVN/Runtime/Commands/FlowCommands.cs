using System;

namespace Orpheus.VN
{
    public class JumpCommand : VNCommand
    {
        public override string Name { get { return "跳转"; } }
        public override bool EndsDialogueBlock { get { return true; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[跳转 标签]"; } }
        public override string LabelArg(string[] args) { return Arg(args, 0); }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            ctx.Runner.JumpTo(args[0]);
            done();
        }
    }

    public class SetCommand : VNCommand
    {
        public override string Name { get { return "设置"; } }
        public override int MinArgs { get { return 2; } }
        public override string Usage { get { return "[设置 变量 值]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            ctx.Variables[args[0]] = args[1];
            done();
        }
    }

    public class IfCommand : VNCommand
    {
        public override string Name { get { return "如果"; } }
        public override bool EndsDialogueBlock { get { return true; } }
        public override int MinArgs { get { return 3; } }
        public override string Usage { get { return "[如果 变量 值 标签]"; } }
        public override string LabelArg(string[] args) { return Arg(args, 2); }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            string value;
            if (ctx.Variables.TryGetValue(args[0], out value) && value == args[1])
                ctx.Runner.JumpTo(args[2]);
            done();
        }
    }

    public class TransitionCommand : VNCommand
    {
        public override string Name { get { return "转场"; } }
        public override bool EndsDialogueBlock { get { return true; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[转场 淡出 秒数] 或 [转场 淡入 秒数]"; } }

        public override string Validate(string[] args)
        {
            string error = base.Validate(args);
            if (error != null) return error;
            if (args[0] != "淡出" && args[0] != "淡入") return "转场只支持「淡出」和「淡入」。用法：" + Usage;
            return null;
        }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            bool toBlack = args[0] == "淡出";
            int session = ctx.Session;
            ctx.LockInput();
            ctx.Transition.Fade(toBlack, FloatArg(args, 1, 0.5f), () =>
            {
                if (!ctx.IsCurrent(session)) return;
                ctx.UnlockInput();
                done();
            });
        }
    }
}
