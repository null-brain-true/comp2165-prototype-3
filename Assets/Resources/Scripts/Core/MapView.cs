using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace COMP2165.Clicker
{
    public class MapView : MonoBehaviour
    {
        public RectTransform map;   // the Map object that holds the slots
        public RectTransform hq;   // the HQ object on the map
        public RawImage hqImage;   // the Raw Image on the HQ object

        class Slot
        {
            public string id, allowed, occupant = "";
            public RawImage icon;
            public Text label;
            public Image bg;
            public Color normal;
            public RectTransform rect;
        }

        readonly List<Slot> slots = new List<Slot>();
        ClickerGame game;
        bool built;

        public void Refresh(ClickerGame g)
        {
            game = g;
            if (!g.Ready || map == null) return;
            if (!built) Build();

            string armedId = g.ArmedBuilding;
            bool armed = !string.IsNullOrEmpty(armedId);
            foreach (var s in slots)
            {
                bool filled = s.occupant != "";
                if (s.icon != null)
                {
                    s.icon.enabled = filled;
                    if (filled) s.icon.texture = g.BuildingIcon(s.occupant);
                }
                if (s.label != null) s.label.text = filled ? "" : "+";
                if (s.bg != null)
                {
                    bool canTake = armed && !filled && (s.allowed == "" || s.allowed.Equals(armedId, StringComparison.OrdinalIgnoreCase));
                    s.bg.color = canTake ? new Color(1f, 0.85f, 0.3f) : s.normal;
                }
            }
              if (hq != null)
            {
                g.ResetDistance();
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in slots)
                {
                    if (s.occupant == "") continue;
                    int i; counts.TryGetValue(s.occupant, out i);
                    g.ApplyDistance(s.occupant, i, Vector2.Distance(s.rect.anchoredPosition, hq.anchoredPosition));
                    counts[s.occupant] = i + 1;
                }
            }
            if (hqImage != null && hqImage.texture == null) hqImage.texture = g.GetImage("hq");
        }

        void Build()
        {
            built = true;
            foreach (var marker in map.GetComponentsInChildren<MapSlot>(true))
            {
                Button b = marker.GetComponent<Button>();
                if (b == null) { Debug.LogError("Map Slot needs a Button: " + marker.name); continue; }
                var s = new Slot
                {
                    id = marker.name,
                    allowed = marker.allowedBuilding,
                    icon = b.GetComponentInChildren<RawImage>(true),
                    label = b.GetComponentInChildren<Text>(true),
                    bg = b.GetComponent<Image>(),
                    normal = b.GetComponent<Image>() != null ? b.GetComponent<Image>().color : Color.white,
                    rect = (RectTransform)marker.transform,
                };
                Slot captured = s;
                b.onClick.AddListener(() => Clicked(captured));
                slots.Add(s);
            }
            Restore();
        }

        void Clicked(Slot s)
        {
            if (s.occupant != "")
            {
                if (game.SellPlacedBuilding(s.occupant)) { s.occupant = ""; Save(); }
                return;
            }
            string armed = game.ArmedBuilding;
            if (string.IsNullOrEmpty(armed)) { game.Notify("Pick a building in the shop first."); return; }
            if (s.allowed != "" && !s.allowed.Equals(armed, StringComparison.OrdinalIgnoreCase))
            { game.Notify("This slot only takes: " + game.BuildingName(s.allowed) + "."); return; }

            if (game.TryPlaceBuilding(armed))
            {
                s.occupant = armed;
                game.ArmedBuilding = "";
                Save();
            }
        }

        string SaveKey() { return "COMP2165_" + game.GetLabel("game_id", "clicker") + "_slots"; }

        void Save()
        {
            var parts = new List<string>();
            foreach (var s in slots) if (s.occupant != "") parts.Add(s.id + "=" + s.occupant);
            PlayerPrefs.SetString(SaveKey(), string.Join(";", parts));
            PlayerPrefs.Save();
        }

        void Restore()
        {
            foreach (string pair in PlayerPrefs.GetString(SaveKey(), "").Split(';'))
            {
                string[] p = pair.Split('=');
                if (p.Length != 2) continue;
                Slot s = slots.Find(x => x.id == p[0]);
                if (s != null) s.occupant = p[1];
            }
            var placed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in slots)
            {
                if (s.occupant == "") continue;
                int n; placed.TryGetValue(s.occupant, out n);
                if (n >= game.OwnedCount(s.occupant)) s.occupant = "";
                else placed[s.occupant] = n + 1;
            }
        }
    }
}