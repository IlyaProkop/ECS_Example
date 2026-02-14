using System;
using UnityEngine;
using Game.Domain;

namespace Game.Config {
    [Serializable]
    public sealed class ActorVisualAuthoring {
        [Min(1)] public int id = ActorVisualIds.Enemy;
        public PrimitiveType primitive = PrimitiveType.Capsule;
        public Mesh mesh;
        public Material materialTemplate;
        public Color color = Color.white;
        [Tooltip("Zero uses the actor diameter. Positive values specify world-space height.")]
        [Min(0)] public float height = 2f;
        [Tooltip("Scale applied to the actor dimensions. Unity capsules need Y = 0.5.")]
        public Vector3 meshScale = new Vector3(1f, 0.5f, 1f);
        [Tooltip("Negative values center the visual at half its height above the arena.")]
        public float elevation = -1f;
        public bool castShadows = true, receiveShadows = true;

        public static ActorVisualAuthoring[] CreateDefaults() => new[] {
            new ActorVisualAuthoring { id = ActorVisualIds.Player, color = new Color(0.25f, 0.85f, 0.45f) },
            new ActorVisualAuthoring { id = ActorVisualIds.Enemy, color = new Color(0.9f, 0.25f, 0.25f) },
            new ActorVisualAuthoring { id = ActorVisualIds.Projectile, primitive = PrimitiveType.Sphere,
                color = new Color(1f, 0.9f, 0.25f), height = 0f, meshScale = Vector3.one, elevation = 1f },
            new ActorVisualAuthoring { id = ActorVisualIds.Coin, primitive = PrimitiveType.Sphere,
                color = new Color(1f, 0.75f, 0.1f), height = 0f, meshScale = Vector3.one }
        };
    }
}
