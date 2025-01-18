using System.Collections.Generic;
using UnityEngine;

public class CompositeColliderExtruder : Extruder {

    [Tooltip("The composite collider of the tilemap to extrude")]
    public CompositeCollider2D CompositeCollider2D;

    public override void Extrude() {
        if (!CanExtrude()) return;

        for (int i = 0; i < CompositeCollider2D.pathCount; i++) {
            Vector2[] pathVertices = new Vector2[CompositeCollider2D.GetPathPointCount(i)];
            CompositeCollider2D.GetPath(i, pathVertices);

            Create3DMeshObject(pathVertices, transform, gameObject.name + "Extrusion", null);
        }
    }
}
