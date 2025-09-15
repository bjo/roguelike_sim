using System;

namespace RoguelikeSim.ContentData {
  [Serializable]
  public struct ResourceEntry {
    public string type;
    public int amount;
  }

  [Serializable]
  public class RoleDef {
    public string id;
    public string label;
    public string eraUnlock;
    public string[] tags;
    public ResourceEntry[] production;
  }

  [Serializable]
  public class BuildingDef {
    public string id;
    public string label;
    public string eraUnlock;
    public string[] tags;
    public string roleId;
    public int capacity;
  }
}
