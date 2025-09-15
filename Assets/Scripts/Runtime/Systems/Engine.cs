using System;
using System.Collections.Generic;
using System.Linq;
using RoguelikeSim.Core;

namespace RoguelikeSim.Systems {
  public static class Engine {
    public static void EnsureResourceKeys(GameModel state) {
      if (state.Resources == null) state.Resources = new Dictionary<string, int>();
      string[] keys = new [] { "food", "materials", "production", "knowledge", "unity", "gold" };
      foreach (var k in keys) if (!state.Resources.ContainsKey(k)) state.Resources[k] = 0;
    }

    public static void StepOneTurn(
      GameModel state,
      IEnumerable<RoguelikeSim.ContentData.RoleDef> roleDefs,
      IEnumerable<RoguelikeSim.ContentData.BuildingDef> buildingDefs
    ) {
      EnsureResourceKeys(state);

      var roleById = roleDefs.ToDictionary(r => r.id, r => r);
      var buildingById = buildingDefs.ToDictionary(b => b.id, b => b);

      var turnDelta = new Dictionary<string, int>();

      foreach (var city in state.Cities) {
        // Capacity per role from buildings present in this city
        var capacityByRole = new Dictionary<string, int>();
        foreach (var binst in city.Buildings) {
          if (!buildingById.TryGetValue(binst.BuildingId, out var bdef)) continue;
          if (string.IsNullOrEmpty(bdef.roleId)) continue;
          if (!capacityByRole.ContainsKey(bdef.roleId)) capacityByRole[bdef.roleId] = 0;
          capacityByRole[bdef.roleId] += Math.Max(0, bdef.capacity);
        }

        // Assigned workers per role (clamped by capacity and population budget)
        var assignedByRole = new Dictionary<string, int>();
        int totalAssigned = 0;
        foreach (var ra in city.RoleAssignments) {
          if (string.IsNullOrEmpty(ra.RoleId)) continue;
          int workers = Math.Max(0, ra.Workers);
          if (!assignedByRole.ContainsKey(ra.RoleId)) assignedByRole[ra.RoleId] = 0;
          assignedByRole[ra.RoleId] += workers;
          totalAssigned += workers;
        }

        // Enforce population cap
        if (totalAssigned > city.Population) {
          // Scale down proportionally
          float scale = city.Population <= 0 ? 0f : (float)city.Population / (float)totalAssigned;
          var keys = assignedByRole.Keys.ToList();
          foreach (var k in keys) assignedByRole[k] = (int)Math.Floor(assignedByRole[k] * scale);
        }

        // Clamp by building capacity per role
        var effectiveByRole = new Dictionary<string, int>();
        foreach (var kvp in assignedByRole) {
          int cap = capacityByRole.TryGetValue(kvp.Key, out var c) ? c : 0;
          effectiveByRole[kvp.Key] = Math.Min(kvp.Value, cap);
        }

        // Produce resources
        foreach (var kvp in effectiveByRole) {
          if (!roleById.TryGetValue(kvp.Key, out var role)) continue;
          int workers = kvp.Value;
          if (role.production == null) continue;
          foreach (var prod in role.production) {
            if (string.IsNullOrEmpty(prod.type)) continue;
            int amount = Math.Max(0, prod.amount) * workers;
            if (!turnDelta.ContainsKey(prod.type)) turnDelta[prod.type] = 0;
            turnDelta[prod.type] += amount;
          }
        }
      }

      // Apply turn delta
      foreach (var kvp in turnDelta) {
        if (!state.Resources.ContainsKey(kvp.Key)) state.Resources[kvp.Key] = 0;
        state.Resources[kvp.Key] += kvp.Value;
      }

      state.Turn++;
      state.EraTurn++;
    }
  }
}
