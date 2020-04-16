using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Boss Module Manager — a KTANE mod that keeps track of which boss modules should ignore each other</summary>
public class BossModuleManager : MonoBehaviour, IDictionary<string, object>
{
    private string _settingsFile;
    private BossModuleSettings Settings;
    private Dictionary<string, object> _functions;

    private void Start()
    {
        name = "BossModuleManager";
        _functions = new Dictionary<string, object>
        {
            { "GetIgnoredModules", new Func<string, string[]>(GetIgnoredModules) },
            { "Refresh", new Action(Refresh) }
        };

        _settingsFile = Path.Combine(Path.Combine(Application.persistentDataPath, "Modsettings"), "BossModules.json");

        if (!File.Exists(_settingsFile))
            Settings = new BossModuleSettings();
        else
        {
            try
            {
                Settings = JsonConvert.DeserializeObject<BossModuleSettings>(File.ReadAllText(_settingsFile), new StringEnumConverter());
                if (Settings == null)
                    throw new Exception("Settings could not be read. Creating new Settings...");
                Debug.LogFormat(@"[BossModuleManager] Settings successfully loaded");
            }
            catch (Exception e)
            {
                Debug.LogFormat(@"[BossModuleManager] Error loading settings file:");
                Debug.LogException(e);
                Settings = new BossModuleSettings();
            }
        }

        Debug.LogFormat(@"[BossModuleManager] Service is active");
        Refresh();
    }

    private void Refresh()
    {
        StartCoroutine(Refresher());
    }

    private IEnumerator Refresher()
    {
        using (var http = UnityWebRequest.Get(Settings.SiteUrl))
        {
            // Request and wait for the desired page.
            yield return http.SendWebRequest();

            if (http.isNetworkError)
            {
                Debug.LogFormat(@"[BossModuleManager] Website {0} responded with error: {1}", Settings.SiteUrl, http.error);
                yield break;
            }

            if (http.responseCode != 200)
            {
                Debug.LogFormat(@"[BossModuleManager] Website {0} responded with code: {1}", Settings.SiteUrl, http.responseCode);
                yield break;
            }

            var allModules = JObject.Parse(http.downloadHandler.text)["KtaneModules"] as JArray;
            if (allModules == null)
            {
                Debug.LogFormat(@"[BossModuleManager] Website {0} did not respond with a JSON array at “KtaneModules” key.", Settings.SiteUrl, http.responseCode);
                yield break;
            }

            var ignoredModules = new Dictionary<string, string[]>();
            foreach (JObject module in allModules)
            {
                var ignoreList = module["Ignore"] as JArray;
                var name = module["Name"] as JValue;
                if (ignoreList != null && ignoreList.All(tok => tok is JValue && ((JValue) tok).Value is string) && name.Value is string)
                    ignoredModules[(string) name.Value] = ignoreList.Select(tok => (string) ((JValue) tok).Value).ToArray();
            }

            Debug.LogFormat(@"[BossModuleManager] List successfully loaded:{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, ignoredModules.Select(kvp => string.Format("[BossModuleManager] {0} => {1}", kvp.Key, string.Join(", ", kvp.Value))).ToArray()));
            Settings.IgnoredModules = ignoredModules;

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(_settingsFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile));
                File.WriteAllText(_settingsFile, JsonConvert.SerializeObject(Settings, Formatting.Indented, new StringEnumConverter()));
            }
            catch (Exception e)
            {
                Debug.LogFormat("[BossModuleManager] Failed to save settings file:");
                Debug.LogException(e);
            }
        }
    }

    private string[] GetIgnoredModules(string moduleName)
    {
        string[] ret;
        if (Settings.IgnoredModules.TryGetValue(moduleName, out ret))
        {
            Debug.LogFormat(@"[BossModuleManager] Request for {0}’s ignore list successful.", moduleName);
            return ret.ToArray();   // Take a copy of the list so that the caller doesn’t modify ours
        }

        Debug.LogFormat(@"[BossModuleManager] Request for {0}’s ignore list failed.", moduleName);
        return null;
    }

    object IDictionary<string, object>.this[string key]
    {
        get { return _functions[key]; }
        set { throw new NotSupportedException(); }
    }

    // Support read-only operations
    ICollection<string> IDictionary<string, object>.Keys { get { return _functions.Keys; } }
    ICollection<object> IDictionary<string, object>.Values { get { return _functions.Values; } }
    int ICollection<KeyValuePair<string, object>>.Count { get { return _functions.Count; } }
    bool ICollection<KeyValuePair<string, object>>.IsReadOnly { get { return true; } }
    bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item) { return ((ICollection<KeyValuePair<string, object>>) _functions).Contains(item); }
    bool IDictionary<string, object>.ContainsKey(string key) { return _functions.ContainsKey(key); }
    void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) { ((ICollection<KeyValuePair<string, object>>) _functions).CopyTo(array, arrayIndex); }
    IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() { return _functions.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return _functions.GetEnumerator(); }
    bool IDictionary<string, object>.TryGetValue(string key, out object value) { return _functions.TryGetValue(key, out value); }

    // Unsupported write operations
    void IDictionary<string, object>.Add(string key, object value) { throw new NotSupportedException(); }
    void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item) { throw new NotSupportedException(); }
    void ICollection<KeyValuePair<string, object>>.Clear() { throw new NotSupportedException(); }
    bool IDictionary<string, object>.Remove(string key) { throw new NotSupportedException(); }
    bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item) { throw new NotSupportedException(); }
}
