using UnityEngine;
using UnityEngine.UI;
 
namespace COMP2165.Clicker
{
    // Shows a Victory or Defeat screen when the game ends.
    // Put this on an object that is always active (e.g. the Canvas), NOT on the panels themselves.
    // Both panels start switched off. Play Again wipes the save and reloads the scene.
    public class EndScreen : MonoBehaviour
    {
        [Header("Victory")]
        public GameObject victoryPanel;
        public Text victoryStats;          // optional: "The camp held against all 5 raids."
        public Button victoryPlayAgain;
 
        [Header("Defeat")]
        public GameObject defeatPanel;
        public Text defeatStats;           // optional: "The HQ fell after holding off 3 raids."
        public Button defeatPlayAgain;
 
        ClickerGame game;
        bool shown;
 
        void Start()
        {
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            if (victoryPlayAgain != null) victoryPlayAgain.onClick.AddListener(PlayAgain);
            if (defeatPlayAgain != null) defeatPlayAgain.onClick.AddListener(PlayAgain);
            game = FindFirstObjectByType<ClickerGame>();
        }
 
        void Update()
        {
            if (shown || game == null || !game.Ready || !game.GameOver) return;
            shown = true;
 
            int raids = game.RaidsDone;
            int prisoners = game.Prison != null ? game.Prison.PrisonerCount() : 0;
 
            if (game.Won)
            {
                if (victoryStats != null)
                    victoryStats.text = "The camp held against all " + raids + " raids.\nPrisoners in the camp: " + prisoners;
                if (victoryPanel != null) victoryPanel.SetActive(true);
                game.PlaySfx("victory", "success");   // uses a "victory" sound if Audio.csv has one
            }
            else
            {
                if (defeatStats != null)
                    defeatStats.text = "The HQ fell after holding off " + raids + " raid" + (raids == 1 ? "" : "s") + ".\nPrisoners in the camp: " + prisoners;
                if (defeatPanel != null) defeatPanel.SetActive(true);
                game.PlaySfx("defeat", "failure");    // uses a "defeat" sound if Audio.csv has one
            }
        }
 
        void PlayAgain()
        {
            if (game != null) game.ResetGame();   // wipes every save and reloads the scene
        }
    }
}