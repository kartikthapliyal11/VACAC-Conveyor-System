using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConveyorSystem
{
    [Serializable]
    public class PieceRecord
    {
        public int type;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class LayoutRecord
    {
        public List<PieceRecord> pieces = new List<PieceRecord>();
    }

    /// <summary>Saves / loads the conveyor layout as JSON in the user's app-data folder.</summary>
    public static class LayoutStorage
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "conveyor_layout.json");

        public static bool Save()
        {
            var data = new LayoutRecord();
            foreach (ConveyorPiece p in ConveyorPiece.Placed)
            {
                data.pieces.Add(new PieceRecord
                {
                    type = p.catalogIndex,
                    position = p.transform.position,
                    rotation = p.transform.rotation
                });
            }

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not save layout: " + e.Message);
                return false;
            }
        }

        public static LayoutRecord Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonUtility.FromJson<LayoutRecord>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not load layout: " + e.Message);
                return null;
            }
        }
    }
}
