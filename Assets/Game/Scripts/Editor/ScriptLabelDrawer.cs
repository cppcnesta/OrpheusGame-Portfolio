using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Orpheus.Editor
{
    [CustomPropertyDrawer(typeof(ScriptLabelAttribute))]
    public sealed class ScriptLabelDrawer : PropertyDrawer
    {
        const string FromTitle = "（从主页开始）";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var flow = property.serializedObject.targetObject as GameFlow;
            if (flow == null || flow.script == null) { EditorGUI.PropertyField(position, property, label); return; }
            var names = new[] { FromTitle }.Concat(flow.script.labels.Select(l => l.name)).ToArray();
            int current = string.IsNullOrEmpty(property.stringValue) ? 0 : System.Array.IndexOf(names, property.stringValue);
            if (current < 0) { names = names.Append(property.stringValue + "（剧本里没有）").ToArray(); current = names.Length - 1; }
            label.text = "从哪里开始";
            int chosen = EditorGUI.Popup(position, label, current, names.Select(n => new GUIContent(n)).ToArray());
            if (chosen != current) property.stringValue = chosen == 0 ? "" : names[chosen];
        }
    }
}
