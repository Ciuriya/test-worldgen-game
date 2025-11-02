using UnityEngine;
using System;
using MyBox;

namespace PendingName.WorldGen {
    
    [Serializable]
    public class GraphGrammarRequirement {

        [Tooltip("The room targeted by this requirement")]
        public Room Room;

        [Tooltip("True if this requirement is required\n" +
                 "If false, this requirement uses the number of requirements needed in the grammar input")]
        public bool Mandatory;

        [Tooltip("The minimum amount of adjacent zones with the same room required")]
        [Range(0, 8)] public int DirectNeighbors;

        [Tooltip("True if there can't be any adjacent zone with this room")]
        public bool NoDirectNeighbors;

        [Tooltip("The percentage of current neighbors with the same room")]
        [MinMaxRange(0, 100)] public RangedInt NeighborhoodPercent;

        [Tooltip("The distance (in zone diameters) between the nearest room and this zone")]
        [MinMaxRange(0, 100)] public RangedInt DistanceFromNearest;

        [Tooltip("The percentage of the map that can be covered by this room")]
        [MinMaxRange(0, 100)] public RangedInt Existing;

        [Tooltip("The percentage of the map that can be connected in a single region starting from current neighbors")]
        [MinMaxRange(0, 100)] public RangedInt Connected;

        [Tooltip("The percentage of all zones with this room connected in a single region starting from current neighbors")]
        [MinMaxRange(0, 100)] public RangedInt ConnectedPercentOfTotalWithRoom;
    }
}