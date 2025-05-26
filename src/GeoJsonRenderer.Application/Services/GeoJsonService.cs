using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Interfaces;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Application.Services
{
    /// <summary>
    /// Serviço principal que orquestra o processamento de GeoJSON e renderização de mapas
    /// </summary>
    public class GeoJsonService
    {
        private readonly IGeoJsonProcessor _geoJsonProcessor;
        private readonly IMapRenderer _mapRenderer;

        /// <summary>
        /// Construtor que recebe as dependências via injeção de dependência
        /// </summary>
        /// <param name="geoJsonProcessor">Processador de GeoJSON</param>
        /// <param name="mapRenderer">Renderizador de mapas</param>
        public GeoJsonService(IGeoJsonProcessor geoJsonProcessor, IMapRenderer mapRenderer)
        {
            _geoJsonProcessor = geoJsonProcessor ?? throw new ArgumentNullException(nameof(geoJsonProcessor));
            _mapRenderer = mapRenderer ?? throw new ArgumentNullException(nameof(mapRenderer));
        }

        /// <summary>
        /// Processa um arquivo GeoJSON, aplica filtros e renderiza um mapa
        /// </summary>
        /// <param name="options">Opções de renderização</param>
        /// <param name="filter">Filtro a ser aplicado</param>
        /// <param name="styleConfig">Configuração de estilos</param>
        /// <returns>Caminho do arquivo de imagem gerado</returns>
        public async Task<string> ProcessAndRenderAsync(
            RenderOptions options,
            GeoFilter filter,
            StyleConfig styleConfig)
        {
            ValidateInputs(options, filter, styleConfig);

            // Carrega as feições do arquivo GeoJSON
            var features = await _geoJsonProcessor.LoadFeaturesAsync(options.InputFilePath);

            // Aplica o filtro às feições
            features = _geoJsonProcessor.ApplyFilter(features, filter);

            // Calcula o bounding box das feições filtradas
            var boundingBox = _geoJsonProcessor.CalculateBoundingBox(features);

            // Aplica um buffer ao bounding box para melhor visualização
            if (options.BufferPercentage > 0)
            {
                boundingBox = boundingBox.Buffer(options.BufferPercentage);
            }

            // Renderiza o mapa e salva a imagem
            var outputPath = await _mapRenderer.RenderMapAsync(
                features,
                boundingBox,
                styleConfig,
                options);

            return outputPath;
        }

        /// <summary>
        /// Valida os parâmetros de entrada
        /// </summary>
        private void ValidateInputs(RenderOptions options, GeoFilter filter, StyleConfig styleConfig)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.InputFilePath))
            {
                throw new ArgumentException("O caminho do arquivo GeoJSON de entrada é obrigatório.", nameof(options.InputFilePath));
            }

            if (string.IsNullOrEmpty(options.OutputFilePath))
            {
                throw new ArgumentException("O caminho do arquivo de saída é obrigatório.", nameof(options.OutputFilePath));
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            if (styleConfig == null)
            {
                throw new ArgumentNullException(nameof(styleConfig));
            }
        }
    }
}