using UnityEngine;
using System.Collections.Generic;
using Delaunay;

public class World {

    public List<Zone> Zones;
    public List<Corner> Corners;
    public List<Edge> Edges;

    private WorldGeneratorData _data;
    private readonly Voronoi _voronoi;
    private GameObject _worldObject;

    public World(Voronoi voronoi, WorldGeneratorData data) {
        _voronoi = voronoi;
        _data = data;

        Zones = new List<Zone>();
        Corners = new List<Corner>();
        Edges = new List<Edge>();

        LoadZones();
    }

    public void Load() {
        ApplyGrammarRules(_data.GrammarRules);
    }

    private void LoadZones() {
        // we save everything into our own types to avoid modifying the library
        // regions are zones here
        List<List<Vector2>> regions = _voronoi.Regions();
        List<Vector2> regionCenters = _voronoi.SiteCoords();
        List<Delaunay.Edge> regionEdges = _voronoi.Edges();

        for (int i = 0; i < regions.Count; i++) {
            List<Vector2> region = regions[i];
            List<Corner> zoneCorners = new List<Corner>();

            foreach (Vector2 coord in region) {
                Corner corner = new Corner(coord);

                zoneCorners.Add(corner);
                Corners.Add(corner);
            }

            Zones.Add(new Zone(regionCenters[i], zoneCorners, null));
        }

        // we go through all the edges in the graph to load them
        // apparently there's a bug relating to how data is loaded into memory but I couldn't find info on that
        foreach (Delaunay.Edge regionEdge in regionEdges) {
            if (!regionEdge.visible) continue; // causes fake point issues if invisible

            Corner leftCorner = Corners.Find(c => c.Coord == regionEdge.clippedEnds[Delaunay.LR.Side.LEFT].Value);
            Corner rightCorner = Corners.Find(c => c.Coord == regionEdge.clippedEnds[Delaunay.LR.Side.RIGHT].Value);
            Zone leftZone = Zones.Find(z => z.Center == regionEdge.leftSite.Coord);
            Zone rightZone = Zones.Find(z => z.Center == regionEdge.rightSite.Coord);

            // grab nearest point in case it's not exactly right
            if (leftCorner == null)
                leftCorner = Corners.Find(c => Vector2.Distance(c.Coord,
                                                                  regionEdge.clippedEnds[Delaunay.LR.Side.LEFT].Value) < 0.2f &&
                                                                  c != rightCorner);

            if (rightCorner == null)
                rightCorner = Corners.Find(c => Vector2.Distance(c.Coord,
                                                                   regionEdge.clippedEnds[Delaunay.LR.Side.RIGHT].Value) < 0.2f &&
                                                                   c != leftCorner);

            Edge edge = new Edge(leftCorner, rightCorner, leftZone, rightZone);

            // link up everything
            leftCorner.Edges.Add(edge);
            leftCorner.Neighbors.Add(rightCorner);

            if (!leftCorner.Zones.Contains(leftZone))
                leftCorner.Zones.Add(leftZone);

            if (!leftCorner.Zones.Contains(rightZone))
                leftCorner.Zones.Add(rightZone);

            rightCorner.Edges.Add(edge);
            rightCorner.Neighbors.Add(leftCorner);

            if (!rightCorner.Zones.Contains(leftZone))
                rightCorner.Zones.Add(leftZone);

            if (!rightCorner.Zones.Contains(rightZone))
                rightCorner.Zones.Add(rightZone);

            leftZone.Edges.Add(edge);
            rightZone.Edges.Add(edge);

            if (!leftZone.Neighbors.Contains(rightZone))
                leftZone.Neighbors.Add(rightZone);

            if (!rightZone.Neighbors.Contains(leftZone))
                rightZone.Neighbors.Add(leftZone);

            Edges.Add(edge);
        }

        // ensure zones with touching corners know they're neighbors
        foreach (Zone zone in Zones)
            foreach (Corner corner in zone.Corners)
                foreach (Zone other in Zones)
                    if (other != zone)
                        foreach (Corner otherCorner in other.Corners)
                            // we avoid equality issues
                            if (Vector2.Distance(corner.Coord, otherCorner.Coord) < 0.2) {
                                if (!zone.Neighbors.Contains(other))
                                    zone.Neighbors.Add(other);

                                if (!other.Neighbors.Contains(zone))
                                    other.Neighbors.Add(zone);

                                if (!corner.Zones.Contains(other))
                                    corner.Zones.Add(other);

                                if (!otherCorner.Zones.Contains(zone))
                                    otherCorner.Zones.Add(zone);
                            }
    }

