using System.Numerics;

namespace AssemblyBrowser
{
    public static class Colors
    {
        public static readonly Vector4 Red = new Vector4(1, 0, 0, 1);
        public static readonly Vector4 Green = new Vector4(0, 1, 0, 1);
        public static readonly Vector4 Blue = new Vector4(0, 0, 1, 1);
        public static readonly Vector4 Yellow = new Vector4(.85f, .85f, 0.15f, 1);
        public static readonly Vector4 Grey = new Vector4(.25f, .25f, .25f, 1);
        public static readonly Vector4 Cyan = new Vector4(0, 1, 1, 1);
        public static readonly Vector4 White = new Vector4(1, 1, 1, 1);

        public static readonly Vector4 NamespaceLabel = new Vector4(240f / 255f, 202f / 255f, 147f / 255f, 1f);
    }
}
