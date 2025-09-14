Developing with Unity (C#)

Prereqs

- Unity Hub + Unity 2022.3 LTS
- Windows build support (optional Mac/Linux)

Setup

1) Open the folder in Unity Hub → Add project from disk.
2) When prompted, install missing modules (TextMeshPro, Input System).
3) Addressables: Window → Asset Management → Addressables → Groups (we will add groups later).

Run

- Create an empty scene `Main` and add a bootstrap MonoBehaviour to initialize a basic `GameModel`.
- Press Play; no gameplay yet—this repo provides types and systems scaffolding.

Workflow

- Content: Create `ScriptableObject` assets under `Assets/Addressables/Content/...` for actions, advisors, crises, cards, era rules.
- Systems: Implement `Resolver.Apply` and a `YieldSystem` to process end-turn.
- UI: Build a simple scene with top resource bar, center node list, bottom action panel.

Next Steps

- Implement deterministic RNG and crisis resolver.
- Define Bronze content via SOs.
- Hook up a basic map (list of nodes) and an action panel per city.