    // todo: a damn solid pass on this
    private void ApplyGrammarRules(List<GraphGrammarRule> grammarRules) {
        foreach (GraphGrammarRule rule in grammarRules) {
            GrammarInput input = rule.Input;
            List<Zone> matchingZones = input.ValidRooms.Count > 0 ?
                                        Zones.FindAll(z => input.ValidRooms.Contains(z.Room)) :
                                        Zones.FindAll(z => z.Room == null);

            List<Zone> rejectedZones = new List<Zone>();
            int ruleApplications = 0;
            int changesApplied = 1;
            int prevChangesApplied = 0;

            // verify zones until none are valid, the cap is reached or not enough zones are being changed every loop (changeable in editor)
            // changes are applied as it goes, so previously rejected zones could now be valid
            // we only check rejected zones after first loop to avoid spam rechecks
            while (changesApplied > 0 && (prevChangesApplied == 0 ||
                                            Mathf.Abs(changesApplied - prevChangesApplied) / prevChangesApplied <=
                                            1 - input.RuleApplicationMinimalVariationPercentage / 100f) &&
                                        ruleApplications < rule.ApplicationLimit) {
                prevChangesApplied = changesApplied;
                changesApplied = 0;
                int zoneCount = matchingZones.Count;

                for (int i = 0; i < zoneCount; i++) {
                    int index = input.RandomizeVerification ? Random.Range(0, matchingZones.Count) : 0;
                    Zone zone = matchingZones[index];

                    matchingZones.RemoveAt(index);
                    rejectedZones.Add(zone);

                    float distFromEdge = zone.FindPercentDistanceFromEdge(_voronoi.plotBounds);

                    if (distFromEdge < input.DistanceFromEdge.Min || distFromEdge > input.DistanceFromEdge.Max)
                        continue;

                    // zone has been deemed valid
                    if (IsZoneValid(zone, rule)) {
                        rejectedZones.Remove(zone);
                        changesApplied++;

                        zone.SetRoom(rule.Output);
                        ruleApplications++;

                        if (ruleApplications >= rule.ApplicationLimit) break;
                    }
                }

                // rejected zones are potentially matching after processing, so we add them again and send it
                matchingZones.AddRange(rejectedZones);
                rejectedZones.Clear();
            }
        }
    }

