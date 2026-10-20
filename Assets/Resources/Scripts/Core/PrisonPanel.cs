using UnityEngine;
 
namespace COMP2165.Clicker
{
    // TEMPORARY quick panel for ordering and assigning prisoners.
    // Drawn with Unity's simple OnGUI system, so there is nothing to set up in the editor.
    // It sits over the Upgrades column of the 1280 x 720 canvas, next to the Buildings shop.
    public static class PrisonPanel
    {
        // Panel position, in the same 1280 x 720 space as the canvas.
        const float PanelX = 923;
        const float PanelY = 114;
        const float PanelWidth = 333;
        const float PanelHeight = 580;
 
        static Prisoner selected;   // the waiting prisoner the player clicked
        static Vector2 waitingScroll;
        static Vector2 buildingScroll;
 
        // Clicks are stored here and applied at the start of the next draw.
        // (OnGUI breaks if the lists change while the buttons are being drawn.)
        static bool orderPrisoner;
        static Prisoner clickedWaiting;
        static Workplace assignTo;
        static Prisoner removePrisoner;
        static Workplace removeFrom;
 
        public static void Draw(ClickerGame game, PrisonManager prison)
        {
            // Apply last click's changes before drawing anything.
            if (Event.current.type == EventType.Layout) ApplyClicks(game, prison);
 
            // Scale so the panel lines up with the canvas at any window size.
            float scale = Screen.width / 1280f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.skin.label.fontSize = 15;
            GUI.skin.button.fontSize = 15;
 
            GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), "");
            GUILayout.BeginArea(new Rect(PanelX + 10, PanelY + 10, PanelWidth - 20, PanelHeight - 20));
 
            GUILayout.Label("Prison production: +" + prison.TotalSuppliesPerSecond().ToString("0.0") + " supplies/s");
 
            // Ordering prisoners
            float supplies = game.Supplies();
            float cost = prison.PrisonerCost();
            GUI.enabled = supplies >= cost;
            if (GUILayout.Button("Order prisoner  (" + cost.ToString("0") + " supplies)")) orderPrisoner = true;
            GUI.enabled = true;
 
            // Waiting prisoners
            GUILayout.Label("WAITING PRISONERS: " + prison.unassigned.Count + "  (click one, then pick a building)");
            waitingScroll = GUILayout.BeginScrollView(waitingScroll, GUILayout.Height(170));
            for (int i = 0; i < prison.unassigned.Count; i++)
            {
                Prisoner p = prison.unassigned[i];
                string text = Describe(p);
                if (p == selected) text = ">> " + text;
                if (GUILayout.Button(text)) clickedWaiting = p;
            }
            GUILayout.EndScrollView();
 
            // The buildings the player has bought in the shop
            GUILayout.Label("YOUR BUILDINGS: " + prison.workplaces.Count + "  (buy them in the shop)");
            buildingScroll = GUILayout.BeginScrollView(buildingScroll);
            for (int w = 0; w < prison.workplaces.Count; w++)
            {
                Workplace place = prison.workplaces[w];
                GUILayout.Label(place.name + " (" + place.mainStat + ")  "
                    + place.workers.Count + "/" + place.capacity + "  |  "
                    + place.SuppliesPerSecond().ToString("0.0") + "/s  |  tension "
                    + place.tension.ToString("0.0") + "/" + prison.tensionThreshold.ToString("0"));
 
                if (selected != null && place.workers.Count < place.capacity && GUILayout.Button("Assign selected prisoner here"))
                {
                    assignTo = place;
                }
 
                for (int j = 0; j < place.workers.Count; j++)
                {
                    if (GUILayout.Button("Remove: " + Describe(place.workers[j])))
                    {
                        removePrisoner = place.workers[j];
                        removeFrom = place;
                    }
                }
            }
            GUILayout.EndScrollView();
 
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
        }
 
        static void ApplyClicks(ClickerGame game, PrisonManager prison)
        {
            if (orderPrisoner)
            {
                if (game.TrySpend(prison.PrisonerCost())) prison.OrderPrisoner();
                orderPrisoner = false;
            }
            if (clickedWaiting != null)
            {
                selected = clickedWaiting;
                clickedWaiting = null;
            }
            if (assignTo != null)
            {
                if (selected != null && prison.Assign(selected, assignTo)) selected = null;
                assignTo = null;
            }
            if (removePrisoner != null)
            {
                prison.Unassign(removePrisoner, removeFrom);
                removePrisoner = null;
                removeFrom = null;
            }
        }
 
        static string Describe(Prisoner p)
        {
            return p.faction + "   STR " + p.strength + "  AGI " + p.agility + "  INT " + p.intellect;
        }
    }
}
