using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AsteroidFragmentAutoAssigner : EditorWindow
{
    [MenuItem("Tools/Auto-Assign Asteroid Fragments")]
    public static void ShowWindow()
    {
        GetWindow<AsteroidFragmentAutoAssigner>("Asteroid Auto-Assign");
    }

    private void OnGUI()
    {
        GUILayout.Label("Automatically assigns Tier 2 and Tier 3 fragments to prefabs.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Auto-Assign All Asteroids", GUILayout.Height(40)))
        {
            AssignFragments();
        }
    }

    private static void AssignFragments()
    {
        // Find ALL prefabs in the entire project
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int updatedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Asteroid asteroid = prefab.GetComponent<Asteroid>();

            if (asteroid != null)
            {
                string cleanName = prefab.name.Replace("(Clone)", "").Trim();

                // Determine if this is a Tier 1 or Tier 2 asteroid based on its name
                if (!cleanName.Contains("_02_"))
                {
                    // It's likely a Tier 1 (e.g., Asteroid_Large_001)
                    // Find all Tier 2 fragments for it
                    List<GameObject> tier2List = new List<GameObject>();
                    string searchPrefix = $"{cleanName}_02_";

                    foreach (string otherGuid in guids)
                    {
                        string otherPath = AssetDatabase.GUIDToAssetPath(otherGuid);
                        GameObject otherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(otherPath);

                        if (otherPrefab.name.StartsWith(searchPrefix) && !otherPrefab.name.Contains("_03_"))
                        {
                            tier2List.Add(otherPrefab);
                        }
                    }

                    if (tier2List.Count > 0)
                    {
                        asteroid.tier2Fragments = tier2List.ToArray();
                        EditorUtility.SetDirty(asteroid);
                        updatedCount++;
                    }
                }
                else if (cleanName.Contains("_02_") && !cleanName.Contains("_03_"))
                {
                    // It's a Tier 2 (e.g., Asteroid_Large_001_02_001)
                    // Find all Tier 3 fragments for it
                    List<GameObject> tier3List = new List<GameObject>();
                    string searchPrefix = $"{cleanName}_03_"; // Note: this matches your naming in the screenshot

                    foreach (string otherGuid in guids)
                    {
                        string otherPath = AssetDatabase.GUIDToAssetPath(otherGuid);
                        GameObject otherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(otherPath);

                        if (otherPrefab.name.StartsWith(searchPrefix))
                        {
                            tier3List.Add(otherPrefab);
                        }
                    }

                    if (tier3List.Count > 0)
                    {
                        asteroid.tier3Fragments = tier3List.ToArray();
                        EditorUtility.SetDirty(asteroid);
                        updatedCount++;
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[Asteroid Setup] Successfully auto-assigned fragments for {updatedCount} prefabs!</color>");
    }
}