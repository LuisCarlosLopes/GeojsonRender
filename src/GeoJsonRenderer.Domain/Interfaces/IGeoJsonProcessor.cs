using System.Collections.Generic;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Domain.Interfaces
{
    /// <summary>
    /// Interface para processamento de arquivos GeoJSON
    /// </summary>
    public interface IGeoJsonProcessor
    {
        /// <summary>
        /// Carrega feições geográficas de um arquivo GeoJSON
        /// </summary>
        /// <param name="filePath">Caminho do arquivo GeoJSON</param>
        /// <returns>Lista de feições geográficas</returns>
        Task<List<GeoFeature>> LoadFeaturesAsync(string filePath);

        /// <summary>
        /// Filtra as feições de acordo com os critérios especificados
        /// </summary>
        /// <param name="features">Lista de feições a serem filtradas</param>
        /// <param name="filter">Critérios de filtragem</param>
        /// <returns>Lista de feições com a propriedade IsFiltered atualizada</returns>
        List<GeoFeature> ApplyFilter(List<GeoFeature> features, GeoFilter filter);

        /// <summary>
        /// Calcula o bounding box para as feições filtradas
        /// </summary>
        /// <param name="features">Lista de feições</param>
        /// <returns>Bounding box das feições filtradas</returns>
        BoundingBox CalculateBoundingBox(List<GeoFeature> features);
    }
} 