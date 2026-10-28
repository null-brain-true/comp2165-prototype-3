using UnityEngine;
using UnityEngine.UI;

namespace COMP2165.Clicker
{
    public class TabPages : MonoBehaviour
    {
        public Button[] tabs;         // Prisoners, Buildings, Upgrades, Building Info
        public GameObject[] pages;    // PrisonerPage, BuildingsPage, UpgradePage, BuildingInfoPage (same order)
        public int startPage = 1;     // 0 = Prisoners, 1 = Buildings, 2 = Upgrades, 3 = Building Info

        public RawImage[] icons;      // one per tab, same order as tabs
        public string[] iconKeys = { "prisoners", "build", "upgrades", "info" };   // keys in Visuals.csv

        ClickerGame game;
        bool iconsLoaded;

        void Awake()
        {
            Show(startPage);
        }

        // Images load after the scene starts, so wait until the game is ready, then fill the icons once
        void Update()
        {
            if (iconsLoaded) return;
            if (game == null) game = FindFirstObjectByType<ClickerGame>();
            if (game == null || !game.Ready) return;

            // Make sure there is one icon slot per tab
            if (icons == null || icons.Length < tabs.Length) System.Array.Resize(ref icons, tabs.Length);

            for (int i = 0; i < icons.Length; i++)
            {
                // Empty slot: use a Raw Image inside the tab button, or make one
                if (icons[i] == null && i < tabs.Length && tabs[i] != null)
                {
                    icons[i] = tabs[i].GetComponentInChildren<RawImage>(true);
                    if (icons[i] == null) icons[i] = MakeIcon(tabs[i]);
                }
                if (icons[i] == null) continue;

                string key = i < iconKeys.Length ? iconKeys[i] : "";
                Texture2D tex = game.GetImage(key);
                if (tex == null) Debug.LogWarning("TabPages: no image found for key '" + key + "'.");
                icons[i].texture = tex;
                icons[i].enabled = tex != null;
            }
            iconsLoaded = true;
        }

        // Adds a Raw Image that fills the tab button, with a small border
        RawImage MakeIcon(Button tab)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(tab.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);
            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;   // clicks still go to the button
            return img;
        }

        public void Show(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                if (i < tabs.Length) tabs[i].interactable = i != index;   // the open tab greys out
            }
        }
    }
}