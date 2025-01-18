using UnityEngine;

public class BoxColliderExtruder : Extruder {

    [Tooltip("The collider of the sprite to extrude")]
    public BoxCollider2D Collider;

    public override void Extrude() {
        if (!CanExtrude()) return;

        Vector2[] vertices = new Vector2[4];

        vertices[0] = Collider.bounds.min;
        vertices[1] = new Vector2(Collider.bounds.min.x, Collider.bounds.max.y);
        vertices[2] = Collider.bounds.max;
        vertices[3] = new Vector2(Collider.bounds.max.x, Collider.bounds.min.y);

        Create3DMeshObject(vertices, transform, gameObject.name + "Extrusion", null);
    }
}
