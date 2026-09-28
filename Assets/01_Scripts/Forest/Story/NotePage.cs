using UnityEngine;

namespace ForestVR
{
    // One of Mateo's written notes: the three notebook pages dropped by the last enemy of each encounter, and the letter
    // in the cabin's drawer. Each asset holds what the player reads and the clue that leads to the next place, so the
    // story is written in the assets (Resources/Story) instead of in the code, and a page is edited without touching it.
    [CreateAssetMenu(menuName = "Forest VR/Note Page", fileName = "Pagina")]
    public sealed class NotePage : ScriptableObject
    {
        [Tooltip("1-3: the notebook pages in order. 4: Mateo's letter in the cabin.")]
        [Min(1)] public int number = 1;
        public string title = "Página";
        [TextArea(6, 16)] public string text;
        [Tooltip("Signature or last line, shown apart at the bottom of the sheet.")]
        public string ending = "— M.";

        public static NotePage Load(int number)
        {
            foreach (var page in Resources.LoadAll<NotePage>("Story"))
                if (page.number == number) return page;
            return null;
        }
    }
}
