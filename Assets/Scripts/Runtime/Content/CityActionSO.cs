using UnityEngine;

namespace RoguelikeSim.Content {
  public enum ActionCategory { Growth, Build, Military, Trade, Knowledge, Unity, Governance, Expansion, Infrastructure, Diplomacy, Space }

  [System.Serializable] public struct Cost { public ResourceType Type; public int Amount; }
  [System.Serializable] public struct Delta { public ResourceType Type; public int Amount; }
  [System.Serializable] public struct Test { public string Metric; public int Dc; public string[] AdvantageTags; }
  [System.Serializable] public struct Project { public string Id; public string Label; public int ProgressRequired; public string ProgressPerTurnTag; }

  [CreateAssetMenu(menuName:"Content/CityAction")]
  public class CityActionSO : ScriptableObject {
    public string Id;
    public string Label;
    public ActionCategory Category;
    public string EraUnlock;
    public int ActionPointCost = 1;
    public int CooldownTurns;
    public Cost[] Costs;
    public Test[] Tests;
    public Delta[] ImmediateDeltas;
    public EffectSO[] AddEffects;
    public Project Project; // optional
    public string[] RequiresTags;
  }
}


