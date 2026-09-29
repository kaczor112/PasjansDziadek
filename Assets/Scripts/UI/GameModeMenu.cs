using UnityEngine;
using UnityEngine.EventSystems;

namespace Pasjans.UI
{
    /// <summary>Podmenu otwierane kursorem, a na telefonie dotknięciem.</summary>
    public sealed class GameModeMenu : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public PasjansApp App;

        public void OnPointerEnter(PointerEventData e)
        {
            if (!App.UsesTouchMenu) App.ShowGameModes();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (App.UsesTouchMenu && e.button == PointerEventData.InputButton.Left)
                App.ToggleGameModes();
        }
    }
}
