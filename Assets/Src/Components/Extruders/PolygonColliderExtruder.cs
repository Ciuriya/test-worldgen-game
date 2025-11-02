using UnityEngine;
using System.Collections.Generic;

namespace PendingName.Extruders {
    public class PolygonColliderExtruder : Extruder {

        [Tooltip("The collider of the sprite to extrude, if not using any, use the sprite's physics shape")]
        public PolygonCollider2D Collider;

        [Tooltip("If not using a collider, should we reverse the path order to look better?")]
        public bool Reverse;

        public override void Extrude() {
            if (!CanExtrude()) return;

            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            List<Vector2> points = new List<Vector2>();

            if (!Collider && renderer) {
                Sprite sprite = renderer.sprite;
                List<Vector2> currentPhysicsPath = new List<Vector2>();
                Vector2 prev = new Vector2(0, 0);
                float x1 = 0;
                float x2 = 0;
                float y1 = 0;
                float y2 = 0;
                bool xCommonPrev = true;
                int set = 0;

                sprite.GetPhysicsShape(0, currentPhysicsPath);

                // converting sprite's physics shape into a singular cohesive path
                for (int i = 0; i < sprite.GetPhysicsShapeCount() * 4; i++) {
                    if (i % 4 == 0 && i != 0) {
                        bool add = false;
                        if (prev != Vector2.zero) add = true;

                        prev = GetPoint(x1, x2, y1, y2, prev);
                        xCommonPrev = y2 == 0;
                        if (add) points.Add(prev);

                        set++;
                        x1 = 0;
                        x2 = 0;
                        y1 = 0;
                        y2 = 0;

                        currentPhysicsPath.Clear();
                        sprite.GetPhysicsShape(set, currentPhysicsPath);
                    }

                    Vector2 vertex = currentPhysicsPath[i % 4];

                    if (x1 == 0) x1 = vertex.x;
                    else if (vertex.x != x1 && x2 == 0) x2 = vertex.x;

                    if (y1 == 0) y1 = vertex.y;
                    else if (vertex.y != y1 && y2 == 0) y2 = vertex.y;
                }

                prev = GetPoint(x1, x2, y1, y2, prev);
                points.Add(prev);
                points.Add(new Vector2(xCommonPrev ? points[0].x : prev.x, xCommonPrev ? prev.y : points[0].y));

                if (Reverse) points.Reverse();
            }
            else if (!Collider) return;
            else points.AddRange(Collider.points);

            GameObject extruded = Create3DMeshObject(points.ToArray(), transform, gameObject.name + "Extrusion", null);
            extruded.transform.position += transform.position;
            extruded.transform.localScale = new Vector3(1, 1, 1);
        }

        private Vector2 GetPoint(float x1, float x2, float y1, float y2, Vector2 prev) {
            float common = x2 == 0 ? x1 : y1;
            float other1 = x2 == 0 ? y1 : x1;
            float other2 = x2 == 0 ? y2 : x2;
            float prevOtherValue = x2 == 0 ? prev.y : prev.x;
            float other = other1 == prevOtherValue ? other2 : other1;

            return new Vector2(x2 == 0 ? common : other, x2 == 0 ? other : common);
        }
    }
}