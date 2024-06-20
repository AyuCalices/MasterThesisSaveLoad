using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace SaveLoadCore
{
    public static class SaveLoadManager
    {
        private static readonly HashSet<(string, object)> SavableList = new HashSet<(string, object)>();
        
        public static void Save<T>(T data, string saveName = "/player.data", string savePath = "") where T : class
        {
            BinaryFormatter formatter = new BinaryFormatter();
            string path = Application.persistentDataPath + savePath + saveName;
            FileStream stream = new FileStream(path, FileMode.Create);

            formatter.Serialize(stream, data);
            stream.Close();
        }
    
        public static T Load<T>(string saveName = "/player.data", string savePath = "") where T : class 
        {
            string path = Application.persistentDataPath + savePath + saveName;
            if (File.Exists(path))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                FileStream stream = new FileStream(path, FileMode.Open);

                T data = formatter.Deserialize(stream) as T;
                stream.Close();
                return data;
            }
            else
            {
                Debug.LogError("Save file not found in " + path);
                return null;
            }
        }

        public static bool SaveExists(string saveName = "/player.data", string savePath = "")
        {
            return File.Exists(Application.persistentDataPath + savePath + saveName);
        }

        public static void RegisterSavable(string identifier, object obj)
        {
            if (SavableList.Add((nameof(obj), obj))) return;
            
            Debug.LogError($"Can't add {obj} twice!");
        }

        public static void UnregisterSavable(string identifier, object obj)
        {
            if (SavableList.Remove((identifier, obj))) return;
            
            Debug.LogError($"Can't remove {obj}, because it is not registered!");
        }
        
        public static void RegisterSavable(string identifier, int obj)
        {
            if (SavableList.Add((nameof(obj), obj))) return;
            
            Debug.LogError($"Can't add {obj} twice!");
        }
    }
}
