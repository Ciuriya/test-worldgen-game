using UnityEngine;
using System.Collections.Generic;

namespace PendingName.Extruders {
    public class CircleExtruder : Extruder {

        [Tooltip("The radius of the circle to extrude")]
        [Range(0, 25)] public float Radius;

        [Tooltip("How many points per quadrant should the extruded collider have?")]
        [Range(1, 10)] public int Precision;

        public override void Extrude() {
            if (!CanExtrude()) return;

            List<Vector2> points = new List<Vector2>();
            Vector2 center = new Vector2(0, 0);

            for (float angle = 0; angle <= 2 * Mathf.PI; angle += (2f * Mathf.PI) / (float)(Precision * 4)) {
                if (angle == 0) continue;

                points.Add(new Vector2(center.x + Radius * Mathf.Cos(angle), center.y + Radius * Mathf.Sin(angle)));
            }

            GameObject extruded = Create3DMeshObject(points.ToArray(), transform, gameObject.name + "Extrusion", null);
            extruded.transform.position += transform.position;
        }
    }
}