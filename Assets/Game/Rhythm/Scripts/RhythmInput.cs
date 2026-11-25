using UnityEngine;
using UnityEngine.InputSystem;

public static class RhythmInput
{
    public static Vector2 Pointer => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    public static bool Held
    {
        get
        {
            var m = Mouse.current; var k = Keyboard.current;
            return m != null && (m.leftButton.isPressed || m.rightButton.isPressed)
                || k != null && (k.zKey.isPressed || k.xKey.isPressed);
        }
    }

    static int ignoredFrame = -1;
    public static void IgnorePressesThisFrame() => ignoredFrame = Time.frameCount;

    public static bool PressedThisFrame
    {
        get
        {
            if (Time.frameCount == ignoredFrame) return false;
            var m = Mouse.current; var k = Keyboard.current;
            return m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)
                || k != null && (k.zKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame);
        }
    }

    public static Vector2 LocalPointer(RectTransform rect)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Pointer, null, out var local);
        return local;
    }
}
