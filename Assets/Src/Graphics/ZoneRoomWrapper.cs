using System.Collections.Generic;

public class ZoneRoomWrapper {

    public Room Room;
    public List<IndexMapWrapper> WallIndexMaps;
    public IndexMapWrapper FloorIndexMap;
    
    public IndexMapWrapper GetIndexMap(bool isWall, int index = 0) {
        if (isWall) {
            if (!Room.PickDifferentIndexMaps) index = 0;
            if (WallIndexMaps.Count > index) return WallIndexMaps[index];
            if (WallIndexMaps.Count > 0) return WallIndexMaps[index % WallIndexMaps.Count];

            return null;
        }

        return FloorIndexMap;
    }
}
