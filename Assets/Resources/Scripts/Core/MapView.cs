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
        public RawImage walkerTemplate;   // switched-off Raw Image the walkers are cloned from

        class Walker
        {
            public RawImage img;
            public Workplace place;   // the building this walker is currently assigned to (null = heading home)
            public Vector2 goal;      // the spot it is currently strolling toward
            public Vector2 from;      // where the current commute started
            public float nextPick;    // when to choose a new spot
            public float progress;    // smooth 0..1 progress along the commute
        }
        readonly Dictionary<Prisoner, Walker> walkers = new Dictionary<Prisoner, Walker>();
        public float wanderRadius = 45f;   // how far from the building centre they roam (map pixels)
        public float wanderSpeed = 25f;    // map pixels per second
        public RawImage hqImage;   // the Raw Image on the HQ object
        public GameObject confirmPanel;   // the Sell Confirm panel (starts switched off)
        public Text confirmText;
        public Button confirmYes, confirmNo;
        Slot pendingSell;

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
            if (hq != null && walkerTemplate != null) UpdateWalkers(g);
        }

        void UpdateWalkers(ClickerGame g)
        {
            PrisonManager prison = g.Prison;
            if (prison == null) return;
            walkerTemplate.gameObject.SetActive(false);

            var spots = new Dictionary<string, RectTransform>();
            var slotCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in slots)
            {
                if (s.occupant == "") continue;
                int n; slotCounts.TryGetValue(s.occupant, out n);
                spots[s.occupant.ToLower() + ":" + n] = s.rect;
                slotCounts[s.occupant] = n + 1;
            }

            Vector2 start = CenterInMap(walkerTemplate.rectTransform);   // the HQ spawn point
            var active = new HashSet<Prisoner>();
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in prison.workplaces)
            {
                int idx; typeCounts.TryGetValue(w.typeId, out idx);
                typeCounts[w.typeId] = idx + 1;

                RectTransform spot;
                if (!spots.TryGetValue(w.typeId.ToLower() + ":" + idx, out spot)) continue;
                Vector2 center = CenterInMap(spot);

                foreach (var p in w.workers)
                {
                    active.Add(p);
                    bool commuting = p.travelLeft > 0;

                    Walker wk;
                    if (!walkers.TryGetValue(p, out wk))
                    {
                        RawImage img = Instantiate(walkerTemplate, map);
                        img.gameObject.SetActive(true);
                        var wr = img.rectTransform;
                        wr.anchorMin = wr.anchorMax = new Vector2(0, 1);
                        wr.pivot = new Vector2(0.5f, 0.5f);
                        wr.sizeDelta = new Vector2(64, 64);
                        wr.localScale = Vector3.one;
                        wr.anchoredPosition = commuting ? start : center;   // loaded saves start at the building
                        img.color = Color.white;
                        img.raycastTarget = false;
                        wk = new Walker { img = img, place = w, from = start, goal = center };
                        walkers[p] = wk;
                    }
                    else if (wk.place != w)
                    {
                        // Newly assigned, or moved to a different building: walk from wherever they are now
                        wk.place = w;
                        wk.from = wk.img.rectTransform.anchoredPosition;
                        wk.progress = 0f;
                        wk.goal = center;
                        wk.nextPick = 0f;
                    }

                    wk.img.texture = g.GetImage(WalkerKey(p.faction));
                    var rt = wk.img.rectTransform;

                    if (commuting)
                    {
                        float total = p.speed > 0 ? w.distance / p.speed : 0;
                        float real = total > 0 ? Mathf.Clamp01(1f - p.travelLeft / total) : 1f;
                        float ceiling = total > 0 ? Mathf.Min(1f, real + 1f / total) : 1f;
                        float step = total > 0 ? Time.deltaTime / total : 1f;
                        wk.progress = Mathf.Clamp(wk.progress + step, real, ceiling);
                        rt.anchoredPosition = Vector2.Lerp(wk.from, center, wk.progress);
                    }
                    else
                    {
                        if (Time.time >= wk.nextPick || Vector2.Distance(rt.anchoredPosition, wk.goal) < 1f)
                        {
                            wk.goal = center + UnityEngine.Random.insideUnitCircle * wanderRadius;
                            wk.nextPick = Time.time + UnityEngine.Random.Range(0.2f, 1f);
                        }
                        rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, wk.goal, wanderSpeed * Time.deltaTime);
                    }
                }
            }

            // Anyone no longer assigned walks back to the HQ, then disappears
            var arrivedHome = new List<Prisoner>();
            foreach (var pair in walkers)
            {
                if (active.Contains(pair.Key)) continue;
                Walker wk = pair.Value;
                wk.place = null;
                var rt = wk.img.rectTransform;
                float speed = pair.Key.speed > 0 ? pair.Key.speed : 50f;
                rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, start, speed * Time.deltaTime);
                if (Vector2.Distance(rt.anchoredPosition, start) < 1f) arrivedHome.Add(pair.Key);
            }
            foreach (var p in arrivedHome) { Destroy(walkers[p].img.gameObject); walkers.Remove(p); }
        }

        Vector2 CenterInMap(RectTransform r)
        {
            Vector3 local = map.InverseTransformPoint(r.TransformPoint(r.rect.center));
            return new Vector2(local.x - map.rect.xMin, local.y - map.rect.yMax);   // top-left origin, same as the walker
        }
        static string WalkerKey(Faction f)
        {
            if (f == Faction.Republic) return "rep";
            if (f == Faction.Imperium) return "imp";
            return "fed";
        }
        void Build()
        {
            built = true;
            if (confirmYes != null) confirmYes.onClick.AddListener(ConfirmSell);
            if (confirmNo != null) confirmNo.onClick.AddListener(CancelSell);
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
                if (confirmPanel == null)   // no popup set up: sell straight away, as before
                {
                    if (game.SellPlacedBuilding(s.occupant)) { s.occupant = ""; Save(); }
                    return;
                }
                pendingSell = s;
                if (confirmText != null)
                    confirmText.text = "Sell " + game.BuildingName(s.occupant) + " for " + game.RefundFor(s.occupant).ToString("0.#") + " supplies?";
                confirmPanel.SetActive(true);
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

        void ConfirmSell()
        {
            if (pendingSell != null && pendingSell.occupant != "")
            {
                if (game.SellPlacedBuilding(pendingSell.occupant)) { pendingSell.occupant = ""; Save(); }
            }
            CancelSell();
        }

        void CancelSell()
        {
            pendingSell = null;
            if (confirmPanel != null) confirmPanel.SetActive(false);
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