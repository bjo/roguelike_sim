using UnityEngine;

namespace RoguelikeSim.Content {
  public enum EffectKind { ResourceAdd, ResourceMult, YieldAdd, ThresholdGate }

  [System.Serializable]
  public struct EffectModifier {
    public EffectKind Kind;
    public ResourceType Resource;
    public float Amount;
    public string Metric;   // "unity" or "entropy" for ThresholdGate
    public float Min;
    public float Max;
    public string OnFail;   // "crisis" | "block"
  }

  [CreateAssetMenu(menuName:"Content/Effect")]
  public class EffectSO : ScriptableObject {
    public string Id;
    public string Scope; // global | city | advisor | card
    public string[] Tags;
    public EffectModifier[] Modifiers;
    public string Duration; // instant | turns | permanent
    public int RemainingTurns;
  }
}


