using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Models;
using GeoJsonRenderer.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace GeoJsonRenderer.Infrastructure.Mapping
{
    /// <summary>
    /// Renderizador de fundo do mapa usando tiles e projeção Web Mercator
    /// </summary>
    public class MapBackgroundRenderer
    {
        private readonly ITileProvider _tileProvider;
        private readonly ILogger<MapBackgroundRenderer> _logger;

        public MapBackgroundRenderer(ITileProvider tileProvider, ILogger<MapBackgroundRenderer> logger)
        {
            _tileProvider = tileProvider ?? throw new ArgumentNullException(nameof(tileProvider));
            _logger = logger;
        }

        public async Task<SKImage> RenderBackground(BoundingBox boundingBox, int width, int height, int? forcedZoomLevel = null)
        {
            _logger?.LogInformation("Iniciando renderização do mapa de fundo");

            // Determina o nível de zoom
            int zoom = forcedZoomLevel ?? _tileProvider.CalculateOptimalZoomLevel(boundingBox, width, height);
            _logger?.LogInformation($"Usando nível de zoom: {zoom}");

            // Cria superfície e canvas
            var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            // Limpa com fundo branco
            canvas.Clear(SKColors.White);

            int tileSize = _tileProvider.TileSize;
            double worldSize = Math.Pow(2, zoom) * tileSize;

            // Projeta boundingBox para coordenadas de mundo
            double minLat = Math.Max(boundingBox.MinY, -85.0511);
            double maxLat = Math.Min(boundingBox.MaxY, 85.0511);

            double minLatRad = minLat * Math.PI / 180.0;
            double maxLatRad = maxLat * Math.PI / 180.0;

            double bbMinWorldX = (boundingBox.MinX + 180.0) / 360.0 * worldSize;
            double bbMaxWorldX = (boundingBox.MaxX + 180.0) / 360.0 * worldSize;
            double bbMinWorldY = (1.0 - Math.Log(Math.Tan(maxLatRad) + 1.0 / Math.Cos(maxLatRad)) / Math.PI) / 2.0 * worldSize;
            double bbMaxWorldY = (1.0 - Math.Log(Math.Tan(minLatRad) + 1.0 / Math.Cos(minLatRad)) / Math.PI) / 2.0 * worldSize;

            double bbWorldWidth = bbMaxWorldX - bbMinWorldX;
            double bbWorldHeight = bbMaxWorldY - bbMinWorldY;

            // Calcula escalas e offsets centrais
            double scaleX = width / bbWorldWidth;
            double scaleY = height / bbWorldHeight;
            double scale = Math.Min(scaleX, scaleY);

            double offsetX = (width - bbWorldWidth * scale) / 2;
            double offsetY = (height - bbWorldHeight * scale) / 2;

            _logger?.LogInformation($"Escala: {scale}, Offset: ({offsetX}, {offsetY})");

            // Baixa e desenha tiles
            var tasks = new List<Task<(int x, int y, SKImage? image)>>();
            int minTileX = (int)(bbMinWorldX / tileSize);
            int maxTileX = (int)(bbMaxWorldX / tileSize);
            int minTileY = (int)(bbMinWorldY / tileSize);
            int maxTileY = (int)(bbMaxWorldY / tileSize);

            minTileX = Math.Max(0, minTileX - 1);
            maxTileX = Math.Min((1 << zoom) - 1, maxTileX + 1);
            minTileY = Math.Max(0, minTileY - 1);
            maxTileY = Math.Min((1 << zoom) - 1, maxTileY + 1);

            for (int tx = minTileX; tx <= maxTileX; tx++)
                for (int ty = minTileY; ty <= maxTileY; ty++)
                    tasks.Add(DownloadAndProcessTile(tx, ty, zoom));

            var completed = await Task.WhenAll(tasks);

            foreach (var (tx, ty, image) in completed.Where(t => t.image != null))
            {
                double tileWorldX = tx * tileSize;
                double tileWorldY = ty * tileSize;

                double x = (tileWorldX - bbMinWorldX) * scale + offsetX;
                double y = (tileWorldY - bbMinWorldY) * scale + offsetY;

                canvas.DrawImage(image!, new SKRect((float)x, (float)y, (float)(x + tileSize * scale), (float)(y + tileSize * scale)));
            }

            _logger?.LogInformation("Renderização do mapa de fundo concluída");
            return surface.Snapshot();
        }

        private async Task<(int x, int y, SKImage? image)> DownloadAndProcessTile(int x, int y, int zoom)
        {
            const int maxRetries = 3;
            int retry = 0;
            while (retry < maxRetries)
            {
                try
                {
                    using var stream = await _tileProvider.GetTileAsync(x, y, zoom);
                    if (stream != null)
                    {
                        using var ms = new SKManagedStream(stream);
                        var img = SKImage.FromEncodedData(ms);
                        return (x, y, img);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Erro ao baixar tile ({x},{y},{zoom}): {ex.Message}, tentativa {retry + 1}/{maxRetries}");
                }
                retry++;
                if (retry < maxRetries)
                    await Task.Delay(100 * retry);
            }
            _logger?.LogError($"Falha ao baixar tile ({x},{y},{zoom}) depois de {maxRetries} tentativas");
            return (x, y, null);
        }
    }
} 