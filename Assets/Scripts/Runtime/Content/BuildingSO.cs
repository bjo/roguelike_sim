using UnityEngine;
using System;

namespace RoguelikeSim.Content {
  [CreateAssetMenu(menuName:"Content/Building")]
  public class BuildingSO : ScriptableObject {
    public string Id;
    public string Label;
    public string EraUnlock; // optional
    public string[] Tags; // e.g., "agriculture", "industry"

    // Which role this building hosts
    public string RoleId;

    // Max workers per instance of this building
    public int Capacity = 1;
  }
}
