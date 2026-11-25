using System;
using System.Globalization;

namespace Orpheus.VN
{
    public abstract class VNCommand
    {
        public const string None = "无";

        public abstract string Name { get; }

        public virtual int MinArgs { get { return 0; } }

        public virtual string Usage { get { return "[" + Name + "]"; } }

        public virtual string Validate(string[] args)
        {
            if (args.Length < MinArgs) return "参数不够。用法：" + Usage;
            return null;
        }

        public virtual string LabelArg(string[] args) { return null; }

        public virtual bool EndsDialogueBlock { get { return false; } }

        public abstract void Execute(string[] args, VNContext ctx, Action done);

        protected static string Arg(string[] args, int i, string fallback = "")
        {
            return i < args.Length && !string.IsNullOrEmpty(args[i]) ? args[i] : fallback;
        }

        protected static float FloatArg(string[] args, int i, float fallback)
        {
            float value;
            if (i < args.Length && float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return fallback;
        }

        protected static string[] ArgsFrom(string[] args, int start)
        {
            if (start >= args.Length) return new string[0];
            var rest = new string[args.Length - start];
            Array.Copy(args, start, rest, 0, rest.Length);
            return rest;
        }
    }
}
