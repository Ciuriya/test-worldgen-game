using System;

namespace PendingName.WorldGen {
    public class Edge {
        public Corner FirstCorner { get; private set; }
        public Corner SecondCorner { get; private set; }
        public Zone LeftZone { get; private set; }
        public Zone RightZone { get; private set; }

        public Edge(Corner firstCorner, Corner secondCorner, Zone leftZone, Zone rightZone) {
            FirstCorner = firstCorner;
            SecondCorner = secondCorner;
            LeftZone = leftZone;
            RightZone = rightZone;
        }

        public static bool operator ==(Edge first, Edge second) {
            if (first is null) {
                if (second is null) return true;

                return false;
            }

            return first.Equals(second);
        }

        public static bool operator !=(Edge first, Edge second) => !(first == second);

        public override bool Equals(object obj) {
            Edge edge = obj as Edge;

            return edge != null && edge.FirstCorner == FirstCorner && edge.SecondCorner == SecondCorner;
        }

        public override int GetHashCode() =>
            HashCode.Combine(FirstCorner.GetHashCode(), SecondCorner.GetHashCode());
    }
}