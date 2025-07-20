using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldGen/World Generator Data")]
public class WorldGeneratorData : ScriptableObject {

    [Tooltip("Number of cells in the voronoi graph")]
    [Range(1, 2500)] public int CellCount = 100;

    [Tooltip("Cell regularity\n0 = no adjustment, 10 = fully regular cells")]
    [Range(0, 10)] public int LloydRelaxations = 0;

    public List<GraphGrammarRule> GrammarRules;

    public Vector2 MapSize; 

    [Range(1, 100)] public int LineThickness = 1;

    [Tooltip("The material used to represent zones")]
    public Material ZoneMaterial;

    [Tooltip("The material used to represent lines")]
    public Material LineMaterial;

}
