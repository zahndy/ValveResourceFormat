using System.Runtime.InteropServices;
using GUI.Utils;
using OpenTK.Graphics.OpenGL;

namespace GUI.Types.Renderer
{
    [StructLayout(LayoutKind.Sequential)]
    public record struct SimpleVertex(Vector3 Position, Color32 Color)
    {
        public static readonly int SizeInBytes = Marshal.SizeOf<SimpleVertex>();

        public static void BindDefaultShaderLayout(int shaderProgram)
        {
            var positionAttributeLocation = GL.GetAttribLocation(shaderProgram, "aVertexPosition");
            GL.EnableVertexAttribArray(positionAttributeLocation);
            GL.VertexAttribPointer(positionAttributeLocation, 3, VertexAttribPointerType.Float, false, SimpleVertex.SizeInBytes, 0);

            var colorAttributeLocation = GL.GetAttribLocation(shaderProgram, "aVertexColor");
            GL.EnableVertexAttribArray(colorAttributeLocation);
            GL.VertexAttribPointer(colorAttributeLocation, 4, VertexAttribPointerType.UnsignedByte, true, SimpleVertex.SizeInBytes, sizeof(float) * 3);
        }
    }

    class OctreeDebugRenderer<T>
        where T : class
    {
        private readonly Shader shader;
        private readonly Octree<T> octree;
        private readonly int vaoHandle;
        private readonly int vboHandle;
        private readonly bool dynamic;
        private bool built;
        private int vertexCount;

        public OctreeDebugRenderer(Octree<T> octree, VrfGuiContext guiContext, bool dynamic)
        {
            this.octree = octree;
            this.dynamic = dynamic;

            shader = shader = guiContext.ShaderLoader.LoadShader("vrf.default");
            GL.UseProgram(shader.Program);

            vboHandle = GL.GenBuffer();

            vaoHandle = GL.GenVertexArray();
            GL.BindVertexArray(vaoHandle);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboHandle);

            SimpleVertex.BindDefaultShaderLayout(shader.Program);

            GL.BindVertexArray(0);
        }

        public static void AddLine(List<SimpleVertex> vertices, Vector3 from, Vector3 to, Color32 color)
        {
            vertices.Add(new SimpleVertex(from, color));
            vertices.Add(new SimpleVertex(to, color));
        }

        public static void AddBox(List<SimpleVertex> vertices, in AABB box, Color32 color)
        {
            // Adding a box will add many vertices, so ensure the required capacity for it up front
            vertices.EnsureCapacity(vertices.Count + 2 * 12);

            AddLine(vertices, new Vector3(box.Min.X, box.Min.Y, box.Min.Z), new Vector3(box.Max.X, box.Min.Y, box.Min.Z), color);
            AddLine(vertices, new Vector3(box.Max.X, box.Min.Y, box.Min.Z), new Vector3(box.Max.X, box.Max.Y, box.Min.Z), color);
            AddLine(vertices, new Vector3(box.Max.X, box.Max.Y, box.Min.Z), new Vector3(box.Min.X, box.Max.Y, box.Min.Z), color);
            AddLine(vertices, new Vector3(box.Min.X, box.Max.Y, box.Min.Z), new Vector3(box.Min.X, box.Min.Y, box.Min.Z), color);

            AddLine(vertices, new Vector3(box.Min.X, box.Min.Y, box.Max.Z), new Vector3(box.Max.X, box.Min.Y, box.Max.Z), color);
            AddLine(vertices, new Vector3(box.Max.X, box.Min.Y, box.Max.Z), new Vector3(box.Max.X, box.Max.Y, box.Max.Z), color);
            AddLine(vertices, new Vector3(box.Max.X, box.Max.Y, box.Max.Z), new Vector3(box.Min.X, box.Max.Y, box.Max.Z), color);
            AddLine(vertices, new Vector3(box.Min.X, box.Max.Y, box.Max.Z), new Vector3(box.Min.X, box.Min.Y, box.Max.Z), color);

            AddLine(vertices, new Vector3(box.Min.X, box.Min.Y, box.Min.Z), new Vector3(box.Min.X, box.Min.Y, box.Max.Z), color);
            AddLine(vertices, new Vector3(box.Max.X, box.Min.Y, box.Min.Z), new Vector3(box.Max.X, box.Min.Y, box.Max.Z), color);
            AddLine(vertices, new Vector3(box.Max.X, box.Max.Y, box.Min.Z), new Vector3(box.Max.X, box.Max.Y, box.Max.Z), color);
            AddLine(vertices, new Vector3(box.Min.X, box.Max.Y, box.Min.Z), new Vector3(box.Min.X, box.Max.Y, box.Max.Z), color);
        }

