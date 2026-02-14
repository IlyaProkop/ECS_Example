using System;
using System.Collections.Generic;
using Game.Domain;
using UnityEngine;

namespace Game.Diagnostics {
    // Captured outside frame timing. A fixed 2m cell matches the motion index's current resolution.
    [Serializable] internal sealed class BenchmarkPopulation {
        public int enemies, projectiles, within10Meters, belowWall, aboveWall, maxEnemiesIn2MeterCell;
        public Vector3 playerPosition;
        public float meanDistanceToPlayer;
        public static BenchmarkPopulation Read(ActorView[] actors, int count, Vector3 player) {
            var result = new BenchmarkPopulation { playerPosition = player };
            var cells = new Dictionary<Vector2Int, int>();
            for (var i = 0; i < count; i++) {
                var actor = actors[i];
                if (actor.kind == ActorKind.Projectile) result.projectiles++;
                if (actor.kind != ActorKind.Enemy) continue;
                result.enemies++;
                var distance = Vector3.Distance(actor.position, player);
                result.meanDistanceToPlayer += distance;
                if (distance <= 10f) result.within10Meters++;
                if (actor.position.z < -2f) result.belowWall++;
                if (actor.position.z > 2f) result.aboveWall++;
                var cell = new Vector2Int(Mathf.FloorToInt(actor.position.x / 2f), Mathf.FloorToInt(actor.position.z / 2f));
                cells.TryGetValue(cell, out var occupants);
                cells[cell] = ++occupants;
                result.maxEnemiesIn2MeterCell = Math.Max(result.maxEnemiesIn2MeterCell, occupants);
            }
            if (result.enemies > 0) result.meanDistanceToPlayer /= result.enemies;
            return result;
        }
    }
}
