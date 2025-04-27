using System;

namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Representa uma caixa delimitadora (bounding box) para um conjunto de feições geográficas
    /// </summary>
    public class BoundingBox
    {
        /// <summary>
        /// Coordenada X mínima (longitude oeste)
        /// </summary>
        public double MinX { get; private set; }

        /// <summary>
        /// Coordenada Y mínima (latitude sul)
        /// </summary>
        public double MinY { get; private set; }

        /// <summary>
        /// Coordenada X máxima (longitude leste)
        /// </summary>
        public double MaxX { get; private set; }

        /// <summary>
        /// Coordenada Y máxima (latitude norte)
        /// </summary>
        public double MaxY { get; private set; }

        /// <summary>
        /// Construtor padrão que inicializa com valores extremos invertidos para facilitar a expansão
        /// </summary>
        public BoundingBox()
        {
            MinX = double.MaxValue;
            MinY = double.MaxValue;
            MaxX = double.MinValue;
            MaxY = double.MinValue;
        }

        /// <summary>
        /// Construtor que inicializa com valores específicos
        /// </summary>
        /// <param name="minX">Coordenada X mínima</param>
        /// <param name="minY">Coordenada Y mínima</param>
        /// <param name="maxX">Coordenada X máxima</param>
        /// <param name="maxY">Coordenada Y máxima</param>
        public BoundingBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>
        /// Expande o bounding box para incluir as coordenadas especificadas
        /// </summary>
        /// <param name="x">Coordenada X</param>
        /// <param name="y">Coordenada Y</param>
        public void Expand(double x, double y)
        {
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
        }

        /// <summary>
        /// Expande o bounding box para incluir outro bounding box
        /// </summary>
        /// <param name="other">Outro bounding box</param>
        public void Expand(BoundingBox other)
        {
            if (other == null)
            {
                return;
            }

            MinX = Math.Min(MinX, other.MinX);
            MinY = Math.Min(MinY, other.MinY);
            MaxX = Math.Max(MaxX, other.MaxX);
            MaxY = Math.Max(MaxY, other.MaxY);
        }

        /// <summary>
        /// Verifica se o bounding box é válido (coordenadas máximas maiores que mínimas)
        /// </summary>
        public bool IsValid()
        {
            return MaxX >= MinX && MaxY >= MinY &&
                   !double.IsInfinity(MinX) && !double.IsInfinity(MinY) &&
                   !double.IsInfinity(MaxX) && !double.IsInfinity(MaxY) &&
                   !double.IsNaN(MinX) && !double.IsNaN(MinY) &&
                   !double.IsNaN(MaxX) && !double.IsNaN(MaxY);
        }

        /// <summary>
        /// Obtém a largura do bounding box
        /// </summary>
        public double Width => MaxX - MinX;

        /// <summary>
        /// Obtém a altura do bounding box
        /// </summary>
        public double Height => MaxY - MinY;

        /// <summary>
        /// Obtém o centro do bounding box
        /// </summary>
        /// <returns>Um par com as coordenadas X e Y do centro</returns>
        public (double X, double Y) GetCenter()
        {
            return ((MinX + MaxX) / 2, (MinY + MaxY) / 2);
        }

        /// <summary>
        /// Aplica um buffer (expansão) ao bounding box
        /// </summary>
        /// <param name="bufferPercentage">Porcentagem de expansão (ex: 0.1 para 10%)</param>
        /// <returns>Um novo bounding box expandido</returns>
        public BoundingBox Buffer(double bufferPercentage)
        {
            double bufferX = Width * bufferPercentage;
            double bufferY = Height * bufferPercentage;

            return new BoundingBox(
                MinX - bufferX,
                MinY - bufferY,
                MaxX + bufferX,
                MaxY + bufferY
            );
        }
    }
} 