        public static void AddBox(List<SimpleVertex> vertices, in Matrix4x4 transform, in AABB box, Color32 color)
        {
            // Adding a box will add many vertices, so ensure the required capacity for it up front
            vertices.EnsureCapacity(vertices.Count + 2 * 12);

            var c1 = Vector3.Transform(new Vector3(box.Min.X, box.Min.Y, box.Min.Z), transform);
            var c2 = Vector3.Transform(new Vector3(box.Max.X, box.Min.Y, box.Min.Z), transform);
            var c3 = Vector3.Transform(new Vector3(box.Max.X, box.Max.Y, box.Min.Z), transform);
            var c4 = Vector3.Transform(new Vector3(box.Min.X, box.Max.Y, box.Min.Z), transform);
            var c5 = Vector3.Transform(new Vector3(box.Min.X, box.Min.Y, box.Max.Z), transform);
            var c6 = Vector3.Transform(new Vector3(box.Max.X, box.Min.Y, box.Max.Z), transform);
            var c7 = Vector3.Transform(new Vector3(box.Max.X, box.Max.Y, box.Max.Z), transform);
            var c8 = Vector3.Transform(new Vector3(box.Min.X, box.Max.Y, box.Max.Z), transform);

            AddLine(vertices, c1, c2, color);
            AddLine(vertices, c2, c3, color);
            AddLine(vertices, c3, c4, color);
            AddLine(vertices, c4, c1, color);

            AddLine(vertices, c5, c6, color);
            AddLine(vertices, c6, c7, color);
            AddLine(vertices, c7, c8, color);
            AddLine(vertices, c8, c5, color);

            AddLine(vertices, c1, c5, color);
            AddLine(vertices, c2, c6, color);
            AddLine(vertices, c3, c7, color);
            AddLine(vertices, c4, c8, color);
        }

        private static void AddOctreeNode(List<SimpleVertex> vertices, Octree<T>.Node node, int depth)
        {
            AddBox(vertices, node.Region, Color32.White with { A = node.HasElements ? (byte)255 : (byte)64 });

            if (node.HasElements)
            {
                foreach (var element in node.Elements)
                {
                    var shading = Math.Min(1.0f, depth * 0.1f);
                    AddBox(vertices, element.BoundingBox, new(1.0f, shading, 0.0f, 1.0f));

                    // AddLine(vertices, element.BoundingBox.Min, node.Region.Min, new Vector4(1.0f, shading, 0.0f, 0.5f));
                    // AddLine(vertices, element.BoundingBox.Max, node.Region.Max, new Vector4(1.0f, shading, 0.0f, 0.5f));
                }
            }

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    AddOctreeNode(vertices, child, depth + 1);
                }
            }
        }

        public void StaticBuild()
        {
            if (!built)
            {
                built = true;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            var vertices = new List<SimpleVertex>();
            AddOctreeNode(vertices, octree.Root, 0);
            vertexCount = vertices.Count;

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboHandle);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexCount * SimpleVertex.SizeInBytes, vertices.ToArray(), dynamic ? BufferUsageHint.DynamicDraw : BufferUsageHint.StaticDraw);
        }

        public void Render()
        {
            if (dynamic)
            {
                Rebuild();
            }

            GL.Enable(EnableCap.Blend);
            GL.DepthMask(false);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.UseProgram(shader.Program);

            shader.SetUniform4x4("transform", Matrix4x4.Identity);

            GL.BindVertexArray(vaoHandle);
            GL.DrawArrays(PrimitiveType.Lines, 0, vertexCount);
            GL.UseProgram(0);
            GL.BindVertexArray(0);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
        }
    }
}
