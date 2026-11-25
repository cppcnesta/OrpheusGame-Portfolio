using System;

namespace Orpheus.VN
{
    public class BgmCommand : VNCommand
    {
        public override string Name { get { return "BGM"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[BGM 名字 淡入秒数] 或 [BGM 无 淡出秒数]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            if (args[0] == VNCommand.None) ctx.Audio.StopBgm(FloatArg(args, 1, 1f));
            else
            {
                var clip = ctx.Library.GetBgm(args[0]);
                if (clip != null) ctx.Audio.PlayBgm(clip, FloatArg(args, 1, 1f));
                else { UnityEngine.Debug.LogWarning("[VN] BGM 尚未交付：" + args[0]); ctx.Audio.StopBgm(FloatArg(args, 1, 1f)); }
            }
            done();
        }
    }

    public class SeCommand : VNCommand
    {
        public override string Name { get { return "音效"; } }
        public override int MinArgs { get { return 1; } }
        public override string Usage { get { return "[音效 名字]"; } }

        public override void Execute(string[] args, VNContext ctx, Action done)
        {
            ctx.Audio.PlaySe(ctx.Library.GetSe(args[0]));
            done();
        }
    }
}
