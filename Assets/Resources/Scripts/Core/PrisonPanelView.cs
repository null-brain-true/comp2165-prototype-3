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
        public RectTransform buildingContent;  // the Content object of the building window (shows the building clicked on the map)
        public ClickerShopRow waitingTemplate;     // a copy of the shop's "Building Row TEMPLATE" (switched off)
        public ClickerShopRow assignedTemplate;

        ClickerGame game;
        PrisonManager prison;
        bool setUp = false;
        bool dirty = true;          // true when the lists need rebuilding
        int shownVersion = -1;      // the prison.version the lists were last built for
        Workplace shownWorkplace;   // the building the lists were last built for

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

            // If the clicked building was sold, forget it
            if (game.SelectedWorkplace != null && !prison.workplaces.Contains(game.SelectedWorkplace))
                game.SelectedWorkplace = null;

            // While the panel is hidden there is nothing to draw. It catches up as soon as it is shown.
            if (!gameObject.activeInHierarchy) return;

            if (waitingTemplate == null || assignedTemplate == null || orderButton == null || orderButtonText == null || waitingContent == null || buildingContent == null)
            { Debug.LogError("PrisonPanelView has an empty Inspector slot."); return; }

            if (!setUp)
            {
                setUp = true;
            }

            // Only rebuild the rows when something changed.
            if (dirty || prison.version != shownVersion || shownWorkplace != game.SelectedWorkplace)
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

        void Rebuild()
        {
            for (int i = 0; i < rows.Count; i++) Destroy(rows[i]);
            rows.Clear();
            headerPlaces.Clear();
            headerRows.Clear();

            // Waiting prisoners. The SELECT button picks one (click it again to un-pick).
            // Then click a building on the map to assign them.
            for (int i = 0; i < prison.unassigned.Count; i++)
            {
                Prisoner p = prison.unassigned[i];
                ClickerShopRow row = AddRow(waitingTemplate, waitingContent);
                ShowPrisoner(row, p);
                row.actionText.text = (p == game.SelectedPrisoner) ? "SELECTED" : "SELECT";
                row.action.onClick.AddListener(() => { game.SelectedPrisoner = (game.SelectedPrisoner == p) ? null : p; dirty = true; });
            }

            // The building clicked on the map, followed by the prisoners working in it.
            Workplace place = game.SelectedWorkplace;
            shownWorkplace = place;
            if (place == null)
            {
                ClickerShopRow hint = AddRow(assignedTemplate, buildingContent);
                hint.icon.enabled = false;
                hint.action.gameObject.SetActive(false);
                hint.title.rectTransform.anchoredPosition = new Vector2(10f, -8f);
                hint.title.rectTransform.sizeDelta = new Vector2(280f, 72f);
                hint.title.text = "Click a building on the map to see who works there.";
                return;
            }

            // The SELL button asks the map to sell this building.
            // Two buttons stacked on the right: ASSIGN puts the selected prisoner here, DEMOLISH asks the map to sell it.
            ClickerShopRow header = AddRow(assignedTemplate, buildingContent, 150f);
            header.icon.texture = game.BuildingIcon(place.typeId);
            header.icon.color = Color.white;

            Button repairButton = header.action;
            Text repairText = header.actionText;
            Button demolishButton = Instantiate(repairButton, repairButton.transform.parent);   // a copy of the same button
            Text demolishText = demolishButton.GetComponentInChildren<Text>(true);
            PlaceButton(repairButton, repairText, 188f, 14f, 102f, 56f);
            PlaceButton(demolishButton, demolishText, 188f, 78f, 102f, 56f);
            demolishText.text = "DEMOLISH";
            repairButton.onClick.AddListener(() => game.RepairSelected());
            demolishButton.onClick.AddListener(() => game.AskSellSelected());

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

        void AssignHere(Workplace place)
        {
            if (game.SelectedPrisoner == null) return;
            if (prison.Assign(game.SelectedPrisoner, place)) game.SelectedPrisoner = null;   // false means the building is full
            dirty = true;
        }

        // Puts a button x, y pixels from the top-left of its row, and fits its label inside it
        static void PlaceButton(Button button, Text label, float x, float y, float width, float height)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            label.rectTransform.anchoredPosition = new Vector2(4f, -3f);
            label.rectTransform.sizeDelta = new Vector2(width - 8f, height - 6f);
        }

        // Copies the row template into a list and gives it the shop's compact layout.
        ClickerShopRow AddRow(ClickerShopRow template, RectTransform parent, float height = 88f)
        {
            ClickerShopRow row = Instantiate(template, parent);
            row.gameObject.SetActive(true);
            row.UseCompactLayout(16);
            if (height > 88f)
            {
                var rt = (RectTransform)row.transform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
                var le = row.GetComponent<LayoutElement>();
                if (le != null) le.minHeight = le.preferredHeight = height;
                var t = row.title.rectTransform;
                t.sizeDelta = new Vector2(t.sizeDelta.x, height - 16f);
            }
            rows.Add(row.gameObject);
            return row;
        }

        // Shows a prisoner: the icon is a square in their faction colour, the name is their faction.
        void ShowPrisoner(ClickerShopRow row, Prisoner p)
        {
            Texture2D sprite = game.GetImage(SpriteKey(p.faction));
            row.icon.texture = sprite;
            row.icon.color = sprite != null ? Color.white : FactionColor(p.faction);
            row.title.text = p.Label() + "  " + p.faction + "\nSTR " + p.strength + "  AGI " + p.agility + "\nINT " + p.intellect + "\nCrime: " + p.crime;
            //row.tooltip = game.Tooltip;
            //row.tooltipText = p.Label() + "\n" + p.faction + "\nCrime: " + p.crime;
        }

        static string SpriteKey(Faction f)
        {
            if (f == Faction.Republic) return "rep";
            if (f == Faction.Imperium) return "imp";
            return "fed";
        }

        // Numbers that change every second (production, tension) are updated here without rebuilding.
        void UpdateTexts()
        {
            for (int i = 0; i < headerPlaces.Count; i++)
            {
                Workplace p = headerPlaces[i];
                ClickerShopRow row = headerRows[i];
                row.title.text = p.name
                    + "\nHP " + game.BuildingHpText(p)
                    + "\n" + p.mainStat + "  " + p.workers.Count + "/" + p.capacity
                    + "\n+" + p.SuppliesPerSecond().ToString("0.0") + "/s"
                    + "\nTension " + p.tension.ToString("0.0") + "/" + prison.tensionThreshold.ToString("0")
                    + "\nConsumption: " + game.ConsumptionText(p)
                    + (p.isGuard ? "\nGuard power " + p.GuardPower().ToString("0.00") : "")
                    + (!p.isGuard && !p.IsStaffed() ? "\nIDLE: no workers" : "")
                    + (!p.isGuard && prison.Efficiency(p.typeId) < 1f ? "\nSHORT ON INPUTS" : "");
                float missing = game.MissingHp(p);
                if (missing <= 0.001f)
                {
                    row.SetActionText(missing < 0 ? "REPAIR" : "FULL HP");
                    row.action.interactable = false;
                }
                else
                {
                    row.SetCosts("REPAIR", game.RepairCostEntries(p.typeId, missing));
                    row.action.interactable = game.CanAffordRepair(p.typeId, missing);
                }
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
            return new Color(0.92f, 0.78f, 0.25f);                              // yellow (Federacy)
        }
    }
}