using UnityEngine;
using System.Collections.Generic;
using System;

namespace PendingName.WorldGen {
    public class Zone {
        public Vector2 Center { get; private set; }
        public Room Room { get; private set; }
        public List<Corner> Corners;
        public List<Edge> Edges;
        public List<Zone> Neighbors;

        public Zone(Vector2 center, List<Corner> corners, Room room) {
            Center = center;
            Corners = corners;
            Edges = new List<Edge>();
            Neighbors = new List<Zone>();
            Room = room;

            foreach (Corner corner in Corners)
                corner.Zones.Add(this);
        }

        public void SetRoom(Room room) => Room = room;

        public List<Zone> GetNeighbors() => Neighbors;

        public List<Zone> GetNeighborsWithRoom(Room room) => Neighbors.FindAll(z => z.Room == room);

        public float FindPercentDistanceFromEdge(Rect bounds) {
            FindDistanceFromEdge(bounds, out float xDist, out float yDist);

            // then converting to percentage of map (up to 50%, that's center)
            if (xDist < yDist) return xDist / bounds.width * 100f;
            else return yDist / bounds.height * 100f;
        }

        public void FindDistanceFromEdge(Rect bounds, out float xDist, out float yDist) {
            // finding nearest edge distance
            xDist = Mathf.Abs(Mathf.Abs(Center.x) - Mathf.Abs(bounds.width / 2));
            yDist = Mathf.Abs(Mathf.Abs(Center.y) - Mathf.Abs(bounds.height / 2));
        }

        public int FindDistanceToNearestZone(List<Zone> zonesToCheck) {
            if (zonesToCheck.Count == 0) return 0;

            float closestDist = 100000000;

            foreach (Zone zone in zonesToCheck) {
                float dist = Vector2.Distance(zone.Center, Center);

                if (dist < closestDist) closestDist = dist;
            }

            // distance in current zone diameters, it's an approximation but that's all we need here
            return Mathf.CeilToInt(closestDist / (CalculateMaximumLengthToInternalEdge() * 2));
        }

        private float CalculateMaximumLengthToInternalEdge() {
            float length = 0;

            foreach (Corner corner in Corners) {
                float dist = Vector2.Distance(corner.Coord, Center);

                if (dist > length) length = dist;
            }

            return length;
        }

        public static bool operator ==(Zone first, Zone second) {
            if (first is null) {
                if (second is null) return true;

                return false;
            }

            return first.Equals(second);
        }

        public static bool operator !=(Zone first, Zone second) => !(first == second);

        public override bool Equals(object obj) {
            Zone zone = obj as Zone;

            return zone != null && zone.Center == Center;
        }

        public override int GetHashCode() =>
            HashCode.Combine(Mathf.RoundToInt(Center.x * 1000f),
                             Mathf.RoundToInt(Center.y * 1000f));
    }
}