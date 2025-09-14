Unity + C# stack (solo-friendly)

Target

- Unity 2022/2023 LTS, 2D URP
- New Input System (mouse-first)
- Addressables for content packs and late-binding assets
- Newtonsoft.Json for saves/content (deterministic JSON)
- Classic MonoBehaviour architecture (no DOTS needed)

Project folders (high-level)

```
Assets/
  Scripts/
    Runtime/
      Core/            // GameModel, RNG, serialization, effect system
      Systems/         // Resolver, Yields, Era, Crisis, Actions, SaveLoad
      StateMachine/    // Boot, Menu, Run, EraIntro, Map, Node, Resolve, Rewards, WinLose
      Content/         // ScriptableObject definitions + loaders
      UI/              // Views, Presenters, ViewModels (MVU/MVC-ish)
    Editor/
      ContentImport/   // JSON → SO importers, validators
  Addressables/
    Content/           // Packs: actions, advisors, cards, crises, eras
  Art/
  Audio/
  Resources/          // Minimal bootstrap only
```

Core architecture

- Central immutable-ish GameModel (copy-on-write per decision) → enables undo/dev tooling and deterministic tests.
- Deterministic RNG: seed string per run → System.Random instance used only in resolver; avoid UnityEngine.Random in logic.
- State machine: Boot → Menu → Run → EraIntro → Map → Node → Resolve → Rewards → EraAdvance → Win/Lose.
- Single resolver: Apply(eventOrAction, choices, state) -> state handles costs, tests, RNG, outcomes, effects.

Data & content

- Use ScriptableObjects for stable, editor-friendly types; optionally support JSON import for bulk editing.
- Addressables group per content kind; reference by ID/key (not scene links).
- Content IDs are strings; link by ID to avoid scene coupling.

ScriptableObject definitions (examples)

```csharp
// Assets/Scripts/Runtime/Content/Resource.cs
public enum ResourceType { Food, Materials, Production, Knowledge, Unity, Gold }

// Assets/Scripts/Runtime/Content/Effects.cs
public enum EffectKind { ResourceAdd, ResourceMult, YieldAdd, ThresholdGate }

[System.Serializable]
public struct EffectModifier {
  public EffectKind Kind;
  public ResourceType Resource;
  public float Amount;    // or Factor for ResourceMult
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
  public string EraUnlock; // bronze..information
  public int ActionPointCost = 1;
  public int CooldownTurns;
  public Cost[] Costs;
  public Test[] Tests;
  public Delta[] ImmediateDeltas;
  public EffectSO[] AddEffects;
  public Project Project; // optional multi-turn
  public string[] RequiresTags;
}
```

Systems

- Yields: sum city bias + traits + effects → apply per end turn.
- Crisis: validate costs → apply → roll outcomes with seeded RNG → enqueue follow-ups if Unity gates crossed.
- Era: apply EraRule effects on enter; increase crisis intensity; refresh node tables.
- Actions: 1 per city per turn; resolve like crises (costs → tests → deltas/effects → projects progress).

Save/Load

- Use Newtonsoft.Json; keep model classes [Serializable] and DTOs separate from SOs.
- Save slots: run autosave; meta profile (unlocks) separate. Compress with GZip.
- Store under Application.persistentDataPath.

Testing

- Unity Test Framework: EditMode tests for resolver (pure functions). PlayMode tests for flow.
- Golden tests: fixed seed → assert event/outcome sequence.
- Validation tests: ID uniqueness, dangling references, basic schema checks.

UI/UX

- Single scene with stacked panels: Top bar (resources, era, entropy), Center (Map/Event card), Bottom (Choices/Tech).
- UGUI + TextMeshPro (fast) or UI Toolkit if preferred. Keep transitions simple.
- Small polish: DOTween/LeanTween for resource pips, shake on crises, subtle SFX.

Packages

- com.unity.inputsystem
- com.unity.addressables
- com.unity.localization (optional)
- com.unity.nuget.newtonsoft-json
- TextMeshPro (built-in)

CI & Ops

- GitHub Actions building Windows/macOS/Linux with -batchmode -nographics.
- Sentry + GameAnalytics for crash and balance.
- Steam Playtest or itch.io for rapid feedback.

Determinism tips

- Keep all game logic off MonoBehaviour Update; run in explicit steps from UI commands.
- Never use DateTime.Now/UnityEngine.Random for logic; pass RNG instance explicitly.
- Serialize only data (no Unity object refs) in saves.

Next steps

- Create Unity project with the above folder layout.
- Define SOs for Bronze content (actions, crises, advisors, cards, era rules).
- Implement the Resolver and Yields systems first; drive a minimal UI that can complete a Bronze-era slice.