using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using RoguelikeSim.Core;
using RoguelikeSim.Systems;
using RoguelikeSim.ContentData;

namespace RoguelikeSim.EditorTools {
  public class SimSandboxWindow : EditorWindow {
    GameModel state = new GameModel();

    List<RoleDef> roles = new List<RoleDef>();
    List<BuildingDef> buildings = new List<BuildingDef>();

    Vector2 scroll;

    string projectRoot => Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);

    [MenuItem("RoguelikeSim/Sim Sandbox")] public static void ShowWindow() {
      GetWindow<SimSandboxWindow>(false, "Sim Sandbox", true);
    }

    void OnEnable() {
      Engine.EnsureResourceKeys(state);
    }

    void LoadJson<T>(string absolutePath, List<T> into) {
      if (!File.Exists(absolutePath)) {
        Debug.LogWarning($"Not found: {absolutePath}");
        return;
      }
      string json = File.ReadAllText(absolutePath);
      var arr = JsonHelper.FromJsonArray<T>(json);
      into.Clear();
      into.AddRange(arr);
    }

    void LoadAllContent() {
      LoadJson(Path.Combine(projectRoot, "content/roles/core.json"), roles);
      LoadJson(Path.Combine(projectRoot, "content/buildings/core.json"), buildings);
    }

    void OnGUI() {
      if (GUILayout.Button("Load Content")) LoadAllContent();

      GUILayout.Space(6);

      GUILayout.Label("Cities", EditorStyles.boldLabel);
      if (GUILayout.Button("Add City")) {
        state.Cities.Add(new CityModel { Id = System.Guid.NewGuid().ToString(), Label = $"City {state.Cities.Count+1}", Population = 5 });
      }

      scroll = GUILayout.BeginScrollView(scroll);
      for (int i = 0; i < state.Cities.Count; i++) {
        var city = state.Cities[i];
        GUILayout.BeginVertical("box");
        GUILayout.BeginHorizontal();
        city.Label = EditorGUILayout.TextField(city.Label);
        if (GUILayout.Button("X", GUILayout.Width(22))) { state.Cities.RemoveAt(i); GUILayout.EndHorizontal(); GUILayout.EndVertical(); break; }
        GUILayout.EndHorizontal();

        city.Population = EditorGUILayout.IntSlider("Population", city.Population, 0, 50);

        // Buildings
        GUILayout.Label("Buildings", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Building")) {
          if (buildings.Count > 0) city.Buildings.Add(new BuildingInstance { BuildingId = buildings[0].id, WorkersAssigned = 0 });
        }
        for (int b = 0; b < city.Buildings.Count; b++) {
          var bi = city.Buildings[b];
          GUILayout.BeginHorizontal();
          int selectedB = Mathf.Max(0, buildings.FindIndex(x => x.id == bi.BuildingId));
          string[] bLabels = buildings.Select(x => $"{x.label} ({x.capacity})").ToArray();
          selectedB = EditorGUILayout.Popup(selectedB, bLabels);
          if (buildings.Count > 0) bi.BuildingId = buildings[Mathf.Clamp(selectedB,0,buildings.Count-1)].id;
          bi.WorkersAssigned = EditorGUILayout.IntField("Workers", bi.WorkersAssigned, GUILayout.Width(200));
          if (GUILayout.Button("-", GUILayout.Width(22))) { city.Buildings.RemoveAt(b); GUILayout.EndHorizontal(); break; }
          GUILayout.EndHorizontal();
          city.Buildings[b] = bi;
        }

        // Role assignments
        GUILayout.Label("Role Assignments", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Role Assignment")) {
          if (roles.Count > 0) city.RoleAssignments.Add(new RoleAssignment { RoleId = roles[0].id, Workers = 0 });
        }
        for (int r = 0; r < city.RoleAssignments.Count; r++) {
          var ra = city.RoleAssignments[r];
          GUILayout.BeginHorizontal();
          int selectedR = Mathf.Max(0, roles.FindIndex(x => x.id == ra.RoleId));
          string[] rLabels = roles.Select(x => x.label).ToArray();
          selectedR = EditorGUILayout.Popup(selectedR, rLabels);
          if (roles.Count > 0) ra.RoleId = roles[Mathf.Clamp(selectedR,0,roles.Count-1)].id;
          ra.Workers = EditorGUILayout.IntField("Workers", ra.Workers, GUILayout.Width(200));
          if (GUILayout.Button("-", GUILayout.Width(22))) { city.RoleAssignments.RemoveAt(r); GUILayout.EndHorizontal(); break; }
          GUILayout.EndHorizontal();
          city.RoleAssignments[r] = ra;
        }

        state.Cities[i] = city;
        GUILayout.EndVertical();
      }
      GUILayout.EndScrollView();

      GUILayout.Space(6);
      GUILayout.Label("Controls", EditorStyles.boldLabel);
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Step Turn")) Engine.StepOneTurn(state, roles, buildings);
      if (GUILayout.Button("Run 10 Turns")) { for (int t=0;t<10;t++) Engine.StepOneTurn(state, roles, buildings); }
      GUILayout.EndHorizontal();

      GUILayout.Space(6);
      GUILayout.Label("Resources", EditorStyles.boldLabel);
      GUILayout.BeginVertical("box");
      foreach (var kv in state.Resources.ToList()) {
        GUILayout.Label($"{kv.Key}: {kv.Value}");
      }
      GUILayout.EndVertical();

      GUILayout.Label($"Turn: {state.Turn}  EraTurn: {state.EraTurn}");

      if (GUILayout.Button("Export State JSON")) {
        string path = EditorUtility.SaveFilePanel("Export GameModel", projectRoot, "state.json", "json");
        if (!string.IsNullOrEmpty(path)) {
          File.WriteAllText(path, JsonUtility.ToJson(state, true));
          EditorUtility.RevealInFinder(path);
        }
      }
    }
  }

  // Helper to parse top-level JSON arrays via JsonUtility
  public static class JsonHelper {
    public static T[] FromJsonArray<T>(string json) {
      string wrapped = "{\"items\":" + json + "}";
      var container = JsonUtility.FromJson<Wrapper<T>>(wrapped);
      return container.items ?? new T[0];
    }
    [System.Serializable]
    private class Wrapper<T> { public T[] items; }
  }
}