    private bool IsZoneValid(Zone zone, GraphGrammarRule rule) {
        int validRequirements = 0;
        bool checkingMandatory = false;
        float connected = 0;
        Room prevReq = null;
        int neighborsWithRoom = 0;

        for (int j = 0; j < rule.Input.Requirements.Count; j++) {
            // if we can't satisfy the requirement count with how many are left, we break
            if (rule.Input.Requirements.Count - j < rule.Input.RequirementCount - validRequirements) break;

            GraphGrammarRequirement req = rule.Input.Requirements[j];
            checkingMandatory = req.Mandatory;

            // todo: these two could be optimized
            List<Zone> zonesWithReqRoom = Zones.FindAll(z => z.Room == req.Room);
            float distFromNearest = zone.FindDistanceToNearestZone(zonesWithReqRoom);

            if (distFromNearest < req.DistanceFromNearest.Min || distFromNearest > req.DistanceFromNearest.Max) {
                if (checkingMandatory) break;
                else continue;
            }

            if (zonesWithReqRoom.Count / (float) Zones.Count * 100f < req.Existing.Min ||
                zonesWithReqRoom.Count / (float) Zones.Count * 100f > req.Existing.Max) {
                if (checkingMandatory) break;
                else continue;
            }

            float roomEdgeLength = 0;
            float totalEdgeLength = 0;

            foreach (Edge edge in zone.Edges) {
                float dist = Vector2.Distance(edge.FirstCorner.Coord, edge.SecondCorner.Coord);
                Zone neighbor = edge.LeftZone != zone ? edge.LeftZone : edge.RightZone;

                if (neighbor.Room == req.Room) roomEdgeLength += dist;

                totalEdgeLength += dist;
            }

            float neighborhoodPercent = roomEdgeLength / totalEdgeLength * 100f;

            if (neighborhoodPercent < req.NeighborhoodPercent.Min || neighborhoodPercent > req.NeighborhoodPercent.Max) {
                if (checkingMandatory) break;
                else continue;
            }

            // only doing these when absolutely necessary as they're demanding
            // it's calculating how many are connected together
            // todo: could we pre-calc the connected size?
            if (connected == 0 || prevReq != req.Room) {
                List<Zone> openSet = zone.GetNeighborsWithRoom(req.Room);
                List<Zone> closedSet = new List<Zone>();

                connected = 0;
                prevReq = req.Room;
                neighborsWithRoom = openSet.Count;

                closedSet.Add(zone);

                if (openSet.Count < req.DirectNeighbors && req.DirectNeighbors > 0) {
                    if (checkingMandatory) break;
                    else continue;
                } else if (openSet.Count > 0 && req.NoDirectNeighbors) {
                    if (checkingMandatory) break;
                    else continue;
                }

                if (req.Connected.Min != 0 || req.Connected.Max != 100 ||
                    req.ConnectedPercentOfTotalWithRoom.Min != 0 || req.ConnectedPercentOfTotalWithRoom.Max != 100) {
                    while (openSet.Count > 0) {
                        Zone open = openSet[0];

                        connected++;

                        foreach (Zone neighbor in open.GetNeighborsWithRoom(req.Room))
                            if (!openSet.Contains(neighbor) && !closedSet.Contains(neighbor))
                                openSet.Add(neighbor);

                        closedSet.Add(open);
                        openSet.Remove(open);
                    }
                }
            } else if (neighborsWithRoom < req.DirectNeighbors && req.DirectNeighbors > 0) {
                if (checkingMandatory) break;
                else continue;
            } else if (neighborsWithRoom > 0 && req.NoDirectNeighbors) {
                if (checkingMandatory) break;
                else continue;
            }

            if (connected / Zones.Count * 100f < req.Connected.Min || connected / Zones.Count * 100f > req.Connected.Max) {
                if (checkingMandatory) break;
                else continue;
            }

            if (connected / zonesWithReqRoom.Count * 100f < req.ConnectedPercentOfTotalWithRoom.Min ||
                connected / zonesWithReqRoom.Count * 100f > req.ConnectedPercentOfTotalWithRoom.Max) {
                if (checkingMandatory) break;
                else continue;
            }

            if (req.Room == rule.Output && (connected + 1) / Zones.Count * 100f >= req.Connected.Max) {
                if (checkingMandatory) break;
                else continue;
            }

            validRequirements++;
            checkingMandatory = false;
        }

        return validRequirements >= rule.Input.RequirementCount && !checkingMandatory;
    }

    public void GenerateMesh() {
        if (_worldObject != null) Object.Destroy(_worldObject);

        _worldObject = new GameObject("Map");
        //_worldObject.layer = LayerMask.NameToLayer("Graph");
        _worldObject.transform.position = new Vector3(0f, 0f, 0f);

        // todo: bake these into world gen settings later, probably in the WorldMapExtruder itself
        WorldMapExtruder extruder = _worldObject.AddComponent<WorldMapExtruder>();
        extruder.MeshDefaultMaterial = _data.ZoneMaterial;
        extruder.ShadowMode = UnityEngine.Rendering.ShadowCastingMode.On;
        extruder.ExtrusionDepth = 1;
        extruder.ExtrusionHeight = 1;
        extruder.Layer = "Default";
        extruder.ExtrudeOnStart = false;
        extruder.World = this;

        extruder.Extrude();
    }

    public void Destroy() {
        if (_worldObject != null) Object.Destroy(_worldObject);

        Zones.Clear();
    }

    public Zone FindZoneAtPosition(Vector2 position) {
        Zone nearestZone = null;
        float distanceToNearestZone = float.MaxValue;

        foreach (Zone zone in Zones) {
            float distance = Vector2.Distance(zone.Center, position);

            if (distance < distanceToNearestZone) {
                distanceToNearestZone = distance;
                nearestZone = zone;
            }
        }

        return nearestZone;
    }
}
