using System.Collections.Generic;
 
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
                        for (int j = 0; j < gone.workers.Count; j++) unassigned.Add(gone.workers[j]);
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
            return new Prisoner(faction, strength, agility, intellect);
        }
 
        // Moves a waiting prisoner into a workplace. Returns false if the workplace is full.
        public bool Assign(Prisoner p, Workplace place)
        {
            if (!place.AddPrisoner(p)) return false;
            unassigned.Remove(p);
            version++;
            return true;
        }
 
        // Moves a prisoner out of a workplace back to the waiting list.
        public void Unassign(Prisoner p, Workplace place)
        {
            place.RemovePrisoner(p);
            unassigned.Add(p);
            version++;
        }
 
        // ----- Every second -----
 
        // Call once per second. Returns the supplies produced this second.
        public float Tick()
        {
            float total = 0;
            for (int i = 0; i < workplaces.Count; i++)
            {
                total += workplaces[i].SuppliesPerSecond();
                workplaces[i].TickTension();
            }
            return total;
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
    }
}