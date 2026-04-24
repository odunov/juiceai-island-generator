using System;
using UnityEngine;

namespace Islands.Prototype
{
    public static class PrototypePersistentInventory
    {
        private const string CurrencyKey = "Islands.Prototype.Inventory.Currency";
        private const string ResourcesKey = "Islands.Prototype.Inventory.Resources";

        private static bool loaded;
        private static int currency;
        private static int resources;

        public static event Action Changed;

        public static int Currency
        {
            get
            {
                EnsureLoaded();
                return currency;
            }
        }

        public static int Resources
        {
            get
            {
                EnsureLoaded();
                return resources;
            }
        }

        public static void AddCurrency(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureLoaded();
            currency = AddClamped(currency, amount);
            Save();
        }

        public static void AddResources(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureLoaded();
            resources = AddClamped(resources, amount);
            Save();
        }

        public static void ResetProgression()
        {
            loaded = true;
            currency = 0;
            resources = 0;
            Save();
        }

        public static void ReloadFromStorage()
        {
            loaded = false;
            EnsureLoaded();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            currency = Mathf.Max(0, PlayerPrefs.GetInt(CurrencyKey, 0));
            resources = Mathf.Max(0, PlayerPrefs.GetInt(ResourcesKey, 0));
            loaded = true;
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(CurrencyKey, currency);
            PlayerPrefs.SetInt(ResourcesKey, resources);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static int AddClamped(int current, int amount)
        {
            var total = (long)current + amount;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
