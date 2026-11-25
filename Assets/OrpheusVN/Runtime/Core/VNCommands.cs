using System.Collections.Generic;

namespace Orpheus.VN
{
    public static class VNCommands
    {
        static readonly Dictionary<string, VNCommand> map = Build(
            new SayCommand(),
            new ContinueSayCommand(),
            new WaitClickCommand(),
            new WaitCommand(),
            new BackgroundCommand(),
            new ShowActorCommand(),
            new HideActorCommand(),
            new ExpressionCommand(),
            new CGCommand(),
            new BgmCommand(),
            new SeCommand(),
            new JumpCommand(),
            new SetCommand(),
            new IfCommand(),
            new TransitionCommand(),
            new GameplayCommand(),
            new EffectCommand(),
            new SaveCommand()
        );

        static Dictionary<string, VNCommand> Build(params VNCommand[] commands)
        {
            var result = new Dictionary<string, VNCommand>();
            foreach (var command in commands) result[command.Name] = command;
            return result;
        }

        public static bool TryGet(string name, out VNCommand command)
        {
            return map.TryGetValue(name, out command);
        }

        public static IEnumerable<VNCommand> All { get { return map.Values; } }
    }
}
