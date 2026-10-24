using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace COMP2165.Clicker
{
    // Holds the building types, the workplaces the player owns and the prisoners,
    // and runs the prison economy.
    // Plain class (not a MonoBehaviour), so ClickerGame creates it with "new PrisonManager(...)".
    //
    // Buying buildings is done by the template's shop (Buildings.csv).
    // ClickerGame tells this class how many of each building are owned (SyncWorkplaces).
    public class PrisonManager
    {
        // ClickerGame sets these from Settings.csv after creating the manager.
        public float tensionThreshold = 5f;   // tension that triggers an event
        public float costGrowth = 0.15f;      // each extra prisoner costs this much more
        public float prisonerBaseCost = 25f;  // price of the first ordered prisoner
        public float defaultSpeed = 50f;
        public string guardTypeId = "watchtower";
        public string foodId = "food";   // starvation never cuts food output, so a famine can recover
        public bool starving = false;        // set each second by ClickerGame
        public float starvePenalty = 0.5f;   // production multiplier while starving
        public Dictionary<string, float> efficiency = new Dictionary<string, float>();   // per building type: 1 = has all inputs, 0 = has none

        public int prisonersOrdered = 0;

        // Goes up whenever buildings, prisoners or assignments change, so the panel knows to redraw.
        public int version = 0;

        // If something is wrong in a CSV file, this holds the message. Empty means all good.
        public string error = "";

        int statTotal;   // total stat points every prisoner gets

        public List<BuildingType> buildingTypes = new List<BuildingType>();   // from Workplaces.csv
        public List<Workplace> workplaces = new List<Workplace>();            // the ones the player owns
        public List<Prisoner> unassigned = new List<Prisoner>();              // prisoners waiting for a job

        // Reads Workplaces.csv (id, main_stat, base_supplies, capacity), then makes any starting prisoners.
        public PrisonManager(List<Dictionary<string, string>> typeRows, int startingPrisoners, int statTotal)
        {
            // Every stat needs at least 1 point, so the total can't be less than 3.
            if (statTotal < 3) statTotal = 3;
            this.statTotal = statTotal;

            foreach (Dictionary<string, string> row in typeRows)
            {
                BuildingType t = new BuildingType();
                t.id = Csv.Get(row, "id");
                string stat = Csv.Get(row, "main_stat");
                t.baseSupplies = Csv.Number(row, "base_supplies", 1);
                t.capacity = (int)Csv.Number(row, "capacity", 3);

                if (t.id == "") { error = "Workplaces.csv: every row needs an id (it must match an id in Buildings.csv)."; return; }
                if (!IsValidStat(stat)) { error = "Workplaces.csv: main_stat must be Strength, Agility or Intellect (" + t.id + ")."; return; }
                if (t.capacity < 1) { error = "Workplaces.csv: capacity must be at least 1 (" + t.id + ")."; return; }

                t.mainStat = ToStat(stat);
                buildingTypes.Add(t);
            }

            for (int i = 0; i < startingPrisoners; i++)
            {
                unassigned.Add(CreateRandomPrisoner());
            }
        }

        // ----- Buildings -----

        // True if Workplaces.csv has a row with this id.
        public bool HasType(string id)
        {
            return FindType(id) != null;
        }

        BuildingType FindType(string id)
        {
            for (int i = 0; i < buildingTypes.Count; i++)
            {
                if (buildingTypes[i].id.ToLower() == id.ToLower()) return buildingTypes[i];
            }
            return null;
        }

        // Makes the number of workplaces of this type match how many the player owns.
        // Call every frame from ClickerGame. If a building is sold, its workers go back to the waiting list.
        public void SyncWorkplaces(string typeId, string displayName, int owned)
        {
            BuildingType t = FindType(typeId);
            if (t == null) return;

            int have = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                if (workplaces[i].typeId == typeId) have++;
            }

            // Add new buildings the player has bought.
            while (have < owned)
            {
                have++;
                Workplace w = new Workplace(displayName + " " + have, t.mainStat, t.baseSupplies, t.capacity);
                w.typeId = typeId;
                w.yieldCurrency = t.yieldCurrency;
                w.isGuard = typeId.ToLower() == guardTypeId.ToLower();
                workplaces.Add(w);
                version++;
            }

            // Remove buildings the player has sold (newest first).
            while (have > owned)
            {
                for (int i = workplaces.Count - 1; i >= 0; i--)
                {
                    if (workplaces[i].typeId == typeId)
                    {
                        Workplace gone = workplaces[i];
                        for (int j = 0; j < gone.workers.Count; j++) { gone.workers[j].travelLeft = 0; unassigned.Add(gone.workers[j]); }
                        workplaces.RemoveAt(i);
                        version++;
                        break;
                    }
                }
                have--;
            }
        }

        // ----- Prisoners -----

        // Price of the next prisoner. Rises with each one you order.
        public float PrisonerCost()
        {
            return UnityEngine.Mathf.Ceil(prisonerBaseCost * UnityEngine.Mathf.Pow(1 + costGrowth, prisonersOrdered));
        }

        // Adds a new random prisoner to the waiting list. (The caller takes the supplies first.)
        public void OrderPrisoner()
        {
            prisonersOrdered++;
            unassigned.Add(CreateRandomPrisoner());
            version++;
        }

        // Makes one prisoner with a random faction and random stats.
        public Prisoner CreateRandomPrisoner()
        {
            // Random faction
            int pick = UnityEngine.Random.Range(0, 3);
            Faction faction = Faction.Federacy;
            if (pick == 0) faction = Faction.Republic;
            if (pick == 1) faction = Faction.Imperium;

            // Every stat starts at 1, then the remaining points are handed out one at a time.
            int strength = 1;
            int agility = 1;
            int intellect = 1;
            for (int i = 0; i < statTotal - 3; i++)
            {
                int stat = UnityEngine.Random.Range(0, 3);
                if (stat == 0) strength++;
                else if (stat == 1) agility++;
                else intellect++;
            }
            Prisoner p = new Prisoner(faction, strength, agility, intellect);
            p.speed = defaultSpeed;
            return p;
        }

        // Moves a waiting prisoner into a workplace. Returns false if the workplace is full.
        public bool Assign(Prisoner p, Workplace place)
        {
            if (!place.AddPrisoner(p)) return false;
            unassigned.Remove(p);
            p.travelLeft = p.speed > 0 ? place.distance / p.speed : 0;
            version++;
            return true;
        }

        // Moves a prisoner out of a workplace back to the waiting list.
        public void Unassign(Prisoner p, Workplace place)
        {
            place.RemovePrisoner(p);
            p.travelLeft = 0;
            unassigned.Add(p);
            version++;
        }

        // ----- Every second -----

        // Call once per second. Returns the amount produced this second, per currency id.
        public Dictionary<string, float> Tick()
        {
            Dictionary<string, float> output = new Dictionary<string, float>();
            for (int i = 0; i < workplaces.Count; i++)
            {
                Workplace w = workplaces[i];
                float made = w.SuppliesPerSecond();
                made *= Efficiency(w.typeId);
                if (starving && w.yieldCurrency != foodId) made *= starvePenalty;
                if (made > 0)
                {
                    float have;
                    output.TryGetValue(w.yieldCurrency, out have);
                    output[w.yieldCurrency] = have + made;
                }
                w.TickTension(starving);
                w.TickTravel();
            }
            return output;
        }

        // Returns the first workplace whose tension is over the threshold, or null if none.
        public Workplace FindAngryWorkplace()
        {
            for (int i = 0; i < workplaces.Count; i++)
            {
                if (workplaces[i].tension >= tensionThreshold) return workplaces[i];
            }
            return null;
        }

        // How many prisoners are currently working in a building.
        public int WorkerCount()
        {
            int total = 0;
            for (int i = 0; i < workplaces.Count; i++) total += workplaces[i].workers.Count;
            return total;
        }

        public int PrisonerCount()
        {
            return WorkerCount() + unassigned.Count;
        }

        // Total supplies per second across all workplaces (for showing on screen).
        public float TotalSuppliesPerSecond()
        {
            float total = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                total += workplaces[i].SuppliesPerSecond();
            }
            return total;
        }

        static bool IsValidStat(string text)
        {
            text = text.ToLower();
            return text == "strength" || text == "agility" || text == "intellect";
        }

        static StatType ToStat(string text)
        {
            text = text.ToLower();
            if (text == "strength") return StatType.Strength;
            if (text == "agility") return StatType.Agility;
            return StatType.Intellect;
        }

        public void SetDistanceAll(float d)
        {
            for (int i = 0; i < workplaces.Count; i++) workplaces[i].distance = d;
        }

        public void SetDistance(string typeId, int index, float d)
        {
            int n = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                if (workplaces[i].typeId != typeId) continue;
                if (n == index) { workplaces[i].distance = d; return; }
                n++;
            }
        }

        public void SetGuardAll(float f)
        {
            for (int i = 0; i < workplaces.Count; i++) workplaces[i].guardFactor = f;
        }

        public void SetGuard(string typeId, int index, float f)
        {
            int n = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                if (workplaces[i].typeId != typeId) continue;
                if (n == index) { workplaces[i].guardFactor = f; return; }
                n++;
            }
        }

        public float GuardPower(string typeId, int index)
        {
            int n = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                if (workplaces[i].typeId != typeId) continue;
                if (n == index) return workplaces[i].GuardPower();
                n++;
            }
            return 0;
        }

        // ----- Saving -----

        static string PrisonerToText(Prisoner p)
        {
            return (int)p.faction + "," + p.strength + "," + p.agility + "," + p.intellect
                + "," + p.speed.ToString("0.##", CultureInfo.InvariantCulture)
                + "," + p.travelLeft.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static Prisoner TextToPrisoner(string text)
        {
            string[] f = text.Split(',');
            int faction, s, a, n;
            if (f.Length < 4) return null;
            if (!int.TryParse(f[0], out faction) || !int.TryParse(f[1], out s) || !int.TryParse(f[2], out a) || !int.TryParse(f[3], out n)) return null;
            if (faction < 0 || faction > 2) return null;
            Prisoner p = new Prisoner((Faction)faction, s, a, n);
            float v;
            if (f.Length > 4 && float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) p.speed = v;
            if (f.Length > 5 && float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) p.travelLeft = v;
            return p;
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ordered=").Append(prisonersOrdered);

            sb.Append("|U=");
            for (int i = 0; i < unassigned.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(PrisonerToText(unassigned[i]));
            }

            for (int i = 0; i < workplaces.Count; i++)
            {
                Workplace w = workplaces[i];
                int index = 0;   // which Farm / Mine / etc. this is
                for (int k = 0; k < i; k++) if (workplaces[k].typeId == w.typeId) index++;

                sb.Append("|W=").Append(w.typeId).Append(':').Append(index).Append(':')
                  .Append(w.tension.ToString("0.##", CultureInfo.InvariantCulture)).Append(':');
                for (int j = 0; j < w.workers.Count; j++)
                {
                    if (j > 0) sb.Append(';');
                    sb.Append(PrisonerToText(w.workers[j]));
                }
            }
            return sb.ToString();
        }

        public void Load(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (string part in text.Split('|'))
            {
                if (part.StartsWith("ordered="))
                {
                    int n;
                    if (int.TryParse(part.Substring(8), out n)) prisonersOrdered = n;
                }
                else if (part.StartsWith("U="))
                {
                    unassigned.Clear();
                    foreach (string item in part.Substring(2).Split(';'))
                    {
                        if (item == "") continue;
                        Prisoner p = TextToPrisoner(item);
                        if (p != null) unassigned.Add(p);
                    }
                }
                else if (part.StartsWith("W="))
                {
                    string[] f = part.Substring(2).Split(':');
                    if (f.Length < 4) continue;
                    int index, seen = 0;
                    float tension;
                    if (!int.TryParse(f[1], out index)) continue;
                    float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out tension);

                    Workplace target = null;
                    for (int i = 0; i < workplaces.Count; i++)
                    {
                        if (workplaces[i].typeId != f[0]) continue;
                        if (seen == index) { target = workplaces[i]; break; }
                        seen++;
                    }

                    foreach (string item in f[3].Split(';'))
                    {
                        if (item == "") continue;
                        Prisoner p = TextToPrisoner(item);
                        if (p == null) continue;
                        if (target == null || !target.AddPrisoner(p)) unassigned.Add(p);   // no room: back to waiting
                    }
                    if (target != null) target.tension = tension;
                }
            }
            version++;
        }
        public void SetTypeYield(string typeId, string currencyId)
        {
            BuildingType t = FindType(typeId);
            if (t != null) t.yieldCurrency = currencyId;
        }

        public void SetEfficiency(string typeId, float value)
        {
            efficiency[typeId] = value;
        }

        public float Efficiency(string typeId)
        {
            float value;
            return efficiency.TryGetValue(typeId, out value) ? value : 1f;
        }

        // What each building type would make this second if it had all its inputs.
        public Dictionary<string, float> PotentialByType()
        {
            Dictionary<string, float> output = new Dictionary<string, float>();
            for (int i = 0; i < workplaces.Count; i++)
            {
                Workplace w = workplaces[i];
                float made = w.SuppliesPerSecond();
                if (starving && w.yieldCurrency != foodId) made *= starvePenalty;
                float have;
                output.TryGetValue(w.typeId, out have);
                output[w.typeId] = have + made;
            }
            return output;
        }

        // Share of this building type's buildings that have someone working inside (0 to 1)
        public float StaffedFraction(string typeId)
        {
            int total = 0, staffed = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                if (workplaces[i].typeId != typeId) continue;
                total++;
                if (workplaces[i].IsStaffed()) staffed++;
            }
            return total == 0 ? 0f : (float)staffed / total;
        }

        public Dictionary<string, float> OutputPerSecond()
        {
            Dictionary<string, float> output = new Dictionary<string, float>();
            for (int i = 0; i < workplaces.Count; i++)
            {
                Workplace w = workplaces[i];
                float made = w.SuppliesPerSecond();
                made *= Efficiency(w.typeId);
                if (starving && w.yieldCurrency != foodId) made *= starvePenalty;
                if (made <= 0) continue;
                float have;
                output.TryGetValue(w.yieldCurrency, out have);
                output[w.yieldCurrency] = have + made;
            }
            return output;
        }
    }
}