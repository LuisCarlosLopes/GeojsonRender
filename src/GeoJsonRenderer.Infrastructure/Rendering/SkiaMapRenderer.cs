using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Interfaces;
using GeoJsonRenderer.Domain.Models;
using GeoJsonRenderer.Infrastructure.Mapping;
using NetTopologySuite.Geometries;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace GeoJsonRenderer.Infrastructure.Rendering
{
    /// <summary>
    /// Implementação do renderizador de mapas usando SkiaSharp
    /// </summary>
    public class SkiaMapRenderer : IMapRenderer
    {
        private readonly ITileProvider _tileProvider;
        private readonly ILogger<SkiaMapRenderer> _logger;
        private TransformParameters? _currentTransform;

        /// <summary>
        /// Construtor padrão
        /// </summary>
        /// <param name="tileProvider">Provedor de tiles</param>
        /// <param name="logger">Logger para debug</param>
        public SkiaMapRenderer(ITileProvider tileProvider, ILogger<SkiaMapRenderer> logger)
        {
            _tileProvider = tileProvider ?? throw new ArgumentNullException(nameof(tileProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Renderiza um mapa com as feições geográficas e salva como imagem
        /// </summary>
        /// <param name="features">Lista de feições a serem renderizadas</param>
        /// <param name="boundingBox">Bounding box para centralização do mapa</param>
        /// <param name="styleConfig">Configuração de estilos para renderização</param>
        /// <param name="options">Opções de renderização</param>
        /// <returns>Caminho do arquivo de imagem gerado</returns>
        public async Task<string> RenderMapAsync(
            List<GeoFeature> features,
            BoundingBox boundingBox,
            StyleConfig styleConfig,
            RenderOptions options)
        {
            ValidateInputs(features, boundingBox, styleConfig, options);

            // Calcular o nível de zoom adequado
            int zoomLevel = options.ZoomLevel ?? _tileProvider.CalculateOptimalZoomLevel(
                boundingBox,
                options.Width,
                options.Height);

            // Calcular os parâmetros de transformação unificados que serão usados por tiles e feições
            _currentTransform = CoordinateTransform.CalculateTransformParameters(
                boundingBox, zoomLevel, options.Width, options.Height);

            // Criar superfície para desenho com configurações de alta qualidade
            var imageInfo = new SKImageInfo(options.Width, options.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var surface = SKSurface.Create(imageInfo))
            {
                var canvas = surface.Canvas;

                // Configurar canvas para máxima qualidade
                canvas.Clear(SKColors.White);

                // Habilitar anti-aliasing e filtros de alta qualidade
                var highQualityPaint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High,
                    IsDither = true
                };

                // Renderizar o mapa de fundo (tiles)
                if (options.ShowMapBackground)
                {
                    await RenderMapBackgroundAsync(canvas, boundingBox, zoomLevel, options.Width, options.Height);
                }

                // Renderizar as feições geográficas
                await RenderFeaturesAsync(canvas, features, boundingBox, styleConfig, options, zoomLevel);

                // Gerar a imagem final
                using (var image = surface.Snapshot())
                using (var data = image.Encode(
                    options.Format == ImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
                    options.Format == ImageFormat.Jpeg ? options.Quality : 100))
                {
                    using (var stream = File.Create(options.OutputFilePath))
                    {
                        data.SaveTo(stream);
                    }
                }

                highQualityPaint.Dispose();
            }

            return options.OutputFilePath;
        }

        /// <summary>
        /// Renderiza o mapa de fundo (tiles) na imagem
        /// </summary>
        private async Task RenderMapBackgroundAsync(
            SKCanvas canvas,
            BoundingBox boundingBox,
            int zoomLevel,
            int width,
            int height)
        {
            // Valida bounding box
            if (boundingBox == null || !boundingBox.IsValid())
            {
                _logger.LogWarning("BoundingBox inválido - não renderizando mapa de fundo");
                return;
            }

            _logger.LogDebug($"Iniciando renderização do mapa de fundo - Zoom: {zoomLevel}, Size: {width}x{height}");
            _logger.LogDebug("Configurações de alta qualidade habilitadas: Anti-aliasing, FilterQuality.High, Dithering");

            // Usa a transformação unificada já calculada
            var transform = _currentTransform;

            if (transform == null)
                throw new InvalidOperationException("Transformação não foi calculada. Chame CalculateTransformParameters primeiro.");

            // Calcula extents em coordenadas de mundo para toda a tela
            double viewMinWorldX = transform.BbMinWorldX - transform.OffsetX / transform.Scale;
            double viewMaxWorldX = transform.BbMinWorldX + (width - transform.OffsetX) / transform.Scale;
            double viewMinWorldY = transform.BbMinWorldY - transform.OffsetY / transform.Scale;
            double viewMaxWorldY = transform.BbMinWorldY + (height - transform.OffsetY) / transform.Scale;

            int tileSize = CoordinateTransform.TILE_SIZE;
            int minTileX = Math.Max(0, (int)Math.Floor(viewMinWorldX / tileSize) - 1);
            int maxTileX = Math.Min((1 << zoomLevel) - 1, (int)Math.Floor(viewMaxWorldX / tileSize) + 1);
            int minTileY = Math.Max(0, (int)Math.Floor(viewMinWorldY / tileSize) - 1);
            int maxTileY = Math.Min((1 << zoomLevel) - 1, (int)Math.Floor(viewMaxWorldY / tileSize) + 1);

            int totalTiles = (maxTileX - minTileX + 1) * (maxTileY - minTileY + 1);
            _logger.LogDebug($"Baixando {totalTiles} tiles (X: {minTileX}-{maxTileX}, Y: {minTileY}-{maxTileY})...");

            // Baixa e desenha cada tile
            var tasks = new List<Task<(int x, int y, SKBitmap? bmp)>>();
            for (int tx = minTileX; tx <= maxTileX; tx++)
                for (int ty = minTileY; ty <= maxTileY; ty++)
                    tasks.Add(DownloadTileBitmapAsync(tx, ty, zoomLevel));

            var results = await Task.WhenAll(tasks);

            int successfulTiles = results.Count(r => r.bmp != null);
            _logger.LogDebug($"Renderizando {successfulTiles}/{results.Length} tiles baixados com sucesso");

            // Configurar paint para renderização de alta qualidade dos tiles
            using var tilePaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                IsDither = true,
                BlendMode = SKBlendMode.SrcOver
            };

            foreach (var (tx, ty, bmp) in results.Where(r => r.bmp != null))
            {
                double tileWorldX = tx * tileSize;
                double tileWorldY = ty * tileSize;

                // Usa a mesma transformação para posicionar o tile
                var (pixelX, pixelY) = CoordinateTransform.WorldToPixel(tileWorldX, tileWorldY, transform);

                var dest = new SKRect(
                    (float)pixelX,
                    (float)pixelY,
                    (float)(pixelX + tileSize * transform.Scale),
                    (float)(pixelY + tileSize * transform.Scale));

                // Renderizar tile com configurações de alta qualidade
                canvas.DrawBitmap(bmp!, dest, tilePaint);

                // Liberar bitmap após uso para economizar memória
                bmp!.Dispose();
            }
        }

        /// <summary>
        /// Converte coordenadas geográficas para pixels usando a transformação unificada
        /// </summary>
        /// <param name="longitude">Longitude em graus decimais</param>
        /// <param name="latitude">Latitude em graus decimais</param>
        /// <param name="zoomLevel">Nível de zoom</param>
        /// <returns>Coordenadas de pixel (X, Y)</returns>
        private (double X, double Y) GeoToPixelUnified(double longitude, double latitude, int zoomLevel)
        {
            if (_currentTransform == null)
                throw new InvalidOperationException("Transformação não foi calculada. Chame CalculateTransformParameters primeiro.");

            // Converte coordenadas geográficas para coordenadas mundiais
            var (worldX, worldY) = CoordinateTransform.GeoToWorld(longitude, latitude, zoomLevel);

            // Aplica a transformação unificada
            return CoordinateTransform.WorldToPixel(worldX, worldY, _currentTransform);
        }

        // Adicionar método auxiliar para download de tile e decodificação em SKBitmap
        private async Task<(int x, int y, SKBitmap? bmp)> DownloadTileBitmapAsync(int x, int y, int zoom)
        {
            try
            {
                using var stream = await _tileProvider.GetTileAsync(x, y, zoom);
                var bmp = SKBitmap.Decode(stream);
                return (x, y, bmp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Erro ao baixar tile {x},{y}: {ex.Message}");
                return (x, y, null);
            }
        }

        /// <summary>
        /// Renderiza as feições geográficas na imagem
        /// </summary>
        private Task RenderFeaturesAsync(
            SKCanvas canvas,
            List<GeoFeature> features,
            BoundingBox boundingBox,
            StyleConfig styleConfig,
            RenderOptions options,
            int zoomLevel)
        {
            // Primeiro renderiza as feições não filtradas (em segundo plano)
            foreach (var feature in features.FindAll(f => !f.IsFiltered))
            {
                RenderFeature(canvas, feature, boundingBox, styleConfig.DefaultStyle, false,
                    null, options, zoomLevel);
            }

            // Depois renderiza as feições filtradas (em destaque)
            foreach (var feature in features.FindAll(f => f.IsFiltered))
            {
                RenderFeature(canvas, feature, boundingBox, styleConfig.HighlightStyle, true,
                    styleConfig.LabelConfig, options, zoomLevel);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Renderiza uma feição geográfica específica
        /// </summary>
        private void RenderFeature(
            SKCanvas canvas,
            GeoFeature feature,
            BoundingBox boundingBox,
            FeatureStyle style,
            bool isFiltered,
            LabelConfig? labelConfig,
            RenderOptions options,
            int zoomLevel)
        {
            // Verificar se é uma geometria NetTopologySuite
            if (feature.Geometry is Geometry geometry)
            {
                // Configurar o pincel de acordo com o estilo
                using (var paint = new SKPaint())
                {
                    // Estilo de preenchimento (se houver)
                    if (!string.IsNullOrEmpty(style.FillColor))
                    {
                        var fillColor = FeatureStyle.ParseColor(style.FillColor);
                        if (fillColor.HasValue)
                        {
                            paint.Color = new SKColor(
                                (byte)fillColor.Value.R,
                                (byte)fillColor.Value.G,
                                (byte)fillColor.Value.B,
                                (byte)fillColor.Value.A);
                            paint.IsStroke = false;
                            paint.IsAntialias = true;

                            // Renderizar a geometria (preenchimento)
                            RenderGeometry(canvas, geometry, boundingBox, paint, options, zoomLevel);
                        }
                    }

                    // Estilo de contorno (após preenchimento)
                    if (!string.IsNullOrEmpty(style.StrokeColor))
                    {
                        var strokeColor = FeatureStyle.ParseColor(style.StrokeColor);
                        if (strokeColor.HasValue)
                        {
                            paint.Color = new SKColor(
                                (byte)strokeColor.Value.R,
                                (byte)strokeColor.Value.G,
                                (byte)strokeColor.Value.B,
                                (byte)strokeColor.Value.A);
                            paint.StrokeWidth = style.StrokeWidth;
                            paint.IsStroke = true;
                            paint.IsAntialias = true;

                            // Renderizar a geometria (contorno)
                            RenderGeometry(canvas, geometry, boundingBox, paint, options, zoomLevel);
                        }
                    }
                }

                // Renderizar o rótulo (apenas para feições filtradas)
                if (isFiltered && labelConfig != null && labelConfig.Enabled)
                {
                    RenderLabel(canvas, feature, geometry, boundingBox, labelConfig, options, zoomLevel);
                }
            }
        }

        /// <summary>
        /// Renderiza uma geometria específica
        /// </summary>
        private void RenderGeometry(
            SKCanvas canvas,
            Geometry geometry,
            BoundingBox boundingBox,
            SKPaint paint,
            RenderOptions options,
            int zoomLevel)
        {
            switch (geometry)
            {
                case Point point:
                    RenderPoint(canvas, point, boundingBox, paint, options, zoomLevel);
                    break;

                case LineString lineString:
                    RenderLineString(canvas, lineString, boundingBox, paint, options, zoomLevel);
                    break;

                case Polygon polygon:
                    RenderPolygon(canvas, polygon, boundingBox, paint, options, zoomLevel);
                    break;

                case MultiPoint multiPoint:
                    foreach (var p in multiPoint.Geometries)
                    {
                        RenderGeometry(canvas, p, boundingBox, paint, options, zoomLevel);
                    }
                    break;

                case MultiLineString multiLineString:
                    foreach (var ls in multiLineString.Geometries)
                    {
                        RenderGeometry(canvas, ls, boundingBox, paint, options, zoomLevel);
                    }
                    break;

                case MultiPolygon multiPolygon:
                    foreach (var poly in multiPolygon.Geometries)
                    {
                        RenderGeometry(canvas, poly, boundingBox, paint, options, zoomLevel);
                    }
                    break;

                case GeometryCollection geometryCollection:
                    foreach (var geom in geometryCollection.Geometries)
                    {
                        RenderGeometry(canvas, geom, boundingBox, paint, options, zoomLevel);
                    }
                    break;
            }
        }

        /// <summary>
        /// Renderiza um ponto
        /// </summary>
        private void RenderPoint(
            SKCanvas canvas,
            Point point,
            BoundingBox boundingBox,
            SKPaint paint,
            RenderOptions options,
            int zoomLevel)
        {
            var pixelCoords = GeoToPixelUnified(point.X, point.Y, zoomLevel);

            // Para pontos, desenha um círculo
            float radius = paint.IsStroke ? 5 : 4; // Raio ligeiramente menor para preenchimento
            canvas.DrawCircle((float)pixelCoords.X, (float)pixelCoords.Y, radius, paint);
        }

        /// <summary>
        /// Renderiza uma linha
        /// </summary>
        private void RenderLineString(
            SKCanvas canvas,
            LineString lineString,
            BoundingBox boundingBox,
            SKPaint paint,
            RenderOptions options,
            int zoomLevel)
        {
            // Garante disposal do SKPath após uso
            using (var path = new SKPath())
            {
                bool first = true;

                foreach (var coord in lineString.Coordinates)
                {
                    var pixelCoords = GeoToPixelUnified(coord.X, coord.Y, zoomLevel);

                    if (first)
                    {
                        path.MoveTo((float)pixelCoords.X, (float)pixelCoords.Y);
                        first = false;
                    }
                    else
                    {
                        path.LineTo((float)pixelCoords.X, (float)pixelCoords.Y);
                    }
                }

                canvas.DrawPath(path, paint);
            }
        }

        /// <summary>
        /// Renderiza um polígono
        /// </summary>
        private void RenderPolygon(
            SKCanvas canvas,
            Polygon polygon,
            BoundingBox boundingBox,
            SKPaint paint,
            RenderOptions options,
            int zoomLevel)
        {
            // Garante disposal do SKPath após uso
            using (var path = new SKPath())
            {
                // Anel externo
                bool first = true;
                foreach (var coord in polygon.ExteriorRing.Coordinates)
                {
                    var pixelCoords = GeoToPixelUnified(coord.X, coord.Y, zoomLevel);

                    if (first)
                    {
                        path.MoveTo((float)pixelCoords.X, (float)pixelCoords.Y);
                        first = false;
                    }
                    else
                    {
                        path.LineTo((float)pixelCoords.X, (float)pixelCoords.Y);
                    }
                }
                path.Close();

                // Anéis internos (buracos)
                for (int i = 0; i < polygon.NumInteriorRings; i++)
                {
                    var ring = polygon.GetInteriorRingN(i);
                    first = true;

                    foreach (var coord in ring.Coordinates)
                    {
                        var pixelCoords = GeoToPixelUnified(coord.X, coord.Y, zoomLevel);

                        if (first)
                        {
                            path.MoveTo((float)pixelCoords.X, (float)pixelCoords.Y);
                            first = false;
                        }
                        else
                        {
                            path.LineTo((float)pixelCoords.X, (float)pixelCoords.Y);
                        }
                    }
                    path.Close();
                }

                canvas.DrawPath(path, paint);
            }
        }

        /// <summary>
        /// Renderiza um rótulo para uma feição
        /// </summary>
        private void RenderLabel(
            SKCanvas canvas,
            GeoFeature feature,
            Geometry geometry,
            BoundingBox boundingBox,
            LabelConfig labelConfig,
            RenderOptions options,
            int zoomLevel)
        {
            if (string.IsNullOrEmpty(labelConfig.PropertyName))
            {
                return; // Não há propriedade para usar como rótulo
            }

            // Obter o valor da propriedade para o rótulo
            string labelText = feature.GetPropertyAsString(labelConfig.PropertyName) ?? string.Empty;
            if (string.IsNullOrEmpty(labelText))
            {
                return; // Não há texto para exibir
            }

            // Calcular o ponto central da geometria para posicionar o rótulo
            var centroid = geometry.Centroid;
            var pixelCoords = GeoToPixelUnified(centroid.X, centroid.Y, zoomLevel);

            // Configurar o pincel para o texto
            using (var textPaint = new SKPaint())
            {
                // Configurar fonte e cor
                textPaint.TextSize = labelConfig.FontSize;
                textPaint.IsAntialias = true;
                textPaint.TextAlign = SKTextAlign.Center;

                var fontColor = FeatureStyle.ParseColor(labelConfig.FontColor);
                if (fontColor.HasValue)
                {
                    textPaint.Color = new SKColor(
                        (byte)fontColor.Value.R,
                        (byte)fontColor.Value.G,
                        (byte)fontColor.Value.B,
                        (byte)fontColor.Value.A);
                }
                else
                {
                    textPaint.Color = SKColors.Black;
                }

                // Se habilitado, desenhar o halo (contorno) do texto primeiro
                if (labelConfig.Halo)
                {
                    using (var haloPaint = new SKPaint())
                    {
                        haloPaint.TextSize = labelConfig.FontSize;
                        haloPaint.IsAntialias = true;
                        haloPaint.TextAlign = SKTextAlign.Center;
                        haloPaint.Style = SKPaintStyle.Stroke;
                        haloPaint.StrokeWidth = labelConfig.HaloWidth;

                        var haloColor = FeatureStyle.ParseColor(labelConfig.HaloColor);
                        if (haloColor.HasValue)
                        {
                            haloPaint.Color = new SKColor(
                                (byte)haloColor.Value.R,
                                (byte)haloColor.Value.G,
                                (byte)haloColor.Value.B,
                                (byte)haloColor.Value.A);
                        }
                        else
                        {
                            haloPaint.Color = SKColors.White;
                        }

                        canvas.DrawText(labelText, (float)pixelCoords.X, (float)pixelCoords.Y, haloPaint);
                    }
                }

                // Desenhar o texto
                canvas.DrawText(labelText, (float)pixelCoords.X, (float)pixelCoords.Y, textPaint);
            }
        }

        /// <summary>
        /// Valida os parâmetros de entrada
        /// </summary>
        private void ValidateInputs(List<GeoFeature> features, BoundingBox boundingBox, StyleConfig styleConfig, RenderOptions options)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            if (boundingBox == null || !boundingBox.IsValid())
            {
                throw new ArgumentException("The bounding box cannot be null or invalid.", nameof(boundingBox));
            }

            if (styleConfig == null)
            {
                throw new ArgumentNullException(nameof(styleConfig));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.OutputFilePath))
            {
                throw new ArgumentException("The output file path is required.", nameof(options.OutputFilePath));
            }
        }
    }
}