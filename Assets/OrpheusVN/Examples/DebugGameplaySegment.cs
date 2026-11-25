using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Orpheus.VN
{
    public class DebugGameplaySegment : MonoBehaviour, IGameplaySegment
    {
        [SerializeField] string type = "rhythm";
        [SerializeField] string result = "calling";
        [SerializeField] float duration = 5f;

        public string Type { get { return type; } }
        float Elapsed { get { return running ? Time.time - startTime : 0f; } }

        bool running;
        float startTime;
        Action<string> onFinished;

        void OnEnable() { VNRegistry.Register(this); }
        void OnDisable() { VNRegistry.Unregister(this); }

        public void Begin(string arg, Action<string> finished)
        {
            Debug.Log("[测试玩法] 开始 " + type + "（参数：" + arg + "），" + duration + " 秒后结束，按 F 立刻结束");
            running = true;
            startTime = Time.time;
            onFinished = finished;
        }

        public void Cancel()
        {
            if (!running) return;
            Debug.Log("[测试玩法] " + type + " 被取消");
            running = false;
            onFinished = null;
        }

        void Update()
        {
            if (!running) return;
            if (Elapsed >= duration || SkipPressed()) Finish();
        }

        void Finish()
        {
            running = false;
            var callback = onFinished;
            onFinished = null;
            if (callback != null) callback(result);
        }

        static bool SkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }
    }
}
