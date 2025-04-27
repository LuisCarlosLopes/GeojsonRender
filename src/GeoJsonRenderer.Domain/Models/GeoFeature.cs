using System.Collections.Generic;

namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Representa uma feição geográfica do GeoJSON
    /// </summary>
    public class GeoFeature
    {
        /// <summary>
        /// Identificador único da feição
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Geometria da feição em formato GeoJSON
        /// </summary>
        public object Geometry { get; set; }

        /// <summary>
        /// Propriedades associadas à feição
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Indica se a feição foi filtrada (atende aos critérios de filtro)
        /// </summary>
        public bool IsFiltered { get; set; }

        /// <summary>
        /// Obtém o valor de uma propriedade específica da feição
        /// </summary>
        /// <param name="propertyName">Nome da propriedade</param>
        /// <returns>Valor da propriedade ou null se não existir</returns>
        public object GetPropertyValue(string propertyName)
        {
            if (Properties != null && Properties.TryGetValue(propertyName, out var value))
            {
                return value;
            }
            
            return null;
        }

        /// <summary>
        /// Obtém o valor de uma propriedade específica da feição como string
        /// </summary>
        /// <param name="propertyName">Nome da propriedade</param>
        /// <returns>Valor da propriedade como string ou string vazia se não existir</returns>
        public string GetPropertyValueAsString(string propertyName)
        {
            var value = GetPropertyValue(propertyName);
            return value?.ToString() ?? string.Empty;
        }
    }
} 