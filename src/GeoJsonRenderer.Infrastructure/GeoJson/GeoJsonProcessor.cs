using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Interfaces;
using GeoJsonRenderer.Domain.Models;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace GeoJsonRenderer.Infrastructure.GeoJson
{
    /// <summary>
    /// Implementação do processador de GeoJSON usando NetTopologySuite
    /// </summary>
    public class GeoJsonProcessor : IGeoJsonProcessor
    {
        /// <summary>
        /// Carrega feições geográficas de um arquivo GeoJSON
        /// </summary>
        /// <param name="filePath">Caminho do arquivo GeoJSON</param>
        /// <returns>Lista de feições geográficas</returns>
        public async Task<List<GeoFeature>> LoadFeaturesAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("The GeoJSON file path cannot be null or empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The GeoJSON file was not found.", filePath);
            }

            string geojsonContent;
#if NET48
            using (var reader = new StreamReader(filePath))
            {
                geojsonContent = await reader.ReadToEndAsync();
            }
#else
            geojsonContent = await File.ReadAllTextAsync(filePath);
#endif

            // Configurando o leitor de GeoJSON
            var serializer = GeoJsonSerializer.Create();

            // Lendo a coleção de feições
            using (var stringReader = new StringReader(geojsonContent))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                FeatureCollection featureCollection = serializer.Deserialize<FeatureCollection>(jsonReader);
                return ConvertToGeoFeatures(featureCollection);
            }
        }

        /// <summary>
        /// Filtra as feições de acordo com os critérios especificados
        /// </summary>
        /// <param name="features">Lista de feições a serem filtradas</param>
        /// <param name="filter">Critérios de filtragem</param>
        /// <returns>Lista de feições com a propriedade IsFiltered atualizada</returns>
        public List<GeoFeature> ApplyFilter(List<GeoFeature> features, GeoFilter filter)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            // Se não há condições, todas as feições são consideradas filtradas
            if (filter.Conditions.Count == 0)
            {
                foreach (var feature in features)
                {
                    feature.IsFiltered = true;
                }
                return features;
            }

            // Aplica o filtro a cada feição
            foreach (var feature in features)
            {
                feature.IsFiltered = filter.Match(feature);
            }

            return features;
        }

        /// <summary>
        /// Calcula o bounding box para as feições filtradas
        /// </summary>
        /// <param name="features">Lista de feições</param>
        /// <returns>Bounding box das feições filtradas</returns>
        public BoundingBox CalculateBoundingBox(List<GeoFeature> features)
        {
            if (features == null || features.Count == 0)
            {
                throw new ArgumentException("The feature list cannot be null or empty.", nameof(features));
            }

            // Obtém apenas as feições filtradas
            var filteredFeatures = features.Where(f => f.IsFiltered).ToList();

            // IMPORTANTE: Só usamos todas as feições se não houver nenhuma filtrada
            // Isso garante que o mapa será centralizado nas feições de interesse
            if (filteredFeatures.Count == 0)
            {
                Console.WriteLine("No filtered features found. Using all features for bounding box calculation.");
                filteredFeatures = features;
            }
            else
            {
                Console.WriteLine($"Using {filteredFeatures.Count} filtered features for bounding box calculation.");
            }

            var boundingBox = new BoundingBox();

            foreach (var feature in filteredFeatures)
            {
                if (feature.Geometry is System.Text.Json.JsonElement jsonElement)
                {
                    // Tenta extrair as coordenadas da propriedade de geometria como um objeto JSON
                    ExtractBoundingBoxFromJsonGeometry(jsonElement, boundingBox);
                }
                else if (feature.Geometry is Geometry geometry)
                {
                    // Extrai as coordenadas diretamente do objeto Geometry do NetTopologySuite
                    var envelope = geometry.EnvelopeInternal;
                    boundingBox.Expand(envelope.MinX, envelope.MinY);
                    boundingBox.Expand(envelope.MaxX, envelope.MaxY);
                }
            }

            // Verifica se o bounding box é válido
            if (!boundingBox.IsValid())
            {
                Console.WriteLine("WARNING: Calculated bounding box is not valid. Using global bounding box.");
                // Caso o bounding box não seja válido, cria um padrão centrado em (0, 0)
                return new BoundingBox(-180, -90, 180, 90);
            }

            // Log detalhado do bounding box
            Console.WriteLine($"BoundingBox calculated for features {(filteredFeatures == features ? "ALL" : "FILTERED")}:\nMinX={boundingBox.MinX:F6}, MinY={boundingBox.MinY:F6}, MaxX={boundingBox.MaxX:F6}, MaxY={boundingBox.MaxY:F6}\nWidth={boundingBox.Width:F6}, Height={boundingBox.Height:F6}");

            return boundingBox;
        }

        /// <summary>
        /// Extrai um bounding box de um elemento JSON que representa uma geometria GeoJSON
        /// </summary>
        private void ExtractBoundingBoxFromJsonGeometry(System.Text.Json.JsonElement jsonElement, BoundingBox boundingBox)
        {
            if (jsonElement.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                string? geometryType = typeElement.GetString();

                switch (geometryType)
                {
                    case "Point":
                        if (jsonElement.TryGetProperty("coordinates", out var pointCoords) &&
                            pointCoords.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            double x = pointCoords[0].GetDouble();
                            double y = pointCoords[1].GetDouble();
                            boundingBox.Expand(x, y);
                        }
                        break;

                    case "LineString":
                    case "MultiPoint":
                        if (jsonElement.TryGetProperty("coordinates", out var lineCoords) &&
                            lineCoords.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var point in lineCoords.EnumerateArray())
                            {
                                double x = point[0].GetDouble();
                                double y = point[1].GetDouble();
                                boundingBox.Expand(x, y);
                            }
                        }
                        break;

                    case "Polygon":
                    case "MultiLineString":
                        if (jsonElement.TryGetProperty("coordinates", out var polyCoords) &&
                            polyCoords.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var ring in polyCoords.EnumerateArray())
                            {
                                foreach (var point in ring.EnumerateArray())
                                {
                                    double x = point[0].GetDouble();
                                    double y = point[1].GetDouble();
                                    boundingBox.Expand(x, y);
                                }
                            }
                        }
                        break;

                    case "MultiPolygon":
                        if (jsonElement.TryGetProperty("coordinates", out var multiPolyCoords) &&
                            multiPolyCoords.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var polygon in multiPolyCoords.EnumerateArray())
                            {
                                foreach (var ring in polygon.EnumerateArray())
                                {
                                    foreach (var point in ring.EnumerateArray())
                                    {
                                        double x = point[0].GetDouble();
                                        double y = point[1].GetDouble();
                                        boundingBox.Expand(x, y);
                                    }
                                }
                            }
                        }
                        break;

                    case "GeometryCollection":
                        if (jsonElement.TryGetProperty("geometries", out var geometries) &&
                            geometries.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var geometry in geometries.EnumerateArray())
                            {
                                ExtractBoundingBoxFromJsonGeometry(geometry, boundingBox);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Converte uma coleção de feições do NetTopologySuite para objetos de domínio
        /// </summary>
        private List<GeoFeature> ConvertToGeoFeatures(FeatureCollection featureCollection)
        {
            if (featureCollection == null)
            {
                return new List<GeoFeature>();
            }

            var geoFeatures = new List<GeoFeature>();

            foreach (var feature in featureCollection)
            {
                var geoFeature = new GeoFeature
                {
                    Id = feature.Attributes.GetOptionalValue("id")?.ToString() ?? Guid.NewGuid().ToString(),
                    Geometry = feature.Geometry,
                    Properties = new Dictionary<string, object>()
                };

                // Converter atributos para o dicionário de propriedades
                foreach (var key in feature.Attributes.GetNames())
                {
                    var value = feature.Attributes[key];
                    geoFeature.Properties[key] = value;
                }

                geoFeatures.Add(geoFeature);
            }

            return geoFeatures;
        }
    }
}