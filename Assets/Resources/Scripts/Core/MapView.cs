using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

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
        public RawImage enemyTemplate;   // switched-off Raw Image the enemies are cloned from
        public RectTransform enemySpawn; // where enemies appear
        public float enemySpeed = 60f;   // map pixels per second
        readonly List<RawImage> enemies = new List<RawImage>();
        readonly List<float> enemyHealth = new List<float>();
        readonly List<Text> enemyLabels = new List<Text>();
        Dictionary<Slot, float> activeTowers = new Dictionary<Slot, float>();
        Dictionary<Slot, List<float>> activeGuards = new Dictionary<Slot, List<float>>();   // was <Slot, int>
        public float enemyHealthMax = 3f;   // health of each raider
        public float towerDamage = 5f;      // damage per second = tower power x this
        readonly List<EnemyType> enemyKinds = new List<EnemyType>();
        readonly List<float> enemyMax = new List<float>();
        public GameObject confirmPanel;   // the Sell Confirm panel (starts switched off)
        public Text confirmText;
        public Button confirmYes, confirmNo;
        Slot pendingSell;
        Slot openSlot;          // the building whose details are showing
        public TabPages tabs;   // optional: drag in the object with TabPages, so clicking a building opens the Prisoners tab
        public int buildingsPage = 1;   // index of BuildingsPage in TabPages
        Slot pendingSlot;               // the empty slot the player clicked
        public int buildingInfoPage = 3;   // index of BuildingInfoPage in TabPages
        public int prisonersPage = 0;        // index of PrisonerPage in TabPages
        public float hqWanderRadius = 60f;   // how far waiting prisoners roam around the HQ (map pixels)
        public float attackRange = 40f;   // how close a monster must get to a building to start hitting it
        float nextHpSave;
        readonly List<Slot> enemyTargets = new List<Slot>();   // null = heading for the HQ

        class Slot
        {
            public string id, allowed, occupant = "";
            public RawImage icon;
            public Text label;
            public Image bg;
            public Color normal;
            public RectTransform rect;
            public float hp;   // 0 = not set yet (a new building starts at full health)
        }

        readonly List<Slot> slots = new List<Slot>();
        ClickerGame game;
        bool built;

        public void Refresh(ClickerGame g)
        {
            game = g;
            if (!g.Ready || map == null) return;
            if (!built) Build();
            PlacePending();

            string armedId = g.ArmedBuilding;
            bool armed = !string.IsNullOrEmpty(armedId);
            float maxHp = MaxHp();
            foreach (var s in slots)
            {
                bool filled = s.occupant != "";
                if (!filled) s.hp = 0;
                else if (s.hp <= 0 || s.hp > maxHp) s.hp = maxHp;   // new buildings start at full health

                if (s.icon != null)
                {
                    s.icon.enabled = filled;
                    if (filled) s.icon.texture = g.BuildingIcon(s.occupant);
                }
                if (s.label != null)
                    s.label.text = !filled ? "+" : (s.hp < maxHp - 0.01f ? Mathf.CeilToInt(s.hp).ToString() : "");
                if (s.bg != null)
                {
                    bool canTake = armed && !filled && (s.allowed == "" || s.allowed.Equals(armedId, StringComparison.OrdinalIgnoreCase));
                    s.bg.color = canTake ? new Color(1f, 0.85f, 0.3f) : s.normal;
                }
            }
            if (Time.time >= nextHpSave) { nextHpSave = Time.time + 15f; Save(); }
            if (hq != null)
            {
                g.ResetDistance();
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in slots)
                {
                    if (s.occupant == "") continue;
                    int i; counts.TryGetValue(s.occupant, out i);
                    g.ApplyDistance(s.occupant, i, Vector2.Distance(CenterInMap(s.rect), CenterInMap(hq)));
                    counts[s.occupant] = i + 1;
                }
            }
            g.ResetGuard();
            var idxOf = new Dictionary<Slot, int>();
            var typeSeen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var towerPower = new Dictionary<Slot, float>();
            var towerGuards = new Dictionary<Slot, List<float>>();   // was <Slot, int>
            foreach (var s in slots)
            {
                if (s.occupant == "") continue;
                int idx; typeSeen.TryGetValue(s.occupant, out idx);
                typeSeen[s.occupant] = idx + 1;
                idxOf[s] = idx;
                if (g.Prison.IsGuardType(s.occupant))
                {
                    towerPower[s] = g.GuardPowerOf(s.occupant, idx);
                    towerGuards[s] = ShooterStats(g.Prison, s.occupant, idx);   // was WorkersAt(...)
                }
            }
            foreach (var s in slots)
            {
                if (s.occupant == "") continue;
                float power = 0;
                foreach (var t in towerPower)
                    if (t.Key != s && Vector2.Distance(CenterInMap(s.rect), CenterInMap(t.Key.rect)) <= g.GuardRadius) power += t.Value;
                g.ApplyGuard(s.occupant, idxOf[s], power);
            }
            activeTowers = towerPower;
            activeGuards = towerGuards;
            // HQ defense: a flat amount for every defense building on the map
            int manned = 0;
            foreach (var t in towerPower) if (t.Value > 0) manned++;   // at least one guard has arrived
            float hqDefense = manned * g.GetSetting("defense_per_tower", 0.3f);
            g.SetHqDefense(hqDefense);
            if (hqImage != null && hqImage.texture == null) hqImage.texture = g.GetImage("hq");
            if (hq != null && walkerTemplate != null) UpdateWalkers(g);
            if (hq != null) MoveEnemies();
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

            // Waiting prisoners hang around the HQ
            Vector2 hqCenter = CenterInMap(hq);
            foreach (var p in prison.unassigned)
            {
                active.Add(p);

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
                    wr.anchoredPosition = hqCenter + UnityEngine.Random.insideUnitCircle * hqWanderRadius;
                    img.color = Color.white;
                    img.raycastTarget = false;
                    wk = new Walker { img = img, place = null, from = start, goal = hqCenter };
                    walkers[p] = wk;
                }
                wk.place = null;
                wk.img.texture = g.GetImage(WalkerKey(p.faction));

                var rt = wk.img.rectTransform;
                if (Vector2.Distance(rt.anchoredPosition, hqCenter) > hqWanderRadius * 1.5f)
                {
                    // Still on the way back from a building: walk home at their own speed
                    float speed = p.speed > 0 ? p.speed : 50f;
                    rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, hqCenter, speed * Time.deltaTime);
                    wk.nextPick = 0f;
                }
                else
                {
                    if (Time.time >= wk.nextPick || Vector2.Distance(rt.anchoredPosition, wk.goal) < 1f)
                    {
                        wk.goal = hqCenter + UnityEngine.Random.insideUnitCircle * hqWanderRadius;
                        wk.nextPick = Time.time + UnityEngine.Random.Range(0.2f, 1f);
                    }
                    rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, wk.goal, wanderSpeed * Time.deltaTime);
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

        public void SpawnRaiders(List<EnemyType> wave)                        // CHANGED: was (int count)
        {
            if (enemyTemplate == null || enemySpawn == null || hq == null)
            {
                Debug.LogWarning("SpawnRaiders fallback. Template: " + (enemyTemplate != null) + ", Spawn: " + (enemySpawn != null) + ", HQ: " + (hq != null));
                for (int i = 0; i < wave.Count; i++) game.RaiderArrived();    // CHANGED: was count
                return;
            }
            Vector2 start = CenterInMap(enemySpawn);
            foreach (EnemyType kind in wave)                                  // CHANGED: was for + PickEnemyType()
            {
                RawImage img = Instantiate(enemyTemplate, map);
                img.gameObject.SetActive(true);
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(64, 64);
                r.localScale = Vector3.one;
                r.anchoredPosition = start + UnityEngine.Random.insideUnitCircle * 40f;
                img.raycastTarget = false;

                Texture2D art = game.GetImage(kind.image) ?? game.GetImage("monster");
                Color tint;
                if (string.IsNullOrEmpty(kind.tint) || !ColorUtility.TryParseHtmlString(kind.tint, out tint)) tint = Color.white;
                img.texture = art;
                img.color = art != null ? tint : Color.red;

                enemies.Add(img);
                enemyKinds.Add(kind);
                enemyHealth.Add(kind.health);
                enemyMax.Add(kind.health);
                Text templateText = img.GetComponentInChildren<Text>(true);
                Font font = (templateText != null && templateText.font != null)
                    ? templateText.font
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (templateText != null) templateText.gameObject.SetActive(false);

                MakeEnemyText(img.transform, "Enemy Name", font, true).text = kind.name;
                Text healthLabel = MakeEnemyText(img.transform, "Enemy Health", font, false);
                healthLabel.text = Mathf.CeilToInt(kind.health) + "/" + Mathf.CeilToInt(kind.health);
                enemyLabels.Add(healthLabel);
                enemyTargets.Add(PickTarget(r.anchoredPosition));
            }
        }

        void MoveEnemies()
        {
            Vector2 hqPos = CenterInMap(hq);

            // 1. Move everyone. A monster that reaches its building hits it instead of walking on.
            for (int i = 0; i < enemies.Count; i++)
            {
                var rt = enemies[i].rectTransform;
                EnemyType kind = enemyKinds[i];
                float speed = kind.speed > 0 ? kind.speed : enemySpeed;
                Slot t = enemyTargets[i];
                if (t != null && t.occupant == "")
                {
                    t = PickTarget(rt.anchoredPosition);
                    enemyTargets[i] = t;
                }

                if (t == null)
                {
                    rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, hqPos, speed * Time.deltaTime);
                    continue;
                }

                Vector2 goal = CenterInMap(t.rect);
                if (Vector2.Distance(rt.anchoredPosition, goal) > attackRange)
                    rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, goal, speed * Time.deltaTime);
                else
                    DamageBuilding(t, kind.damage * Time.deltaTime);
            }

            // 2. Each tower hits one enemy per guard. Damage depends on the enemy's weakness.
            // 2. Each guard shoots one enemy. Damage depends on the guard's stat and the enemy's weakness.
            float perPoint = game.GetSetting("stat_damage_bonus", 0.1f);
            foreach (var t in activeGuards)
            {
                if (t.Key.occupant == "") continue;
                Vector2 towerPos = CenterInMap(t.Key.rect);
                var chosen = new List<int>();
                for (int shot = 0; shot < t.Value.Count; shot++)
                {
                    int best = -1;
                    float bestDist = game.GuardRadius;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (chosen.Contains(i)) continue;
                        float d = Vector2.Distance(enemies[i].rectTransform.anchoredPosition, towerPos);
                        if (d <= bestDist) { bestDist = d; best = i; }
                    }
                    if (best == -1) break;
                    chosen.Add(best);
                    float statBonus = 1f + t.Value[shot] * perPoint;
                    enemyHealth[best] -= game.GetSetting("tower_damage", towerDamage) * statBonus * DamageMultiplier(enemyKinds[best], t.Key.occupant) * Time.deltaTime;
                }
            }

            // 3. Update labels, remove the dead and the ones that reached the HQ
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemyLabels[i] != null)
                    enemyLabels[i].text = Mathf.CeilToInt(Mathf.Max(0, enemyHealth[i])) + "/" + Mathf.CeilToInt(enemyMax[i]);

                bool dead = enemyHealth[i] <= 0;
                bool arrived = enemyTargets[i] == null && Vector2.Distance(enemies[i].rectTransform.anchoredPosition, hqPos) < 5f;
                if (!dead && !arrived) continue;

                Destroy(enemies[i].gameObject);
                enemies.RemoveAt(i);
                enemyKinds.RemoveAt(i);
                enemyHealth.RemoveAt(i);
                enemyMax.RemoveAt(i);
                enemyLabels.RemoveAt(i);
                enemyTargets.RemoveAt(i);
                if (dead) game.RaiderKilled();
                else game.RaiderArrived();
            }
        }

        // A small outlined label attached to an enemy: above = name over the head, otherwise health under the feet
        Text MakeEnemyText(Transform parent, string objectName, Font font, bool above)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, above ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, above ? 0f : 1f);
            rt.anchoredPosition = new Vector2(0f, above ? 2f : -2f);
            rt.sizeDelta = new Vector2(160f, 22f);

            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = 16;
            t.color = Color.white;
            t.alignment = above ? TextAnchor.LowerCenter : TextAnchor.UpperCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            go.GetComponent<Outline>().effectColor = Color.black;   // keeps the text readable on the sand background
            return t;
        }

        // Only enemy types whose first_raid has been reached can spawn
        EnemyType PickEnemyType()
        {
            var pool = game.EnemyTypes.FindAll(e => e.firstRaid <= game.RaidsDone);
            if (pool.Count == 0) pool = game.EnemyTypes;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        float DamageMultiplier(EnemyType kind, string towerType)
        {
            if (kind == null || kind.weakTo == "") return 1f;
            return kind.weakTo.Equals(towerType, StringComparison.OrdinalIgnoreCase) ? kind.weakMultiplier : 1f;
        }
        float MaxHp() { return Mathf.Max(1f, game.GetSetting("building_max_health", 20f)); }

        // The closest placed building to this point. Returns null if the HQ is closer (or nothing is placed).
        Slot PickTarget(Vector2 from)
        {
            Slot best = null;
            float bestDist = Vector2.Distance(from, CenterInMap(hq));
            foreach (var s in slots)
            {
                if (s.occupant == "") continue;
                float d = Vector2.Distance(from, CenterInMap(s.rect));
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        void DamageBuilding(Slot s, float amount)
        {
            if (s.occupant == "") return;
            s.hp -= amount;
            if (s.hp > 0) return;
            string name = DestroySlot(s);
            if (name != "") game.Notify(name + " was destroyed by raiders!");
        }

        // Which map slot belongs to this Workplace? (the reverse of WorkplaceFor)
        Slot SlotFor(Workplace w)
        {
            int index = 0;
            foreach (var wp in game.Prison.workplaces)
            {
                if (wp == w) break;
                if (wp.typeId.Equals(w.typeId, StringComparison.OrdinalIgnoreCase)) index++;
            }
            int seen = 0;
            foreach (var s in slots)
            {
                if (!s.occupant.Equals(w.typeId, StringComparison.OrdinalIgnoreCase)) continue;
                if (seen == index) return s;
                seen++;
            }
            return null;
        }

        public string HpText(Workplace w)
        {
            Slot s = SlotFor(w);
            return s == null ? "" : Mathf.CeilToInt(s.hp) + "/" + Mathf.CeilToInt(MaxHp());
        }

        public bool CanRepair(Workplace w)
        {
            Slot s = SlotFor(w);
            return s != null && s.hp > 0 && s.hp < MaxHp() - 0.01f;
        }

        public string RepairText(Workplace w)
        {
            Slot s = SlotFor(w);
            if (s == null) return "REPAIR";
            float missing = 1f - s.hp / MaxHp();
            if (missing <= 0.001f) return "FULL HP";
            return "REPAIR\n" + Mathf.CeilToInt(game.RepairCostFor(s.occupant, missing)) + " " + game.RepairCurrencyName(s.occupant);
        }

        // 0 = full health, 1 = nearly destroyed, -1 = no slot found
        public float MissingFraction(Workplace w)
        {
            Slot s = SlotFor(w);
            if (s == null || s.occupant == "") return -1f;
            return Mathf.Clamp01(1f - s.hp / MaxHp());
        }

        // The REPAIR button in the building window calls this (through ClickerGame)
        public void Repair()
        {
            Slot s = game.SelectedWorkplace == null ? null : SlotFor(game.SelectedWorkplace);
            if (s == null || s.occupant == "") return;
            float max = MaxHp();
            float missing = 1f - s.hp / max;
            if (missing <= 0.001f) { game.Notify("Already at full health."); return; }
            if (!game.TrySpendRepair(s.occupant, missing)) return;
            s.hp = max;
            Save();
            game.Notify(game.BuildingName(s.occupant) + " repaired.");
        }

        // How many guards have arrived and are working in the Nth building of this type
        // The main-stat value of every guard who has arrived at the Nth building of this type (one entry per shot)
        List<float> ShooterStats(PrisonManager prison, string typeId, int index)
        {
            var stats = new List<float>();
            if (prison == null) return stats;
            int n = 0;
            foreach (var w in prison.workplaces)
            {
                if (!w.typeId.Equals(typeId, StringComparison.OrdinalIgnoreCase)) continue;
                if (n == index)
                {
                    foreach (var p in w.workers)
                        if (p.travelLeft <= 0) stats.Add(p.GetStat(w.mainStat));   // still walking = not shooting yet
                    return stats;
                }
                n++;
            }
            return stats;
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
            if (enemyTemplate != null) enemyTemplate.gameObject.SetActive(false);
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
                var hover = marker.gameObject.AddComponent<MapHover>();
                hover.game = game;
                hover.getText = () => SlotTooltip(captured);
            }
            Restore();

            if (hq != null)
            {
                Button hqButton = hq.GetComponent<Button>();
                if (hqButton == null) hqButton = hq.gameObject.AddComponent<Button>();
                if (hqImage != null) { hqImage.raycastTarget = true; hqButton.targetGraphic = hqImage; }
                hqButton.onClick.AddListener(ClickedHq);

                var hqHover = hq.gameObject.AddComponent<MapHover>();
                hqHover.game = game;
                hqHover.getText = HqTooltip;
            }
        }

        string SlotTooltip(Slot s)
        {
            if (s.occupant == "")
            {
                string t = "Empty slot";
                if (s.allowed != "") t += "\nOnly takes: " + game.BuildingName(s.allowed);
                return t + "\nClick to choose a building.";
            }

            Workplace w = WorkplaceFor(s);
            if (w == null) return game.BuildingName(s.occupant);

            PrisonManager prison = game.Prison;
            string text = w.name
                + "\nHP " + Mathf.CeilToInt(s.hp) + "/" + Mathf.CeilToInt(MaxHp())
                + "\n" + w.mainStat + "   Workers " + w.workers.Count + "/" + w.capacity
                + "\n+" + w.SuppliesPerSecond().ToString("0.0") + "/s"
                + "\nTension " + w.tension.ToString("0.0") + "/" + prison.tensionThreshold.ToString("0")
                + "\nConsumption: " + game.ConsumptionText(w);

            if (w.isGuard)
            {
                text += "\nGuard power " + w.GuardPower().ToString("0.00");
                foreach (var e in game.EnemyTypes)
                    if (e.weakTo.Equals(w.typeId, StringComparison.OrdinalIgnoreCase))
                        text += "\nExtra damage to " + e.name + " x" + e.weakMultiplier.ToString("0.#");
            }
            if (!w.isGuard && !w.IsStaffed()) text += "\nIDLE: no workers";
            if (!w.isGuard && prison.Efficiency(w.typeId) < 1f) text += "\nSHORT ON INPUTS";

            if (w.workers.Count > 0) text += "\n";
            foreach (var p in w.workers)
                text += "\n" + p.Label() + " " + p.faction + "  STR " + p.strength + " AGI " + p.agility + " INT " + p.intellect;
            return text;
        }

        string HqTooltip()
        {
            string text = "Headquarters\nWaiting prisoners: " + game.Prison.unassigned.Count + "\n" + game.RaidStatus() + "\n";
            foreach (var e in game.EnemyTypes)
            {
                if (e.firstRaid > game.RaidsDone) continue;
                text += "\n" + e.name + ": weak to " + (e.weakTo == "" ? "nothing" : game.BuildingName(e.weakTo));
            }
            return text;
        }

        void ClickedHq()
        {
            pendingSlot = null;                 // from the earlier empty-slot change; delete this line if you didn't add it
            game.SelectedWorkplace = null;
            if (tabs != null) tabs.Show(prisonersPage);   // the waiting prisoners list
        }

        void Clicked(Slot s)
        {
            if (s.occupant != "")
            {
                pendingSlot = null;
                OpenBuilding(s);
                return;
            }
            string armed = game.ArmedBuilding;
            if (string.IsNullOrEmpty(armed))
            {
                // No building chosen yet: remember this slot and open the shop tab
                pendingSlot = s;
                if (tabs != null) tabs.Show(buildingsPage);
                string only = s.allowed != "" ? " (this slot only takes " + game.BuildingName(s.allowed) + ")" : "";
                game.Notify("Pick a building to place here." + only);
                return;
            }
            if (s.allowed != "" && !s.allowed.Equals(armed, StringComparison.OrdinalIgnoreCase))
            { game.Notify("This slot only takes: " + game.BuildingName(s.allowed) + "."); return; }

            if (game.TryPlaceBuilding(armed))
            {
                s.occupant = armed;
                game.ArmedBuilding = "";
                Save();
            }
        }

        // Clicking a building on the map: assign the selected prisoner (if any) and show its details
        void OpenBuilding(Slot s)
        {
            Workplace place = WorkplaceFor(s);
            if (place == null) return;   // just placed, the workplace isn't created yet
            openSlot = s;

            Prisoner waiting = game.SelectedPrisoner;
            if (waiting != null)
            {
                if (game.Prison.Assign(waiting, place)) game.SelectedPrisoner = null;
                else game.Notify(place.name + " is full.");
            }
            game.SelectedWorkplace = place;
            if (tabs != null) tabs.Show(buildingInfoPage);
        }

        // Called by the SELL button in the building window (this is the old sell code from Clicked)
        public void AskSell()
        {
            if (openSlot == null || openSlot.occupant == "") return;
            if (confirmPanel == null)   // no popup set up: sell straight away
            {
                if (game.SellPlacedBuilding(openSlot.occupant)) { openSlot.occupant = ""; game.SelectedWorkplace = null; Save(); }
                return;
            }
            pendingSell = openSlot;
            if (confirmText != null)
                confirmText.text = "Demolish " + game.BuildingName(openSlot.occupant) + " for " + game.RefundFor(openSlot.occupant).ToString("0.#") + " supplies?";
            confirmPanel.SetActive(true);
        }

        // Destroys a random placed building (blank typeId = any). Returns its name, or "" if there was none.
        public string DestroyRandomBuilding(string typeId)
        {
            var candidates = new List<Slot>();
            foreach (var s in slots)
            {
                if (s.occupant == "") continue;
                if (typeId != "" && !s.occupant.Equals(typeId, StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add(s);
            }
            if (candidates.Count == 0) return "";
            return DestroySlot(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }

        // Destroys the building in this slot (no refund). Returns its name, or "" if it failed.
        string DestroySlot(Slot hit)
        {
            string type = hit.occupant;
            if (type == "") return "";
            string buildingName = game.BuildingName(type);
            Workplace place = WorkplaceFor(hit);   // work this out before the slot is cleared

            if (!game.DestroyBuilding(type)) return "";
            if (place != null) game.Prison.RemoveWorkplace(place);
            hit.occupant = "";
            hit.hp = 0;
            if (openSlot == hit) openSlot = null;
            if (game.SelectedWorkplace == place) game.SelectedWorkplace = null;
            Save();
            return buildingName;
        }

        // Which Workplace (the object that holds the workers) belongs to this map slot?
        // The 1st Farm slot is the 1st Farm workplace, the 2nd Farm slot is the 2nd one, and so on.
        Workplace WorkplaceFor(Slot target)
        {
            int index = 0;
            foreach (var s in slots)
            {
                if (s == target) break;
                if (s.occupant.Equals(target.occupant, StringComparison.OrdinalIgnoreCase)) index++;
            }
            int seen = 0;
            foreach (var w in game.Prison.workplaces)
            {
                if (!w.typeId.Equals(target.occupant, StringComparison.OrdinalIgnoreCase)) continue;
                if (seen == index) return w;
                seen++;
            }
            return null;
        }

        void ConfirmSell()
        {
            if (pendingSell != null && pendingSell.occupant != "")
            {
                if (game.SellPlacedBuilding(pendingSell.occupant)) { pendingSell.occupant = ""; game.SelectedWorkplace = null; Save(); }
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
            foreach (var s in slots) if (s.occupant != "") parts.Add(s.id + "=" + s.occupant + "@" + s.hp.ToString("0.#", CultureInfo.InvariantCulture));
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
                if (s == null) continue;
                string occupant = p[1];
                float hp = 0;
                int at = occupant.IndexOf('@');
                if (at >= 0)
                {
                    float.TryParse(occupant.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out hp);
                    occupant = occupant.Substring(0, at);
                }
                s.occupant = occupant;
                s.hp = hp;
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

        // If the player clicked an empty slot and has now picked a building in the shop, place it there.
        void PlacePending()
        {
            if (pendingSlot == null) return;
            string armed = game.ArmedBuilding;
            if (string.IsNullOrEmpty(armed)) return;   // still choosing

            Slot s = pendingSlot;
            pendingSlot = null;
            if (s.occupant != "") return;

            if (s.allowed != "" && !s.allowed.Equals(armed, StringComparison.OrdinalIgnoreCase))
            {
                game.Notify("This slot only takes: " + game.BuildingName(s.allowed) + ".");
                game.ArmedBuilding = "";
                return;
            }
            if (game.TryPlaceBuilding(armed)) { s.occupant = armed; Save(); }
            game.ArmedBuilding = "";
        }
    }
}