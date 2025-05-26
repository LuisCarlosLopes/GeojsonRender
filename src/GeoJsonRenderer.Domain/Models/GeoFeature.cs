using System.Collections.Generic;

namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Representa uma feição geográfica com geometria e propriedades
    /// </summary>
    public class GeoFeature
    {
        /// <summary>
        /// Identificador único da feição
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Geometria da feição (pode ser Point, LineString, Polygon, etc.)
        /// </summary>
        public object? Geometry { get; set; }

        /// <summary>
        /// Propriedades adicionais da feição
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// Indica se a feição passou pelo filtro aplicado
        /// </summary>
        public bool IsFiltered { get; set; } = false;

        /// <summary>
        /// Obtém o valor de uma propriedade específica
        /// </summary>
        /// <param name="propertyName">Nome da propriedade</param>
        /// <returns>Valor da propriedade ou null se não existir</returns>
        public object? GetProperty(string propertyName)
        {
            return Properties.TryGetValue(propertyName, out var value) ? value : null;
        }

        /// <summary>
        /// Obtém o valor de uma propriedade como string
        /// </summary>
        /// <param name="propertyName">Nome da propriedade</param>
        /// <returns>Valor da propriedade como string ou null se não existir</returns>
        public string? GetPropertyAsString(string propertyName)
        {
            var value = GetProperty(propertyName);
            return value?.ToString();
        }

        /// <summary>
        /// Define o valor de uma propriedade
        /// </summary>
        /// <param name="propertyName">Nome da propriedade</param>
        /// <param name="value">Valor da propriedade</param>
        public void SetProperty(string propertyName, object value)
        {
            Properties[propertyName] = value;
        }
    }
}