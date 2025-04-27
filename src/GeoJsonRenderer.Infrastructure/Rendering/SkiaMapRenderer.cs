using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Interfaces;
using GeoJsonRenderer.Domain.Models;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace GeoJsonRenderer.Infrastructure.Rendering
{
    /// <summary>
    /// Implementação do renderizador de mapas usando SkiaSharp
    /// </summary>
    public class SkiaMapRenderer : IMapRenderer
    {
        private readonly ITileProvider _tileProvider;

        /// <summary>
        /// Construtor padrão
        /// </summary>
        /// <param name="tileProvider">Provedor de tiles</param>
        public SkiaMapRenderer(ITileProvider tileProvider)
        {
            _tileProvider = tileProvider ?? throw new ArgumentNullException(nameof(tileProvider));
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

            // Criar superfície para desenho
            using (var surface = SKSurface.Create(new SKImageInfo(options.Width, options.Height)))
            {
                var canvas = surface.Canvas;
                
                // Limpar o canvas com um fundo branco
                canvas.Clear(SKColors.White);

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
                return;

            int tileSize = _tileProvider.TileSize;
            double worldSize = Math.Pow(2, zoomLevel) * tileSize;

            // Limita latitudes para projeção válida
            double minLat = Math.Max(boundingBox.MinY, -85.0511);
            double maxLat = Math.Min(boundingBox.MaxY, 85.0511);
            double minLatRad = minLat * Math.PI / 180.0;
            double maxLatRad = maxLat * Math.PI / 180.0;

            // Calcula coordenadas mundiais (pixels) do bounding box
            double bbMinWorldX = (boundingBox.MinX + 180.0) / 360.0 * worldSize;
            double bbMaxWorldX = (boundingBox.MaxX + 180.0) / 360.0 * worldSize;
            double bbMinWorldY = (1.0 - Math.Log(Math.Tan(maxLatRad) + 1.0 / Math.Cos(maxLatRad)) / Math.PI) / 2.0 * worldSize;
            double bbMaxWorldY = (1.0 - Math.Log(Math.Tan(minLatRad) + 1.0 / Math.Cos(minLatRad)) / Math.PI) / 2.0 * worldSize;

            double bbWorldWidth = bbMaxWorldX - bbMinWorldX;
            double bbWorldHeight = bbMaxWorldY - bbMinWorldY;

            // Define escala e offsets para centralizar
            double scaleX = width / bbWorldWidth;
            double scaleY = height / bbWorldHeight;
            double scale = Math.Min(scaleX, scaleY);
            double offsetX = (width - bbWorldWidth * scale) / 2;
            double offsetY = (height - bbWorldHeight * scale) / 2;

            // Calcula extents em coordenadas de mundo para toda a tela
            double viewMinWorldX = bbMinWorldX - offsetX / scale;
            double viewMaxWorldX = bbMinWorldX + (width - offsetX) / scale;
            double viewMinWorldY = bbMinWorldY - offsetY / scale;
            double viewMaxWorldY = bbMinWorldY + (height - offsetY) / scale;
            
            int minTileX = Math.Max(0, (int)Math.Floor(viewMinWorldX / tileSize) - 1);
            int maxTileX = Math.Min((1 << zoomLevel) - 1, (int)Math.Floor(viewMaxWorldX / tileSize) + 1);
            int minTileY = Math.Max(0, (int)Math.Floor(viewMinWorldY / tileSize) - 1);
            int maxTileY = Math.Min((1 << zoomLevel) - 1, (int)Math.Floor(viewMaxWorldY / tileSize) + 1);

            // Baixa e desenha cada tile
            var tasks = new List<Task<(int x, int y, SKBitmap? bmp)>>();
            for (int tx = minTileX; tx <= maxTileX; tx++)
                for (int ty = minTileY; ty <= maxTileY; ty++)
                    tasks.Add(DownloadTileBitmapAsync(tx, ty, zoomLevel));

            var results = await Task.WhenAll(tasks);
            foreach (var (tx, ty, bmp) in results.Where(r => r.bmp != null))
            {
                double tileWorldX = tx * tileSize;
                double tileWorldY = ty * tileSize;
                float x = (float)((tileWorldX - bbMinWorldX) * scale + offsetX);
                float y = (float)((tileWorldY - bbMinWorldY) * scale + offsetY);
                var dest = new SKRect(x, y, x + (float)(tileSize * scale), y + (float)(tileSize * scale));
                canvas.DrawBitmap(bmp!, dest);
            }
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
            catch
            {
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
            LabelConfig labelConfig,
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
            var pixelCoords = _tileProvider.GeoToPixel(
                point.X, point.Y, zoomLevel, options.Width, options.Height, boundingBox);
            
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
                    var pixelCoords = _tileProvider.GeoToPixel(
                        coord.X, coord.Y, zoomLevel, options.Width, options.Height, boundingBox);
                    
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
                    var pixelCoords = _tileProvider.GeoToPixel(
                        coord.X, coord.Y, zoomLevel, options.Width, options.Height, boundingBox);
                    
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
                        var pixelCoords = _tileProvider.GeoToPixel(
                            coord.X, coord.Y, zoomLevel, options.Width, options.Height, boundingBox);
                        
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
            string labelText = feature.GetPropertyValueAsString(labelConfig.PropertyName);
            if (string.IsNullOrEmpty(labelText))
            {
                return; // Não há texto para exibir
            }
            
            // Calcular o ponto central da geometria para posicionar o rótulo
            var centroid = geometry.Centroid;
            var pixelCoords = _tileProvider.GeoToPixel(
                centroid.X, centroid.Y, zoomLevel, options.Width, options.Height, boundingBox);
            
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
                throw new ArgumentException("O bounding box não pode ser nulo ou inválido.", nameof(boundingBox));
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
                throw new ArgumentException("O caminho do arquivo de saída é obrigatório.", nameof(options.OutputFilePath));
            }
        }
    }
} 