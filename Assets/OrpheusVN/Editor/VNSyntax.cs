using System.Text.RegularExpressions;

namespace Orpheus.VN.EditorTools
{
    public static class VNSyntax
    {
        public static readonly Regex SpeakerLine = new Regex(
            @"^(?<name>[^\[\]（）()：:，,。！!…—]{1,16}?)\s*(?:[（(](?<expr>[^）)]+)[）)])?\s*[：:]$");
    }
}
