using System.IO;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface para provedor de tiles de mapas
    /// </summary>
    public interface ITileProvider
    {
        /// <summary>
        /// Tamanho do tile em pixels
        /// </summary>
        int TileSize { get; }

        /// <summary>
        /// Obtém um tile do mapa para as coordenadas, zoom e índice especificados
        /// </summary>
        /// <param name="x">Índice X do tile</param>
        /// <param name="y">Índice Y do tile</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Stream com a imagem do tile</returns>
        Task<Stream> GetTileAsync(int x, int y, int zoom);

        /// <summary>
        /// Calcula o nível de zoom adequado para exibir o bounding box especificado
        /// </summary>
        /// <param name="boundingBox">Bounding box a ser exibido</param>
        /// <param name="width">Largura da imagem em pixels</param>
        /// <param name="height">Altura da imagem em pixels</param>
        /// <returns>Nível de zoom recomendado</returns>
        int CalculateOptimalZoomLevel(BoundingBox boundingBox, int width, int height);

        /// <summary>
        /// Converte coordenadas geográficas (latitude, longitude) para coordenadas de pixel na imagem
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <param name="width">Largura da imagem em pixels</param>
        /// <param name="height">Altura da imagem em pixels</param>
        /// <param name="boundingBox">Bounding box sendo visualizado</param>
        /// <returns>Coordenadas X e Y em pixels</returns>
        (double X, double Y) GeoToPixel(
            double longitude,
            double latitude,
            int zoom,
            int width,
            int height,
            BoundingBox boundingBox);
    }
}