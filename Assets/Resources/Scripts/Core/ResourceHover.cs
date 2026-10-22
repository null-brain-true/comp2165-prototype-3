using UnityEngine;
using UnityEngine.EventSystems;

namespace COMP2165.Clicker
{
    public class ResourceHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public ClickerGame game;
        public int index;
        bool over;
        Vector2 pos;

        public void OnPointerEnter(PointerEventData e) { over = true; pos = e.position; }
        public void OnPointerMove(PointerEventData e) { pos = e.position; }
        public void OnPointerExit(PointerEventData e) { over = false; Hide(); }
        void OnDisable() { if (over) Hide(); over = false; }

        void Hide() { if (game != null && game.Tooltip != null) game.Tooltip.Hide(); }

        void Update()
        {
            if (!over || game == null || !game.Ready || game.Tooltip == null) return;
            game.Tooltip.Show(game.RateTooltip(index), pos);   // refreshed every frame, so the numbers stay live
        }
    }
}