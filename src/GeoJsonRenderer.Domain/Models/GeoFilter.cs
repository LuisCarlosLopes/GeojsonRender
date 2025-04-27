using System.Collections.Generic;

namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Representa um filtro para seleção de feições geográficas
    /// </summary>
    public class GeoFilter
    {
        /// <summary>
        /// Lista de condições que devem ser satisfeitas (operação AND)
        /// </summary>
        public List<FilterCondition> Conditions { get; set; } = new List<FilterCondition>();

        /// <summary>
        /// Verifica se uma feição satisfaz o filtro
        /// </summary>
        /// <param name="feature">Feição a ser verificada</param>
        /// <returns>True se a feição satisfaz todas as condições do filtro, False caso contrário</returns>
        public bool Match(GeoFeature feature)
        {
            // Se não há condições, todas as feições são consideradas
            if (Conditions == null || Conditions.Count == 0)
            {
                return true;
            }

            // AND semantics: todas as condições devem ser satisfeitas
            foreach (var condition in Conditions)
            {
                if (!condition.Match(feature))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Representa uma condição de filtro baseada em uma propriedade
    /// </summary>
    public class FilterCondition
    {
        /// <summary>
        /// Nome da propriedade a ser verificada
        /// </summary>
        public string Property { get; set; }

        /// <summary>
        /// Valor esperado para a propriedade
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Verifica se uma feição satisfaz a condição
        /// </summary>
        /// <param name="feature">Feição a ser verificada</param>
        /// <returns>True se a feição satisfaz a condição, False caso contrário</returns>
        public bool Match(GeoFeature feature)
        {
            if (string.IsNullOrEmpty(Property) || feature == null)
            {
                return false;
            }

            var propertyValue = feature.GetPropertyValue(Property);
            
            if (propertyValue == null)
            {
                return false;
            }

            // Comparação simples baseada em strings
            return propertyValue.ToString().Equals(Value?.ToString(), 
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
} 