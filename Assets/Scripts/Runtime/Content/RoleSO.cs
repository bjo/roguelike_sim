using UnityEngine;
using System;

namespace RoguelikeSim.Content {
  [Serializable]
  public struct PerResourceValue {
    public ResourceType Type;
    public int Amount;
  }

  [CreateAssetMenu(menuName:"Content/Role")]
  public class RoleSO : ScriptableObject {
    public string Id;
    public string Label;
    public string EraUnlock; // optional
    public string[] Tags; // e.g., "agriculture", "industry"

    // Per worker, per turn
    public PerResourceValue[] Production;
  }
}
