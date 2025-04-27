using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using GeoJsonRenderer.Infrastructure.Interfaces;
using GeoJsonRenderer.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace GeoJsonRenderer.Infrastructure.Mapping
{
    /// <summary>
    /// Implementação do provedor de tiles para mapa de fundo
    /// </summary>
    public class TileProvider : ITileProvider, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _tileServerUrl;
        private readonly int _minZoom = 0;
        private readonly int _maxZoom = 19;
        private readonly ILogger<TileProvider> _logger;

        /// <summary>
        /// Tamanho do tile em pixels
        /// </summary>
        public int TileSize { get; } = 256;

        /// <summary>
        /// Construtor padrão
        /// </summary>
        /// <param name="logger">Logger para depuração</param>
        /// <param name="tileServerUrl">URL do servidor de tiles</param>
        public TileProvider(ILogger<TileProvider> logger, string tileServerUrl = null)
        {
            _tileServerUrl = string.IsNullOrEmpty(tileServerUrl)
                ? "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
                : tileServerUrl;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GeoJsonRenderer/1.0");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Obtém um tile do mapa para as coordenadas, zoom e índice especificados
        /// </summary>
        /// <param name="x">Índice X do tile</param>
        /// <param name="y">Índice Y do tile</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Stream com a imagem do tile</returns>
        public async Task<Stream> GetTileAsync(int x, int y, int zoom)
        {
            // Validação de parâmetros
            if (zoom < _minZoom || zoom > _maxZoom)
            {
                throw new ArgumentOutOfRangeException(nameof(zoom), $"The zoom level must be between {_minZoom} and {_maxZoom}");
            }

            int maxIndex = (1 << zoom) - 1; // Número máximo de tiles para este nível de zoom

            if (x < 0 || x > maxIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"The X index must be between 0 and {maxIndex}");
            }

            if (y < 0 || y > maxIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(y), $"The Y index must be between 0 and {maxIndex}");
            }

            // Constrói a URL do tile substituindo os marcadores {z}, {x} e {y}
            string url = _tileServerUrl
                .Replace("{z}", zoom.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            // Faz a requisição HTTP
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode(); // Lança exceção se o status code não for de sucesso
            
            // Retorna o stream da resposta
            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Calcula o nível de zoom adequado para exibir o bounding box especificado
        /// </summary>
        /// <param name="boundingBox">Bounding box a ser exibido</param>
        /// <param name="width">Largura da imagem em pixels</param>
        /// <param name="height">Altura da imagem em pixels</param>
        /// <returns>Nível de zoom recomendado</returns>
        public int CalculateOptimalZoomLevel(BoundingBox boundingBox, int width, int height)
        {
            if (boundingBox == null)
            {
                throw new ArgumentNullException(nameof(boundingBox));
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and height must be positive values");
            }

            int maxAllowedZoom = _maxZoom;

            // Calcula o centro do bounding box
            var center = boundingBox.GetCenter();

            // Calcula a largura e altura do bounding box em graus
            double boxWidth = boundingBox.Width;
            double boxHeight = boundingBox.Height;

            // Para debugging
            double maxDim = Math.Max(boxWidth, boxHeight);
            if (_logger != null)
            {
                _logger.LogDebug($"BoundingBox max dimension: {maxDim}");
            }

            // Estratégia progressiva para áreas pequenas
            // Para áreas extremamente pequenas, aplicamos zoom máximo
            // Para áreas um pouco maiores, aplicamos zoom progressivamente menor
            if (maxDim < 0.01)
            {
                // Áreas extremamente pequenas (< 0.01°) recebem zoom máximo
                if (_logger != null)
                {
                    _logger.LogDebug($"Extremely small area (maxDim={maxDim:F6}), using maximum zoom of {maxAllowedZoom}");
                }
                return maxAllowedZoom;
            }
            else if (maxDim < 0.05)
            {
                // Áreas muito pequenas (0.01° - 0.05°) recebem zoom 17
                if (_logger != null)
                {
                    _logger.LogDebug($"Very small area (maxDim={maxDim:F6}), using zoom 17");
                }
                return 17;
            }
            else if (maxDim < 0.2)
            {
                // Áreas pequenas (0.05° - 0.2°) recebem zoom 16
                if (_logger != null)
                {
                    _logger.LogDebug($"Small area (maxDim={maxDim:F6}), using zoom 16");
                }
                return 16;
            }

            // Define uma porcentagem de buffer que se ajusta ao tamanho da área
            double bufferPercentage = 0.1; // 10% padrão
            
            // Aplicar um buffer para adicionar contexto à visualização
            boundingBox = boundingBox.Buffer(bufferPercentage);

            // Recalcula as dimensões com o buffer aplicado
            boxWidth = boundingBox.Width;
            boxHeight = boundingBox.Height;

            // Coordenadas do bounding box ajustado
            double west = boundingBox.MinX;
            double south = boundingBox.MinY;
            double east = boundingBox.MaxX;
            double north = boundingBox.MaxY;

            if (_logger != null)
            {
                _logger.LogDebug($"Adjusted BoundingBox: West={west}, South={south}, East={east}, North={north}");
            }

            for (int zoom = maxAllowedZoom; zoom >= 0; zoom--)
            {
                // Coordenadas em pixels nos extremos do bounding box
                double westPixel = LongitudeToPixelX(west, zoom);
                double eastPixel = LongitudeToPixelX(east, zoom);
                double northPixel = LatitudeToPixelY(north, zoom);
                double southPixel = LatitudeToPixelY(south, zoom);

                // Dimensões da área representada em pixels no zoom atual
                double pixelWidth = Math.Abs(eastPixel - westPixel);
                double pixelHeight = Math.Abs(southPixel - northPixel);

                // Razão entre as dimensões da área em pixels e as dimensões da tela
                double widthRatio = pixelWidth / width;
                double heightRatio = pixelHeight / height;

                // Escolhe a razão que melhor encaixa na tela (considerando a que mais limita)
                double score = Math.Max(widthRatio, heightRatio);

                if (_logger != null)
                {
                    _logger.LogDebug($"Zoom: {zoom}, Score: {score}, PixelWidth: {pixelWidth}, PixelHeight: {pixelHeight}");
                }

                // Se a razão for menor que 0.5, a imagem ocupará menos da metade da tela
                // Nesse caso, queremos aumentar o zoom
                if (score < 0.5)
                {
                    if (_logger != null)
                    {
                        _logger.LogDebug($"Selected zoom level: {zoom}");
                    }
                    return zoom;
                }
            }

            // Se nenhum zoom for adequado, usa um valor padrão
            if (_logger != null)
            {
                _logger.LogDebug("No suitable zoom level found, defaulting to 10");
            }
            return 10;
        }

        /// <summary>
        /// Converte longitude para coordenada X em pixels
        /// </summary>
        /// <param name="longitude">Longitude em graus</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Coordenada X em pixels</returns>
        private double LongitudeToPixelX(double longitude, int zoom)
        {
            // Fórmula para converter longitude para coordenada X em pixels no sistema de projeção Web Mercator
            double worldSize = Math.Pow(2, zoom) * TileSize;
            double x = (longitude + 180.0) / 360.0 * worldSize;
            return x;
        }

        /// <summary>
        /// Converte latitude para coordenada Y em pixels
        /// </summary>
        /// <param name="latitude">Latitude em graus</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Coordenada Y em pixels</returns>
        private double LatitudeToPixelY(double latitude, int zoom)
        {
            // Garantir que a latitude esteja dentro de limites aceitáveis para evitar distorções
            latitude = Math.Max(Math.Min(latitude, 85.0511), -85.0511);
            
            // Fórmula para converter latitude para coordenada Y em pixels no sistema de projeção Web Mercator
            double latRad = latitude * Math.PI / 180.0;
            double worldSize = Math.Pow(2, zoom) * TileSize;
            double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * worldSize;
            return y;
        }

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
        public (double X, double Y) GeoToPixel(
            double longitude, 
            double latitude, 
            int zoom, 
            int width, 
            int height, 
            BoundingBox boundingBox)
        {
            if (boundingBox == null || !boundingBox.IsValid())
            {
                throw new ArgumentException("O bounding box não pode ser nulo ou inválido.", nameof(boundingBox));
            }

            // Garantir que as coordenadas estejam dentro de limites aceitáveis para evitar distorções
            latitude = Math.Max(Math.Min(latitude, 85.0511), -85.0511);

            // Calcular posição no mundo do OSM
            double worldSize = Math.Pow(2, zoom) * TileSize;
            double worldX = (longitude + 180.0) / 360.0 * worldSize;
            double latRad = latitude * Math.PI / 180.0;
            double worldY = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * worldSize;

            // Converter bounding box para coordenadas de mundo
            double bbMinLat = Math.Max(boundingBox.MinY, -85.0511);
            double bbMaxLat = Math.Min(boundingBox.MaxY, 85.0511);
            double bbMinLatRad = bbMinLat * Math.PI / 180.0;
            double bbMaxLatRad = bbMaxLat * Math.PI / 180.0;
            double bbMinWorldX = (boundingBox.MinX + 180.0) / 360.0 * worldSize;
            double bbMaxWorldX = (boundingBox.MaxX + 180.0) / 360.0 * worldSize;
            double bbMinWorldY = (1.0 - Math.Log(Math.Tan(bbMaxLatRad) + 1.0 / Math.Cos(bbMaxLatRad)) / Math.PI) / 2.0 * worldSize;
            double bbMaxWorldY = (1.0 - Math.Log(Math.Tan(bbMinLatRad) + 1.0 / Math.Cos(bbMinLatRad)) / Math.PI) / 2.0 * worldSize;

            // Calcular escala
            double bbWorldWidth = bbMaxWorldX - bbMinWorldX;
            double bbWorldHeight = bbMaxWorldY - bbMinWorldY;
            double scaleX = width / bbWorldWidth;
            double scaleY = height / bbWorldHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Calcular offsets para centralização
            double offsetX = (width - bbWorldWidth * scale) / 2.0;
            double offsetY = (height - bbWorldHeight * scale) / 2.0;

            // Calcular coordenadas de pixel com offset
            double pixelX = (worldX - bbMinWorldX) * scale + offsetX;
            double pixelY = (worldY - bbMinWorldY) * scale + offsetY;

            return (pixelX, pixelY);
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}