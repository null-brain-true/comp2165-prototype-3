using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace COMP2165.Clicker
{
    public class CostEntry { public string amount, name; public Texture2D icon; }
    public class ClickerShopRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RawImage icon;
        public Text title, description, actionText;
        public Button action;
        [HideInInspector] public ClickerTooltip tooltip;
        [HideInInspector] public string tooltipText;

        public bool UseTitleOnlyLayout(int fontSize)
        {
            bool changed = false;
            bool oldLayout = description != null && description.gameObject.activeSelf;
            if (oldLayout)
            {
                description.gameObject.SetActive(false);
                changed = true;
                var titleRect = title == null ? null : title.rectTransform;
                if (titleRect != null)
                {
                    titleRect.anchoredPosition = new Vector2(90f, -12f);
                    titleRect.sizeDelta = new Vector2(335f, 68f);
                }
            }
            if (title != null && title.fontSize < fontSize)
            {
                title.fontSize = fontSize;
                changed = true;
            }
            return changed;
        }

        public void UseCompactLayout(int fontSize)
        {
            if (description != null) description.gameObject.SetActive(false);
            var rect = transform as RectTransform;
            if (rect != null) rect.sizeDelta = new Vector2(299f, 88f);
            var layout = GetComponent<LayoutElement>();
            if (layout != null) layout.minHeight = layout.preferredHeight = 88f;
            SetRect(icon.rectTransform, 7f, 18f, 52f, 52f);
            SetRect(title.rectTransform, 65f, 8f, 116f, 72f);
            title.fontSize = fontSize;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 13;
            title.resizeTextMaxSize = fontSize;
            SetRect(action.GetComponent<RectTransform>(), 188f, 14f, 102f, 60f);
            SetRect(actionText.rectTransform, 4f, 3f, 94f, 54f);
            actionText.fontSize = 15;
            actionText.resizeTextForBestFit = true;
            actionText.resizeTextMinSize = 11;
            actionText.resizeTextMaxSize = 15;
        }

        static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Show(tooltipText, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Hide();
        }

        void OnDisable()
        {
            if (tooltip != null) tooltip.Hide();
        }

        class CostSlot { public GameObject root; public Text amount; public RawImage icon; }
        readonly List<CostSlot> costSlots = new List<CostSlot>();
        RectTransform costStrip;

        // Plain text on the button (LOCKED, OWNED, CANCEL...). Hides the icon strip.
        public void SetActionText(string text)
        {
            if (costStrip != null) costStrip.gameObject.SetActive(false);
            SetRect(actionText.rectTransform, 4f, 3f, 94f, 54f);
            actionText.text = text;
        }

        // "BUY" on top, then one [amount][icon] pair per cost underneath.
        public void SetCosts(string header, List<CostEntry> costs)
        {
            EnsureStrip();
            SetRect(actionText.rectTransform, 4f, 2f, 94f, 24f);
            actionText.text = header;
            costStrip.gameObject.SetActive(true);
            while (costSlots.Count < costs.Count) costSlots.Add(NewSlot());
            for (int i = 0; i < costSlots.Count; i++)
            {
                bool used = i < costs.Count;
                costSlots[i].root.SetActive(used);
                if (!used) continue;
                bool hasIcon = costs[i].icon != null;
                costSlots[i].icon.gameObject.SetActive(hasIcon);
                costSlots[i].icon.texture = costs[i].icon;
                costSlots[i].amount.text = hasIcon ? costs[i].amount : costs[i].amount + " " + costs[i].name;
            }
        }

        void EnsureStrip()
        {
            if (costStrip != null) return;
            var go = new GameObject("Cost Strip", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.layer = LayerMask.NameToLayer("UI");
            costStrip = go.GetComponent<RectTransform>();
            costStrip.SetParent(action.transform, false);
            costStrip.anchorMin = new Vector2(0f, 0f);
            costStrip.anchorMax = new Vector2(1f, 0f);
            costStrip.pivot = new Vector2(0.5f, 0f);
            costStrip.anchoredPosition = new Vector2(0f, 3f);
            costStrip.sizeDelta = new Vector2(-6f, 28f);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        CostSlot NewSlot()
        {
            var slot = new CostSlot();
            var root = new GameObject("Cost", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            root.layer = LayerMask.NameToLayer("UI");
            root.transform.SetParent(costStrip, false);
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            slot.root = root;

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text));
            textGo.layer = LayerMask.NameToLayer("UI");
            textGo.transform.SetParent(root.transform, false);
            slot.amount = textGo.GetComponent<Text>();
            slot.amount.font = actionText.font;
            slot.amount.fontSize = 14;
            slot.amount.color = actionText.color;
            slot.amount.alignment = TextAnchor.MiddleCenter;
            slot.amount.horizontalOverflow = HorizontalWrapMode.Overflow;
            slot.amount.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            iconGo.layer = LayerMask.NameToLayer("UI");
            iconGo.transform.SetParent(root.transform, false);
            slot.icon = iconGo.GetComponent<RawImage>();
            slot.icon.raycastTarget = false;
            var le = iconGo.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 20f;
            slot.icon.color = Color.white;
            return slot;
        }
    }
}
