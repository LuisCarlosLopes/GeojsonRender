using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Application.Configuration
{
    /// <summary>
    /// Classe responsável por analisar e converter arquivos de configuração JSON
    /// </summary>
    public class GeoJsonConfigParser
    {
        /// <summary>
        /// Carrega um arquivo de configuração JSON e converte para objetos de domínio
        /// </summary>
        /// <param name="configFilePath">Caminho do arquivo de configuração</param>
        /// <returns>Tupla com as configurações carregadas</returns>
        public async Task<(GeoFilter Filter, StyleConfig StyleConfig, RenderOptions RenderOptions)> LoadConfigAsync(
            string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath))
            {
                throw new ArgumentException("O caminho do arquivo de configuração é obrigatório.", nameof(configFilePath));
            }

            if (!File.Exists(configFilePath))
            {
                throw new FileNotFoundException("O arquivo de configuração não foi encontrado.", configFilePath);
            }

            string json;
#if NET48
            using (var reader = new StreamReader(configFilePath))
            {
                json = await reader.ReadToEndAsync();
            }
#else
            json = await File.ReadAllTextAsync(configFilePath);
#endif

            var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            // Lê os filtros
            var filter = ParseFilter(root);

            // Lê as configurações de estilo
            var styleConfig = ParseStyleConfig(root);

            // Lê as opções de renderização
            var renderOptions = ParseRenderOptions(root);

            return (filter, styleConfig, renderOptions);
        }

        /// <summary>
        /// Analisa a seção de filtros do JSON
        /// </summary>
        private GeoFilter ParseFilter(JsonElement root)
        {
            var filter = new GeoFilter();

            if (root.TryGetProperty("filters", out var filtersElement) && filtersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in filtersElement.EnumerateArray())
                {
                    if (item.TryGetProperty("property", out var propertyElement) &&
                        item.TryGetProperty("value", out var valueElement))
                    {
                        var condition = new FilterCondition
                        {
                            Property = propertyElement.GetString(),
                            Value = valueElement.ValueKind == JsonValueKind.String
                                ? valueElement.GetString()
                                : valueElement.ValueKind == JsonValueKind.Number
                                    ? valueElement.GetDouble()
                                    : valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False
                                        ? valueElement.GetBoolean()
                                        : null
                        };

                        filter.Conditions.Add(condition);
                    }
                }
            }

            return filter;
        }

        /// <summary>
        /// Analisa a seção de configuração de estilos do JSON
        /// </summary>
        private StyleConfig ParseStyleConfig(JsonElement root)
        {
            var styleConfig = new StyleConfig();

            // Estilo de destaque (só sobrescreve propriedades especificadas)
            if (root.TryGetProperty("highlightStyle", out var highlightStyleElement))
            {
                var parsed = ParseFeatureStyle(highlightStyleElement);
                if (!string.IsNullOrEmpty(parsed.FillColor))
                    styleConfig.HighlightStyle.FillColor = parsed.FillColor;
                if (!string.IsNullOrEmpty(parsed.StrokeColor))
                    styleConfig.HighlightStyle.StrokeColor = parsed.StrokeColor;
                styleConfig.HighlightStyle = ParseFeatureStyle(highlightStyleElement);
            }

            // Estilo padrão
            if (root.TryGetProperty("defaultStyle", out var defaultStyleElement))
            {
                styleConfig.DefaultStyle = ParseFeatureStyle(defaultStyleElement);
            }

            // Configuração de label
            if (root.TryGetProperty("labelProperty", out var labelPropertyElement) && 
                labelPropertyElement.ValueKind == JsonValueKind.String)
            {
                styleConfig.LabelConfig.PropertyName = labelPropertyElement.GetString();
                styleConfig.LabelConfig.Enabled = true;
            }

            if (root.TryGetProperty("labelConfig", out var labelConfigElement))
            {
                ParseLabelConfig(labelConfigElement, styleConfig.LabelConfig);
            }

            return styleConfig;
        }

        /// <summary>
        /// Analisa um objeto de estilo de feição do JSON
        /// </summary>
        private FeatureStyle ParseFeatureStyle(JsonElement element)
        {
            var style = new FeatureStyle();

            if (element.TryGetProperty("fillColor", out var fillColorElement) && 
                fillColorElement.ValueKind == JsonValueKind.String)
            {
                style.FillColor = fillColorElement.GetString();
            }

            if (element.TryGetProperty("strokeColor", out var strokeColorElement) && 
                strokeColorElement.ValueKind == JsonValueKind.String)
            {
                style.StrokeColor = strokeColorElement.GetString();
            }

            if (element.TryGetProperty("strokeWidth", out var strokeWidthElement) && 
                strokeWidthElement.ValueKind == JsonValueKind.Number)
            {
                style.StrokeWidth = (float)strokeWidthElement.GetDouble();
            }

            return style;
        }

        /// <summary>
        /// Analisa uma configuração de label do JSON
        /// </summary>
        private void ParseLabelConfig(JsonElement element, LabelConfig labelConfig)
        {
            if (element.TryGetProperty("enabled", out var enabledElement) && 
                enabledElement.ValueKind == JsonValueKind.False)
            {
                labelConfig.Enabled = false;
                return;
            }

            if (element.TryGetProperty("fontSize", out var fontSizeElement) && 
                fontSizeElement.ValueKind == JsonValueKind.Number)
            {
                labelConfig.FontSize = fontSizeElement.GetInt32();
            }

            if (element.TryGetProperty("fontColor", out var fontColorElement) && 
                fontColorElement.ValueKind == JsonValueKind.String)
            {
                labelConfig.FontColor = fontColorElement.GetString();
            }

            if (element.TryGetProperty("halo", out var haloElement) && 
                haloElement.ValueKind == JsonValueKind.True || haloElement.ValueKind == JsonValueKind.False)
            {
                labelConfig.Halo = haloElement.GetBoolean();
            }

            if (element.TryGetProperty("haloColor", out var haloColorElement) && 
                haloColorElement.ValueKind == JsonValueKind.String)
            {
                labelConfig.HaloColor = haloColorElement.GetString();
            }

            if (element.TryGetProperty("haloWidth", out var haloWidthElement) && 
                haloWidthElement.ValueKind == JsonValueKind.Number)
            {
                labelConfig.HaloWidth = (float)haloWidthElement.GetDouble();
            }
        }

        /// <summary>
        /// Analisa as opções de renderização do JSON
        /// </summary>
        private RenderOptions ParseRenderOptions(JsonElement root)
        {
            var options = new RenderOptions();

            if (root.TryGetProperty("renderOptions", out var renderOptionsElement))
            {
                if (renderOptionsElement.TryGetProperty("width", out var widthElement) && 
                    widthElement.ValueKind == JsonValueKind.Number)
                {
                    options.Width = widthElement.GetInt32();
                }

                if (renderOptionsElement.TryGetProperty("height", out var heightElement) && 
                    heightElement.ValueKind == JsonValueKind.Number)
                {
                    options.Height = heightElement.GetInt32();
                }

                if (renderOptionsElement.TryGetProperty("format", out var formatElement) && 
                    formatElement.ValueKind == JsonValueKind.String)
                {
                    var formatString = formatElement.GetString().ToLowerInvariant();
                    options.Format = formatString == "png" ? ImageFormat.Png : ImageFormat.Jpeg;
                }

                if (renderOptionsElement.TryGetProperty("quality", out var qualityElement) && 
                    qualityElement.ValueKind == JsonValueKind.Number)
                {
                    // Implementação do método Math.Clamp
                    int quality = qualityElement.GetInt32();
                    options.Quality = quality < 0 ? 0 : (quality > 100 ? 100 : quality);
                }

                if (renderOptionsElement.TryGetProperty("zoomLevel", out var zoomLevelElement) && 
                    zoomLevelElement.ValueKind == JsonValueKind.Number)
                {
                    options.ZoomLevel = zoomLevelElement.GetInt32();
                }

                if (renderOptionsElement.TryGetProperty("autoCenter", out var autoCenterElement) && 
                    (autoCenterElement.ValueKind == JsonValueKind.True || autoCenterElement.ValueKind == JsonValueKind.False))
                {
                    options.AutoCenter = autoCenterElement.GetBoolean();
                }

                if (renderOptionsElement.TryGetProperty("bufferPercentage", out var bufferElement) && 
                    bufferElement.ValueKind == JsonValueKind.Number)
                {
                    options.BufferPercentage = bufferElement.GetDouble();
                }

                if (renderOptionsElement.TryGetProperty("showMapBackground", out var showMapBgElement) && 
                    (showMapBgElement.ValueKind == JsonValueKind.True || showMapBgElement.ValueKind == JsonValueKind.False))
                {
                    options.ShowMapBackground = showMapBgElement.GetBoolean();
                }

                if (renderOptionsElement.TryGetProperty("tileServerUrl", out var tileServerElement) && 
                    tileServerElement.ValueKind == JsonValueKind.String)
                {
                    options.TileServerUrl = tileServerElement.GetString();
                }
            }

            if (root.TryGetProperty("inputFilePath", out var inputFileElement) && 
                inputFileElement.ValueKind == JsonValueKind.String)
            {
                options.InputFilePath = inputFileElement.GetString();
            }

            if (root.TryGetProperty("outputFilePath", out var outputFileElement) && 
                outputFileElement.ValueKind == JsonValueKind.String)
            {
                options.OutputFilePath = outputFileElement.GetString();
            }

            return options;
        }
    }
} 