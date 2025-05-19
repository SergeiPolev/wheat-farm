using UnityEngine;
using Data;

public class PlayerPrefsJsonSaveSystem : ISaveSystem
{
    private readonly string KEY_DATA = "Data";

    public PlayerPrefsJsonSaveSystem()
    {
        KEY_DATA = Application.persistentDataPath + "Save.json";
        Debug.Log(KEY_DATA);
    }

    public void Save(PlayerProgress data)
    {
        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(KEY_DATA, json);
        PlayerPrefs.Save();
        Debug.Log("Save json");
    }

    public void RemoveData()
    {
        PlayerPrefs.DeleteKey(KEY_DATA);
    }

    public bool HasData()
    {
        return PlayerPrefs.HasKey(KEY_DATA);
    }

    public PlayerProgress Load()
    {
        string json = "";
        json = PlayerPrefs.GetString(KEY_DATA);
        /*
        using (var reader = new StreamReader(KEY_DATA))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                json += line;
            }
        }
        */
        if (string.IsNullOrEmpty(json))
        {
            return new PlayerProgress();
        }

        return JsonUtility.FromJson<PlayerProgress>(json);
    }
}
