using UnityEngine;
using System.Collections.Generic;
using System;

namespace PendingName.WorldGen {
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
            if (first is null) {
                if (second is null) return true;

                return false;
            }

            return first.Equals(second);
        }

        public static bool operator !=(Corner first, Corner second) => !(first == second);

        public override bool Equals(object obj) {
            Corner corner = obj as Corner;

            return corner != null && corner.Coord == Coord;
        }

        public override int GetHashCode() =>
            HashCode.Combine(Mathf.RoundToInt(Coord.x * 1000f),
                             Mathf.RoundToInt(Coord.y * 1000f));
    }
}
