using System;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Infrastructure.Mapping
{
    /// <summary>
    /// Classe responsável por transformações de coordenadas entre diferentes sistemas
    /// Centraliza a lógica de projeção para garantir consistência entre tiles e feições
    /// </summary>
    public static class CoordinateTransform
    {
        /// <summary>
        /// Tamanho padrão de um tile em pixels
        /// </summary>
        public const int TILE_SIZE = 256;

        /// <summary>
        /// Limite máximo de latitude para projeção Web Mercator (evita distorções nos polos)
        /// </summary>
        public const double MAX_LATITUDE = 85.0511;

        /// <summary>
        /// Limite mínimo de latitude para projeção Web Mercator
        /// </summary>
        public const double MIN_LATITUDE = -85.0511;

        /// <summary>
        /// Converte longitude para coordenada X em pixels no sistema Web Mercator
        /// </summary>
        /// <param name="longitude">Longitude em graus decimais</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Coordenada X em pixels</returns>
        public static double LongitudeToWorldX(double longitude, int zoom)
        {
            double worldSize = Math.Pow(2, zoom) * TILE_SIZE;
            return (longitude + 180.0) / 360.0 * worldSize;
        }

        /// <summary>
        /// Converte latitude para coordenada Y em pixels no sistema Web Mercator
        /// </summary>
        /// <param name="latitude">Latitude em graus decimais</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Coordenada Y em pixels</returns>
        public static double LatitudeToWorldY(double latitude, int zoom)
        {
            // Limita a latitude para evitar distorções extremas
            latitude = Math.Max(Math.Min(latitude, MAX_LATITUDE), MIN_LATITUDE);

            // Fórmula da projeção Web Mercator (EPSG:3857)
            double latRad = latitude * Math.PI / 180.0;
            double worldSize = Math.Pow(2, zoom) * TILE_SIZE;
            return (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * worldSize;
        }

        /// <summary>
        /// Converte coordenadas geográficas para coordenadas mundiais Web Mercator
        /// </summary>
        /// <param name="longitude">Longitude em graus decimais</param>
        /// <param name="latitude">Latitude em graus decimais</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <returns>Coordenadas mundiais (X, Y) em pixels</returns>
        public static (double X, double Y) GeoToWorld(double longitude, double latitude, int zoom)
        {
            return (LongitudeToWorldX(longitude, zoom), LatitudeToWorldY(latitude, zoom));
        }

        /// <summary>
        /// Converte coordenadas geográficas para coordenadas de pixel na tela
        /// considerando o bounding box e as dimensões da imagem
        /// </summary>
        /// <param name="longitude">Longitude em graus decimais</param>
        /// <param name="latitude">Latitude em graus decimais</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <param name="imageWidth">Largura da imagem em pixels</param>
        /// <param name="imageHeight">Altura da imagem em pixels</param>
        /// <param name="boundingBox">Bounding box da área sendo visualizada</param>
        /// <returns>Coordenadas de pixel (X, Y) na imagem</returns>
        public static (double X, double Y) GeoToPixel(
            double longitude,
            double latitude,
            int zoom,
            int imageWidth,
            int imageHeight,
            BoundingBox boundingBox)
        {
            if (boundingBox == null || !boundingBox.IsValid())
            {
                throw new ArgumentException("O bounding box não pode ser nulo ou inválido.", nameof(boundingBox));
            }

            // Converte as coordenadas para o sistema mundial
            var (worldX, worldY) = GeoToWorld(longitude, latitude, zoom);

            // Converte os limites do bounding box para coordenadas mundiais
            var (bbMinWorldX, bbMinWorldY) = GeoToWorld(boundingBox.MinX, boundingBox.MinY, zoom);
            var (bbMaxWorldX, bbMaxWorldY) = GeoToWorld(boundingBox.MaxX, boundingBox.MaxY, zoom);

            // Como Y cresce para baixo no Web Mercator, corrigimos as coordenadas
            double actualMinWorldY = Math.Min(bbMinWorldY, bbMaxWorldY);
            double actualMaxWorldY = Math.Max(bbMinWorldY, bbMaxWorldY);

            // Calcula as dimensões do bounding box em coordenadas mundiais
            double bbWorldWidth = bbMaxWorldX - bbMinWorldX;
            double bbWorldHeight = actualMaxWorldY - actualMinWorldY;

            // Calcula a escala que melhor se ajusta à imagem mantendo proporção
            double scaleX = imageWidth / bbWorldWidth;
            double scaleY = imageHeight / bbWorldHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Calcula os offsets para centralizar a imagem
            double offsetX = (imageWidth - bbWorldWidth * scale) / 2.0;
            double offsetY = (imageHeight - bbWorldHeight * scale) / 2.0;

            // Calcula as coordenadas de pixel finais
            double pixelX = (worldX - bbMinWorldX) * scale + offsetX;
            double pixelY = (worldY - actualMinWorldY) * scale + offsetY;

            return (pixelX, pixelY);
        }

        /// <summary>
        /// Calcula os parâmetros de transformação (escala e offsets) para um bounding box
        /// </summary>
        /// <param name="boundingBox">Bounding box da área</param>
        /// <param name="zoom">Nível de zoom</param>
        /// <param name="imageWidth">Largura da imagem</param>
        /// <param name="imageHeight">Altura da imagem</param>
        /// <returns>Parâmetros de transformação</returns>
        public static TransformParameters CalculateTransformParameters(
            BoundingBox boundingBox,
            int zoom,
            int imageWidth,
            int imageHeight)
        {
            if (boundingBox == null || !boundingBox.IsValid())
            {
                throw new ArgumentException("O bounding box não pode ser nulo ou inválido.", nameof(boundingBox));
            }

            // Converte os limites do bounding box para coordenadas mundiais
            // Nota: No sistema Web Mercator, Y cresce para baixo
            var (bbMinWorldX, bbMinWorldY) = GeoToWorld(boundingBox.MinX, boundingBox.MinY, zoom);
            var (bbMaxWorldX, bbMaxWorldY) = GeoToWorld(boundingBox.MaxX, boundingBox.MaxY, zoom);

            // Como Y cresce para baixo no Web Mercator, MinY geográfico vira MaxY em pixels
            // e MaxY geográfico vira MinY em pixels
            double actualMinWorldY = Math.Min(bbMinWorldY, bbMaxWorldY);
            double actualMaxWorldY = Math.Max(bbMinWorldY, bbMaxWorldY);

            // Calcula as dimensões do bounding box em coordenadas mundiais
            double bbWorldWidth = bbMaxWorldX - bbMinWorldX;
            double bbWorldHeight = actualMaxWorldY - actualMinWorldY;

            // Calcula a escala que melhor se ajusta à imagem mantendo proporção
            double scaleX = imageWidth / bbWorldWidth;
            double scaleY = imageHeight / bbWorldHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Calcula os offsets para centralizar a imagem
            double offsetX = (imageWidth - bbWorldWidth * scale) / 2.0;
            double offsetY = (imageHeight - bbWorldHeight * scale) / 2.0;

            return new TransformParameters
            {
                BbMinWorldX = bbMinWorldX,
                BbMinWorldY = actualMinWorldY,
                BbMaxWorldX = bbMaxWorldX,
                BbMaxWorldY = actualMaxWorldY,
                BbWorldWidth = bbWorldWidth,
                BbWorldHeight = bbWorldHeight,
                Scale = scale,
                OffsetX = offsetX,
                OffsetY = offsetY
            };
        }

        /// <summary>
        /// Aplica uma transformação já calculada a uma coordenada mundial
        /// </summary>
        /// <param name="worldX">Coordenada X mundial</param>
        /// <param name="worldY">Coordenada Y mundial</param>
        /// <param name="transform">Parâmetros de transformação</param>
        /// <returns>Coordenadas de pixel (X, Y)</returns>
        public static (double X, double Y) WorldToPixel(double worldX, double worldY, TransformParameters transform)
        {
            double pixelX = (worldX - transform.BbMinWorldX) * transform.Scale + transform.OffsetX;
            double pixelY = (worldY - transform.BbMinWorldY) * transform.Scale + transform.OffsetY;
            return (pixelX, pixelY);
        }
    }

    /// <summary>
    /// Parâmetros de transformação para conversão de coordenadas mundiais para pixels
    /// </summary>
    public class TransformParameters
    {
        public double BbMinWorldX { get; set; }
        public double BbMinWorldY { get; set; }
        public double BbMaxWorldX { get; set; }
        public double BbMaxWorldY { get; set; }
        public double BbWorldWidth { get; set; }
        public double BbWorldHeight { get; set; }
        public double Scale { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
    }
}