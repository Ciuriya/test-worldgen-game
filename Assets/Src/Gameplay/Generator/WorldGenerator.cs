using Delaunay;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class WorldGenerator {

    private readonly WorldGeneratorData _data;
    private World _world;
    private bool _isGenerating;
    private double _totalGenerationTime;
    private WorldMapExtruder _extruder;

    public WorldGenerator(WorldGeneratorData worldGenData) {
        _data = worldGenData;
    }

    // multi-thread? can this even be multi-threaded really? maybe update the gen text here? maybe make it a step enum system? no clue
    // could also be cool to make it into a loading animation
    public void StartGenerationSequence() {
        _world?.Destroy();

        _totalGenerationTime = Time.realtimeSinceStartupAsDouble;

        _world = new World(GenerateVoronoi(), _data);
        _world.Load();
        _extruder = _world.GenerateMesh();
        _isGenerating = true;
    }

    public void EarlyUpdate() {
        if (_isGenerating && !_extruder.IsGenerating)
            FinalizeGeneratingSequence();
    }

    private void FinalizeGeneratingSequence() {
        _isGenerating = false;
        _totalGenerationTime = Math.Round(Time.realtimeSinceStartupAsDouble - _totalGenerationTime, 3);
        double extrusionTime = Math.Round(_extruder.GenerationTime, 3);
        
        Debug.Log($"Generated in {_totalGenerationTime} seconds.\n" +
                  $"Extrusion took {extrusionTime} seconds.");
    }

    public Voronoi GenerateVoronoi() {
        List<Vector2> points = GenerateRandomPoints();

        return new Voronoi(points, new Rect(-(_data.MapSize.x / 2), -(_data.MapSize.y / 2), 
                                            _data.MapSize.x, _data.MapSize.y), 
                           _data.LloydRelaxations);
    }

    private List<Vector2> GenerateRandomPoints() {
        List<Vector2> points = new List<Vector2>();

        for (int i = 0; i < _data.CellCount; i++)
            points.Add(new Vector2(Random.Range(-(_data.MapSize.x / 2), _data.MapSize.x / 2), 
                                   Random.Range(-(_data.MapSize.y / 2), _data.MapSize.y / 2)));

        return points;
    }
}
