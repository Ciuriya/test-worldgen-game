public class ZoneRoomWrapper {

    public Room Room;
    public IndexMapWrapper WallIndexMap;
    public IndexMapWrapper FloorIndexMap;
    public IndexMapWrapper GetIndexMap(bool isWall) => isWall ? WallIndexMap : FloorIndexMap;
}
