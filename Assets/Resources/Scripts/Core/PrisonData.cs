using System.Collections.Generic;

namespace COMP2165.Clicker
{
    // The three prisoner factions.
    public enum Faction { Republic, Imperium, Federacy }

    // The three prisoner stats (more can be added later).
    public enum StatType { Strength, Agility, Intellect }

    // One prisoner. Plain class, like a Java class with public fields.
    // Stats are whole numbers, at least 1 each. Together they add up to the total set in Settings.csv.
    public class Prisoner
    {
        public Faction faction;
        public int strength;
        public int agility;
        public int intellect;
        public float speed = 50f;
        public float travelLeft = 0f;
        public int number;         // shown as No. 042
        public string crime = "";  // picked from Crimes.csv

        public Prisoner(Faction faction, int strength, int agility, int intellect)
        {
            this.faction = faction;
            this.strength = strength;
            this.agility = agility;
            this.intellect = intellect;
        }

        // Look up one stat by its type.
        public int GetStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return strength;
                case StatType.Agility: return agility;
                default: return intellect;
            }
        }

        public string Label()
        {
            return "No. " + number.ToString("000");
        }
    }

    // How one kind of building behaves with prisoners (loaded from Workplaces.csv).
    // The id matches a row in Buildings.csv.
    public class BuildingType
    {
        public string id;
        public StatType mainStat;   // the stat that decides how well prisoners work here
        public float baseSupplies;  // supplies per second per worker
        public int capacity;        // max prisoners inside each one
        public string yieldCurrency = "";   // the currency workers here produce
    }

    // One building the player has bought, where prisoners are assigned to work.
    public class Workplace
    {
        // Placeholder balance numbers. Tune these when you playtest.
        const float SameFactionBonus = 1.25f;
        const float MixedFactionPenalty = 0.9f;
        const float StatToSupplies = 0.2f;
        const float TensionPerWorkerPerSecond = 0.01f;
        const float GuardPerWorker = 0.15f;
        const float GuardPerStrength = 0.03f;
        const float SameFactionTensionMultiplier = 2f;   // same-faction buildings rise this many times faster

        public string name;
        public string typeId;       // which building type (the id in Buildings.csv) this one is
        public StatType mainStat;   // the stat that decides how well prisoners work here
        public float baseSupplies;  // supplies per second per worker
        public int capacity;        // max prisoners inside
        public float tension;       // builds up over time
        public float guardFactor = 1f;   // 1 = unprotected, lower = better protected
        public bool isGuard;             // true for guard towers
        public string yieldCurrency = "";   // the currency this building's workers produce
        public float distance = 0f;   // map pixels from the HQ (display only for now)
        public List<Prisoner> workers = new List<Prisoner>();

        public Workplace(string name, StatType mainStat, float baseSupplies, int capacity)
        {
            this.name = name;
            this.mainStat = mainStat;
            this.baseSupplies = baseSupplies;
            this.capacity = capacity;
        }

        public bool AddPrisoner(Prisoner p)
        {
            if (workers.Count >= capacity) return false;
            workers.Add(p);
            return true;
        }

        public void RemovePrisoner(Prisoner p)
        {
            workers.Remove(p);
        }

        // True if there are 2 or more workers and they all share one faction.
        public bool AllSameFaction()
        {
            if (workers.Count < 2) return false;
            for (int i = 1; i < workers.Count; i++)
            {
                if (workers[i].faction != workers[0].faction) return false;
            }
            return true;
        }

        // True if there are 2 or more workers and at least two factions.
        public bool MixedFactions()
        {
            if (workers.Count < 2) return false;
            return !AllSameFaction();
        }

        public float SuppliesPerSecond()
        {
            if (isGuard) return 0;
            float total = 0;
            for (int i = 0; i < workers.Count; i++)
            {
                if (workers[i].travelLeft > 0) continue;   // still commuting
                total += baseSupplies + workers[i].GetStat(mainStat) * StatToSupplies;
            }
            if (AllSameFaction()) total *= SameFactionBonus;
            if (MixedFactions()) total *= MixedFactionPenalty;
            return total;
        }

        // Call once per second. Every worker adds a little base tension. Same-faction groups add extra.
        public void TickTension(bool starving = false)
        {
            if (isGuard || workers.Count == 0) return;

            float gain = workers.Count * TensionPerWorkerPerSecond;   // always rises
            if (AllSameFaction()) gain *= SameFactionTensionMultiplier;

            tension += gain * (starving ? 3f : 1f) * guardFactor;
        }

        public void ResetTension()
        {
            tension = 0;
        }

        public void TickTravel()
        {
            for (int i = 0; i < workers.Count; i++)
                if (workers[i].travelLeft > 0) workers[i].travelLeft -= 1f;
        }

        public int CommutingCount()
        {
            int n = 0;
            for (int i = 0; i < workers.Count; i++) if (workers[i].travelLeft > 0) n++;
            return n;
        }

        public bool IsStaffed()
        {
            for (int i = 0; i < workers.Count; i++)
                if (workers[i].travelLeft <= 0) return true;   // someone has arrived and is working
            return false;
        }

        public float GuardPower()
        {
            if (!isGuard) return 0;
            float total = 0;
            const float GuardPerStat = 0.03f;   // was GuardPerStrength
            for (int i = 0; i < workers.Count; i++)
            {
                if (workers[i].travelLeft > 0) continue;   // still walking there
                total += GuardPerWorker + workers[i].GetStat(mainStat) * GuardPerStat;
            }
            return total;
        }
    }
}
