using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering {
    internal struct InstancedRenderResources {
        public Mesh mesh;
        public Material material;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
    }

    internal static class InstancedRenderHelper {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        public static InstancedRenderResources CreateResources(in ActorVisual visual) {
            return new InstancedRenderResources {
                mesh = visual.Mesh,
                material = CreateInstancedMaterial(visual.Color, visual.Template),
                shadowCastingMode = visual.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = visual.ReceiveShadows
            };
        }

        private static void Release(Object value) {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        public static bool IsValid(in InstancedRenderResources resources) {
            return resources.mesh != null && resources.material != null;
        }

        public static InstancedBatchWriter CreateBatchWriter(in InstancedRenderResources resources, Matrix4x4[] matrices, Camera camera) {
            return new InstancedBatchWriter(
                resources.mesh,
                resources.material,
                matrices,
                resources.shadowCastingMode,
                resources.receiveShadows,
                camera);
        }

        public static void Dispose(ref InstancedRenderResources resources) {
            if (resources.material != null) {
                Release(resources.material);
            }

            resources.material = null;
            resources.mesh = null;
        }

        public static Mesh LoadPrimitiveMesh(PrimitiveType primitiveType) {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            var meshFilter = primitive.GetComponent<MeshFilter>();
            var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            primitive.SetActive(false);
            Release(primitive);
            return mesh;
        }

        public static Material CreateInstancedMaterial(Color color, Material template = null) {
            if (template == null) template = Resources.Load<Material>("ArenaMaterial");
            var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) {
                shader = Shader.Find("Standard");
            }

            if (shader == null) {
                throw new System.InvalidOperationException("No supported actor material shader was found.");
            }

            var material = template != null ? new Material(template) : new Material(shader);
            material.enableInstancing = true;
            if (material.HasProperty(BaseColorPropertyId)) {
                material.SetColor(BaseColorPropertyId, color);
            }

            if (material.HasProperty(ColorPropertyId)) {
                material.SetColor(ColorPropertyId, color);
            }

            return material;
        }

        public static void DrawBatch(
            Mesh mesh,
            Material material,
            Matrix4x4[] matrices,
            int count,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            Camera camera) {
            if (mesh == null || material == null || count <= 0) {
                return;
            }

            Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                matrices,
                count,
                null,
                shadowCastingMode,
                receiveShadows,
                0,
                camera);
        }
    }

    internal struct InstancedBatchWriter {
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Matrix4x4[] matrices;
        private readonly ShadowCastingMode shadowCastingMode;
        private readonly bool receiveShadows;
        private readonly Camera camera;

        private int count;

        public InstancedBatchWriter(
            Mesh mesh,
            Material material,
            Matrix4x4[] matrices,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            Camera camera) {
            this.mesh = mesh;
            this.material = material;
            this.matrices = matrices;
            this.shadowCastingMode = shadowCastingMode;
            this.receiveShadows = receiveShadows;
            this.camera = camera;
            this.count = 0;
        }

        public void Add(Matrix4x4 matrix) {
            this.matrices[this.count] = matrix;
            this.count++;
            if (this.count < this.matrices.Length) {
                return;
            }

            this.Flush();
        }

        public void Flush() {
            InstancedRenderHelper.DrawBatch(
                this.mesh,
                this.material,
                this.matrices,
                this.count,
                this.shadowCastingMode,
                this.receiveShadows,
                this.camera);
            this.count = 0;
        }
    }
}
