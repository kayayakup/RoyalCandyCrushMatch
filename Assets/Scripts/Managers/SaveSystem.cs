// ============================================================
// SaveSystem.cs  –  static JSON ↔ PlayerPrefs persistence
// ============================================================
using UnityEngine;

public static class SaveSystem
{
    private const string KEY = "MatchThree_Save_v3";

    public static void Save(SaveData data)
    {
        data.valid = true;
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static SaveData Load()
    {
        if (!PlayerPrefs.HasKey(KEY)) return null;
        try
        {
            var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(KEY));
            return (d != null && d.valid) ? d : null;
        }
        catch { return null; }
    }

    public static bool HasSave() => PlayerPrefs.HasKey(KEY);

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(KEY);
        PlayerPrefs.Save();
    }
}
