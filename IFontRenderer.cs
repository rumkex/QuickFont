using System.Drawing;
using System.Drawing.Imaging;

namespace QuickFont
{
    public struct Quad
    {
        public float X1, X2;
        public float Y1, Y2;
        public float U1, U2;
        public float V1, V2;

        public Color Color;
        public Texture Texture;
    }

    public abstract class Texture
    {
        public int Width { get; protected set; }
        public int Height { get; protected set; }
    }

    public interface IFontRenderer
    {
        void DrawQuad(Quad q);
        void Begin(PointF offset);
        void End();
        Texture CreateTexture(BitmapData bitmapData);
    }
}