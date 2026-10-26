using UnityEngine;
using UnityEngine.UI;

namespace COMP2165.Clicker
{
    public class ResetControl : MonoBehaviour
    {
        public ClickerGame game;        // drag in Game Manager
        public Button resetButton;      // the RESET button
        public GameObject confirmPanel; // the confirmation panel (starts switched off)
        public Button confirmYes, confirmNo;

        void Awake()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (resetButton != null) resetButton.onClick.AddListener(Ask);
            if (confirmYes != null) confirmYes.onClick.AddListener(Yes);
            if (confirmNo != null) confirmNo.onClick.AddListener(No);
        }

        void Ask() { if (confirmPanel != null) confirmPanel.SetActive(true); }
        void No()  { if (confirmPanel != null) confirmPanel.SetActive(false); }

        void Yes()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (game != null) game.ResetGame();
        }
    }
}