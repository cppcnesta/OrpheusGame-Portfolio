using UnityEngine;
using UnityEngine.EventSystems;

namespace Orpheus
{
    public sealed class NoteToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PuzzlePanel owner;
        public int index;
        Vector3 original;
        bool dragging;
        public void OnBeginDrag(PointerEventData data)
        {
            if (owner.Locked) return;
            dragging = true;
            original = transform.position;
            GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || owner.Locked) return;
            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, data.position, data.pressEventCamera, out var point))
                rect.position = point;
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (!dragging) return;
            dragging = false;
            transform.position = original;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
        }
    }
}
