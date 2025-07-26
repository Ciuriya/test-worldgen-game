using UnityEngine;
using System.Collections.Generic;
using System;

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

    public void SetRoom(Room room) {
        Room = room;
    }

    public List<Zone> GetNeighbors() {
        return Neighbors;
    }

    public List<Zone> GetNeighborsWithRoom(Room room) {
        return Neighbors.FindAll(z => z.Room == room);
    }

    public float FindPercentDistanceFromEdge(Rect bounds) {
        FindDistanceFromEdge(bounds, out float xDist, out float yDist);

        // then converting to percentage of map (up to 50%, that's center)
        if (xDist < yDist) return xDist / bounds.width * 100f;
        else return yDist / bounds.height * 100f;
    }

    public void FindDistanceFromEdge(Rect bounds, out float xDist, out float yDist) {
        // finding nearest edge distance
        xDist = bounds.width / 2 - Mathf.Abs(Center.x - bounds.width / 2);
        yDist = bounds.height / 2 - Mathf.Abs(Center.y - bounds.height / 2);
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
        if (ReferenceEquals(first, null)) {
            if (ReferenceEquals(second, null))
                return true;

            return false;
        }

        return first.Equals(second);
    }

    public static bool operator !=(Zone first, Zone second) {
        return !(first == second);
    }

    public override bool Equals(object obj) {
        Zone zone = obj as Zone;

        if (zone == null) return false;
        else return zone.Center == Center;
    }

    public override int GetHashCode() =>
        HashCode.Combine(Mathf.RoundToInt(Center.x * 1000f), 
                         Mathf.RoundToInt(Center.y * 1000f));
}
