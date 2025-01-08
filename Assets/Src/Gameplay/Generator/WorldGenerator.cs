using Delaunay;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator {

    private WorldGeneratorData _data;
    private World _world;

    public WorldGenerator(WorldGeneratorData worldGenData) {
        _data = worldGenData;
    }

    // multi-thread? can this even be multi-threaded really? maybe update the gen text here? maybe make it a step enum system? no clue
    // could also be cool to make it into a loading animation
    public void StartGenerationSequence() {
        if (_world != null) _world.Destroy();

        float generationTime = Time.realtimeSinceStartup;

        _world = new World(GenerateVoronoi(), _data);
        _world.GenerateMesh();
        _world.Load();

        generationTime = Time.realtimeSinceStartup - generationTime;
        Debug.Log($"Generated in {Mathf.RoundToInt(generationTime * 1000) / 1000.0f} seconds.");
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
