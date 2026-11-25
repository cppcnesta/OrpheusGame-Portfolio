using System;

namespace Orpheus.VN
{
    public interface IGameplaySegment
    {
        string Type { get; }

        void Begin(string arg, Action<string> onFinished);

        void Cancel();
    }

    public interface IStageEffect
    {
        string Id { get; }

        void Play(string[] args);
    }
}
