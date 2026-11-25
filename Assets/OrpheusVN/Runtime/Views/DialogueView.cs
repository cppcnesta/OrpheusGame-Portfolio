using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Orpheus.VN
{
    public class DialogueView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject box;
        [SerializeField] GameObject nameBox;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] Graphic clickArea;
        [SerializeField] CanvasGroup inputGroup;
        [SerializeField] float charsPerSecond = 30f;

        public event Action AdvanceRequested;
        public event Action<CharacterData, string> SpeakerPrepared;
        public event Action LineShown;
        public void PrepareSpeaker(CharacterData character, string expression)
            => SpeakerPrepared?.Invoke(character, expression);

        public bool IsTyping { get; private set; }

        public int CurrentLine { get; private set; }

        public bool Rushing { get; private set; }

        Coroutine typing;
        bool inputEnabled = true;
        string blockSpeaker;
        bool paragraphPending;

        public int Show(string speaker, string text)
        {
            CurrentLine++;
            if (box != null) box.SetActive(true);

            bool hasName = !string.IsNullOrEmpty(speaker);
            if (nameBox != null) nameBox.SetActive(hasName);
            if (nameText != null) nameText.text = hasName ? speaker : "";

            blockSpeaker = speaker ?? "";
            paragraphPending = false;
            Rushing = false;

            bodyText.text = text;
            StartTyping(0);
            ScrollTo(1f);
            LineShown?.Invoke();
            return CurrentLine;
        }

        public bool CanAppend(string speaker)
        {
            return blockSpeaker != null && blockSpeaker == (speaker ?? "") && (box == null || box.activeSelf);
        }

        public int Append(string text)
        {
            CurrentLine++;
            if (box != null) box.SetActive(true);

            CompleteTyping();
            bodyText.ForceMeshUpdate();
            int alreadyShown = bodyText.textInfo.characterCount;

            bodyText.text = bodyText.text + (paragraphPending ? "\n\n" : "\n") + text;
            paragraphPending = false;

            if (!Rushing) StartTyping(alreadyShown);
            ScrollTo(0f);
            LineShown?.Invoke();
            return CurrentLine;
        }

        public void BreakParagraph()
        {
            paragraphPending = true;
        }

        public void EndBlock()
        {
            blockSpeaker = null;
            paragraphPending = false;
            Rushing = false;
        }

        public void Rush()
        {
            Rushing = true;
            CompleteTyping();
        }

        public void StopRush()
        {
            Rushing = false;
        }

        public void Hide()
        {
            CompleteTyping();
            EndBlock();
            if (box != null) box.SetActive(false);
        }

        public void CompleteTyping()
        {
            if (typing != null) StopCoroutine(typing);
            typing = null;
            bodyText.maxVisibleCharacters = int.MaxValue;
            IsTyping = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (hintText != null) hintText.text = enabled ? "点击继续 / 空格 · 滚轮回看本段" : "";
            if (inputGroup != null) inputGroup.blocksRaycasts = enabled;
            if (clickArea != null) clickArea.raycastTarget = enabled;
        }

        void StartTyping(int from)
        {
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeRoutine(from));
        }

        IEnumerator TypeRoutine(int from)
        {
            IsTyping = true;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            float shown = from;
            bodyText.maxVisibleCharacters = from;

            while (shown < total)
            {
                shown += charsPerSecond * Time.deltaTime;
                bodyText.maxVisibleCharacters = Mathf.Min(total, (int)shown);
                yield return null;
            }

            typing = null;
            bodyText.maxVisibleCharacters = int.MaxValue;
            IsTyping = false;
        }

        void ScrollTo(float position)
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = position;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!inputEnabled || (eventData != null && (eventData.dragging || eventData.button != PointerEventData.InputButton.Left))) return;
            if (eventData != null && eventData.pointerPressRaycast.gameObject != null
                && eventData.pointerPressRaycast.gameObject.GetComponentInParent<Scrollbar>() != null)
                return;
            RequestAdvance();
        }

        void Update()
        {
            if (!inputEnabled) return;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                RequestAdvance();
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                RequestAdvance();
#endif
        }

        void RequestAdvance()
        {
            if (AdvanceRequested != null) AdvanceRequested();
        }
    }
}
