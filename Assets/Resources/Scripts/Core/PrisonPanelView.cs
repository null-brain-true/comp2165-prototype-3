using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace COMP2165.Clicker
{
    // Fills the prison panel you build in Unity with prisoners and buildings,
    // using the same row style as the template's Buildings and Upgrades shop.
    // Put this script on your Prison Panel, then drag the objects into its slots in the Inspector.
    public class PrisonPanelView : MonoBehaviour
    {
        public Text summaryText;               // optional: one line of totals
        public Button orderButton;             // the "Order prisoner" button
        public Text orderButtonText;           // the Text inside the order button
        public RectTransform waitingContent;   // the Content object of the waiting prisoners scroll area
        public RectTransform buildingContent;  // the Content object of the buildings scroll area
        public ClickerShopRow waitingTemplate;     // a copy of the shop's "Building Row TEMPLATE" (switched off)
        public ClickerShopRow assignedTemplate;

        ClickerGame game;
        PrisonManager prison;
        Prisoner selected;          // the waiting prisoner the player clicked
        bool setUp = false;
        bool dirty = true;          // true when the lists need rebuilding
        int shownVersion = -1;      // the prison.version the lists were last built for

        List<GameObject> rows = new List<GameObject>();            // every row currently on screen
        List<Workplace> headerPlaces = new List<Workplace>();      // building rows, so their text can update
        List<ClickerShopRow> headerRows = new List<ClickerShopRow>();

        void Awake()
        {
            if (waitingTemplate != null) waitingTemplate.gameObject.SetActive(false);
            if (assignedTemplate != null) assignedTemplate.gameObject.SetActive(false);
            if (orderButton != null) orderButton.onClick.AddListener(OrderClicked);
        }

        // ClickerGame calls this every frame.
        public void Refresh(ClickerGame game, PrisonManager prison)
        {
            this.game = game;
            this.prison = prison;

            // While the panel is hidden there is nothing to draw. It catches up as soon as it is shown.
            if (!gameObject.activeInHierarchy) return;

            if (waitingTemplate == null || assignedTemplate == null || orderButton == null || orderButtonText == null || waitingContent == null || buildingContent == null)
            { Debug.LogError("PrisonPanelView has an empty Inspector slot."); return; }

            if (!setUp)
            {
                setUp = true;
            }

            // Only rebuild the rows when something changed.
            if (dirty || prison.version != shownVersion)
            {
                Rebuild();
                dirty = false;
                shownVersion = prison.version;
            }

            UpdateTexts();
        }

        void OrderClicked()
        {
            if (game == null || prison == null) { Debug.LogWarning("Order clicked before the game was ready."); return; }
            float cost = prison.PrisonerCost();
            bool paid = game.TrySpend(cost);
            Debug.Log("Order prisoner: cost " + cost + ", paid " + paid);
            if (paid) prison.OrderPrisoner();
        }

        void AssignHere(Workplace place)
        {
            if (selected == null) return;
            if (prison.Assign(selected, place)) selected = null;   // false means the building is full
            dirty = true;
        }

        void Rebuild()
        {
            for (int i = 0; i < rows.Count; i++) Destroy(rows[i]);
            rows.Clear();
            headerPlaces.Clear();
            headerRows.Clear();

            // Waiting prisoners. The SELECT button picks one.
            // "() => { ... }" is a small inline method that runs when the button is clicked.
            for (int i = 0; i < prison.unassigned.Count; i++)
            {
                Prisoner p = prison.unassigned[i];
                ClickerShopRow row = AddRow(waitingTemplate, waitingContent);
                ShowPrisoner(row, p);
                row.actionText.text = (p == selected) ? "SELECTED" : "SELECT";
                row.action.onClick.AddListener(() => { selected = p; dirty = true; });
            }

            // Each building, followed by the prisoners working in it.
            for (int i = 0; i < prison.workplaces.Count; i++)
            {
                Workplace place = prison.workplaces[i];

                // The ASSIGN button puts the selected prisoner into this building.
                ClickerShopRow header = AddRow(assignedTemplate, buildingContent);
                header.icon.texture = null;
                header.icon.color = new Color(0.35f, 0.75f, 0.4f);
                header.actionText.text = "ASSIGN";
                header.action.onClick.AddListener(() => AssignHere(place));
                headerPlaces.Add(place);
                headerRows.Add(header);

                // The REMOVE button sends a worker back to the waiting list.
                for (int j = 0; j < place.workers.Count; j++)
                {
                    Prisoner worker = place.workers[j];
                    ClickerShopRow workerRow = AddRow(assignedTemplate, buildingContent);
                    ShowPrisoner(workerRow, worker);
                    workerRow.actionText.text = "REMOVE";
                    workerRow.action.onClick.AddListener(() => { prison.Unassign(worker, place); });
                }
            }
        }

        // Copies the row template into a list and gives it the shop's compact layout.
        ClickerShopRow AddRow(ClickerShopRow template, RectTransform parent)
        {
            ClickerShopRow row = Instantiate(template, parent);
            row.gameObject.SetActive(true);
            row.UseCompactLayout(16);
            rows.Add(row.gameObject);
            return row;
        }

        // Shows a prisoner: the icon is a square in their faction colour, the name is their faction.
        void ShowPrisoner(ClickerShopRow row, Prisoner p)
        {
            row.icon.texture = null;
            row.icon.color = FactionColor(p.faction);
            row.title.text = p.faction + "\nSTR " + p.strength + "  AGI " + p.agility + "\nINT " + p.intellect;
        }

        // Numbers that change every second (production, tension) are updated here without rebuilding.
        void UpdateTexts()
        {
            for (int i = 0; i < headerPlaces.Count; i++)
            {
                Workplace p = headerPlaces[i];
                ClickerShopRow row = headerRows[i];
                row.title.text = p.name
                    + "\n" + p.mainStat + "  " + p.workers.Count + "/" + p.capacity
                    + "\n+" + p.SuppliesPerSecond().ToString("0.0") + "/s"
                    + "\nTension " + p.tension.ToString("0.0") + "/" + prison.tensionThreshold.ToString("0");
                row.action.interactable = selected != null && p.workers.Count < p.capacity;
            }

            float cost = prison.PrisonerCost();
            orderButtonText.text = "ORDER PRISONER (" + cost.ToString("0") + ")";
            orderButton.interactable = game.Supplies() >= cost;

            if (summaryText != null)
            {
                summaryText.text = "Waiting: " + prison.unassigned.Count
                    + "   Workers: " + prison.WorkerCount()
                    + "   Production: +" + prison.TotalSuppliesPerSecond().ToString("0.0") + "/s";
            }
        }

        static Color FactionColor(Faction f)
        {
            if (f == Faction.Republic) return new Color(0.3f, 0.5f, 0.9f);    // blue
            if (f == Faction.Imperium) return new Color(0.85f, 0.3f, 0.3f);   // red
            return new Color(0.92f, 0.78f, 0.25f);                              // green (Federacy)
        }
    }
}