using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace COMP2165.Clicker
{
    public class ResourceBar : MonoBehaviour
    {
        public ClickerGame game;             // drag in Game Manager
        public RectTransform entryTemplate;  // switched off; see the Unity steps
        public Text warningText;             // optional: shows STARVING
        public Text raidText;          // shows the raid countdown and HQ health
        public GameObject endPanel;    // optional: shown on win or loss
        public Text endText;

        class Entry { public RawImage icon; public Text label; }
        readonly List<Entry> entries = new List<Entry>();
        bool built;

        void Awake()
        {
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
        }

        void Update()
        {
            if (game == null || !game.Ready || entryTemplate == null) return;
            if (!built) Build();

            for (int i = 0; i < entries.Count; i++)
            {
                Texture2D art = game.GetImage("res_" + game.CurrencyId(i)) ?? game.GetImage(game.CurrencyId(i));
                entries[i].label.text = (art != null ? "" : game.CurrencyName(i) + " ") + game.AmountText(i);
                entries[i].icon.enabled = art != null;
                entries[i].icon.texture = art;
            }
            if (warningText != null) warningText.text = game.IsStarving ? "STARVING!" : "";
            if (raidText != null) raidText.text = game.RaidStatus();
            if (endPanel != null)
            {
                endPanel.SetActive(game.GameOver);
                if (game.GameOver && endText != null) endText.text = game.RaidStatus();
            }
        }

        void Build()
        {
            built = true;
            for (int i = 0; i < game.CurrencyCount; i++)
            {
                RectTransform rt = Instantiate(entryTemplate, entryTemplate.parent);
                rt.gameObject.SetActive(true);
                rt.name = "Resource - " + game.CurrencyName(i);
                RawImage icon = rt.GetComponentInChildren<RawImage>(true);
                Text label = rt.GetComponentInChildren<Text>(true);
                if (icon == null || label == null)
                { Debug.LogError("Entry Template needs a RawImage child and a Text child."); return; }

                ResourceHover hover = rt.gameObject.AddComponent<ResourceHover>();
                hover.game = game;
                hover.index = i;
                entries.Add(new Entry { icon = icon, label = label });
            }
        }
    }
}