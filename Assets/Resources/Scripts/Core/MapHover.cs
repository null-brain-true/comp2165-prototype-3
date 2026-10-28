using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace COMP2165.Clicker
{
    // Put on anything on the map that should show a tooltip while the mouse is over it.
    public class MapHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public ClickerGame game;
        public Func<string> getText;   // returns the text to show (called every frame)
        bool over;
        Vector2 pos;

        public void OnPointerEnter(PointerEventData e) { over = true; pos = e.position; }
        public void OnPointerMove(PointerEventData e) { pos = e.position; }
        public void OnPointerExit(PointerEventData e) { over = false; Hide(); }
        void OnDisable() { if (over) Hide(); over = false; }

        void Hide() { if (game != null && game.Tooltip != null) game.Tooltip.Hide(); }

        void Update()
        {
            if (!over || game == null || !game.Ready || game.Tooltip == null || getText == null) return;
            game.Tooltip.Show(getText(), pos);
        }
    }
}