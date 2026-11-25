using UnityEngine;
using UnityEngine.EventSystems;

namespace Orpheus
{
    public sealed class NoteSlot : MonoBehaviour, IDropHandler
    {
        public PuzzlePanel owner;
        public int index;
        public void OnDrop(PointerEventData data)
        {
            var token = data.pointerDrag == null ? null : data.pointerDrag.GetComponent<NoteToken>();
            if (token != null && token.owner == owner) owner.Swap(token.index, index);
        }
    }
}
