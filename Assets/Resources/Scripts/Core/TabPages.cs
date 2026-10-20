using UnityEngine;
using UnityEngine.UI;

namespace COMP2165.Clicker
{
    public class TabPages : MonoBehaviour
    {
        public Button[] tabs;         // Prisoners, Buildings, Upgrade
        public GameObject[] pages;    // PrisonerPage, BuildingsPage, UpgradePage (same order)
        public int startPage = 1;     // 0 = Prisoners, 1 = Buildings, 2 = Upgrade

        void Awake()
        {
            Show(startPage);
        }

        public void Show(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                if (i < tabs.Length) tabs[i].interactable = i != index;   // the open tab greys out
            }
        }
    }
}