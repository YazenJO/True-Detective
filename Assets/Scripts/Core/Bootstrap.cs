using UnityEngine;
using TrueDetective.UI;

namespace TrueDetective.Core
{
    /// <summary>
    /// Puts the game into whatever scene is running, so pressing Play works from an
    /// empty scene and nothing has to be dragged into the Inspector. Everything the
    /// game needs - canvas, event system, screens - is built by Game itself.
    /// </summary>
    public static class Bootstrap
    {
        public const string CaseToPlay = "case01";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            // a scene that already has a Game keeps it; this only fills a gap
            if (Object.FindObjectOfType<Game>() != null) return;

            var go = new GameObject("TrueDetective");
            var game = go.AddComponent<Game>();
            game.caseId = CaseToPlay;
            Object.DontDestroyOnLoad(go);
        }
    }
}
