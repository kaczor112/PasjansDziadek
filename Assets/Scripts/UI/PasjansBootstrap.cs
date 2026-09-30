using UnityEngine;
using UnityEngine.Scripting;

namespace Pasjans.UI
{
    /// <summary>Uruchamia interfejs, gdy scena nie odtworzy jego komponentu.</summary>
    [Preserve]
    public static class PasjansBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureApplicationStarted()
        {
            if (Object.FindFirstObjectByType<PasjansApp>() != null) return;

            GameObject host = GameObject.Find("Pasjans");
            if (host == null) host = new GameObject("Pasjans");
            host.AddComponent<PasjansApp>();
            Debug.Log("Pasjans: moduł startowy utworzył interfejs.");
        }
    }
}
