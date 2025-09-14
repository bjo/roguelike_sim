using System;
using System.Collections.Generic;

namespace RoguelikeSim.Core {
  [Serializable]
  public class GameModel {
    public string Seed = "seed";
    public int Turn = 0;
    public string Era = "bronze";
    public int EraTurn = 0;
    public float Entropy = 0f;
    public Dictionary<string, int> Resources = new Dictionary<string, int>();
  }
}


