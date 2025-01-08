using UnityEngine;
using System.Collections.Generic;

public class Corner {
    public Vector2 Coord { get; private set; }
    public List<Edge> Edges;
    public List<Zone> Zones;
    public List<Corner> Neighbors;

    public Corner(Vector2 p_coord) {
        Coord = p_coord;
        Edges = new List<Edge>();
        Zones = new List<Zone>();
        Neighbors = new List<Corner>();
    }

    public static bool operator ==(Corner first, Corner second) {
        if (ReferenceEquals(first, null)) {
            if (ReferenceEquals(second, null))
                return true;

            return false;
        }

        return first.Equals(second);
    }

    public static bool operator !=(Corner first, Corner second) {
        return !(first == second);
    }

    public override bool Equals(object obj) {
        Corner corner = obj as Corner;

        if (corner == null) return false;
        else return corner.Coord == Coord;
    }

    public override int GetHashCode() {
        return Coord.GetHashCode();
    }
}
