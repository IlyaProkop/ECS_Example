using Game.Config;
using Game.Domain;
using UnityEngine;

namespace Game.Rendering {
    internal sealed class ArenaCameraController {
        private readonly Camera camera;
        private readonly float height, followRate, minimumSize, halfWidth;
        public ArenaCameraController(GameConfig config, Camera camera) {
            this.camera = camera;
            this.height = config.cameraHeight; this.followRate = config.cameraFollowLerp;
            this.minimumSize = Mathf.Max(config.cameraOrthographicSize, config.arenaHalfSize.y + 4f);
            this.halfWidth = config.arenaHalfSize.x + 2f;
        }
        public void Update(SessionPhase phase, float deltaTime) {
            var target = new Vector3(0f, this.height, phase == SessionPhase.AwaitingEntry ? -3f : 0f);
            this.camera.transform.position = Vector3.Lerp(this.camera.transform.position, target, 1f - Mathf.Exp(-this.followRate * deltaTime));
            var size = Mathf.Max(this.minimumSize, this.halfWidth / Mathf.Max(0.1f, this.camera.aspect));
            if (this.camera.orthographicSize != size) this.camera.orthographicSize = size;
        }
    }
}
