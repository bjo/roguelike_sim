using System;
using System.Collections.Generic;
using RoguelikeSim.Content;

namespace RoguelikeSim.Core {
  [Serializable]
  public struct RoleAssignment {
    public string RoleId; // reference to RoleSO by Id
    public int Workers;   // number of population assigned
  }

  [Serializable]
  public struct BuildingInstance {
    public string BuildingId; // reference to BuildingSO by Id
    public int WorkersAssigned; // must be <= capacity defined by BuildingSO
  }

  [Serializable]
  public class CityModel {
    public string Id;
    public string Label;
    public int Population;
    public List<string> Tags = new List<string>();

    // Assignment of population to roles
    public List<RoleAssignment> RoleAssignments = new List<RoleAssignment>();

    // Infrastructure present in the city
    public List<BuildingInstance> Buildings = new List<BuildingInstance>();
  }
}
