using System;
using System.Collections.Generic;

[Serializable]
public class ZoneRoomWrapper {

    public Room Room;
    public List<IndexMapWrapper> WallIndexMaps;
    public IndexMapWrapper FloorIndexMap;
    public float MergeWallsTileSize;
    public List<float> MergeWallsStartOffsets;

    public IndexMapWrapper GetIndexMap(bool isEdge, int index = 0) {
        if (isEdge) {
            if (Room.MergeWalls || !Room.PickDifferentIndexMaps) index = 0;

            if (WallIndexMaps.Count > index) 
                return WallIndexMaps[index];
            if (WallIndexMaps.Count > 0) 
                return WallIndexMaps[index % WallIndexMaps.Count];

            return null;
        }

        return FloorIndexMap;
    }
    
}
