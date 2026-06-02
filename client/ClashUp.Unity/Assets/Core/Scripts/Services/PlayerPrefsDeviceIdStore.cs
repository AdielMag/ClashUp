using System;
using UnityEngine;

namespace ClashUp.Client.Core
{
    public sealed class PlayerPrefsDeviceIdStore : IDeviceIdStore
    {
        private const string Key = "clashup.deviceId";

        public string GetOrCreate()
        {
            var existing = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            var fresh = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(Key, fresh);
            PlayerPrefs.Save();
            return fresh;
        }
    }
}
