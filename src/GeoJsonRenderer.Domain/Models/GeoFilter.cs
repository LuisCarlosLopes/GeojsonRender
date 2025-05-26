using System.Collections.Generic;

namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Tipos de operadores de filtro
    /// </summary>
    public enum FilterOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith
    }

    /// <summary>
    /// Representa um filtro para seleção de feições geográficas
    /// </summary>
    public class GeoFilter
    {
        /// <summary>
        /// Lista de condições que devem ser satisfeitas (operação AND)
        /// </summary>
        public List<FilterCondition> Conditions { get; set; } = new();

        /// <summary>
        /// Verifica se uma feição satisfaz o filtro
        /// </summary>
        /// <param name="feature">Feição a ser verificada</param>
        /// <returns>True se a feição satisfaz todas as condições do filtro, False caso contrário</returns>
        public bool Match(GeoFeature feature)
        {
            if (feature?.Properties == null)
                return false;

            foreach (var condition in Conditions)
            {
                var propertyValue = feature.GetPropertyAsString(condition.Property);

                if (!condition.Match(feature))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Representa uma condição de filtro para feições geográficas
    /// </summary>
    public class FilterCondition
    {
        /// <summary>
        /// Nome da propriedade a ser filtrada
        /// </summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>
        /// Valor esperado para a propriedade
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de operação de comparação
        /// </summary>
        public FilterOperator Operator { get; set; } = FilterOperator.Equals;

        /// <summary>
        /// Verifica se uma feição atende a esta condição específica
        /// </summary>
        /// <param name="feature">Feição a ser verificada</param>
        /// <returns>True se a feição atende à condição</returns>
        public bool Match(GeoFeature feature)
        {
            var propertyValue = feature.GetPropertyAsString(Property);
            return EvaluateCondition(propertyValue, Value, Operator);
        }

        /// <summary>
        /// Avalia uma condição específica
        /// </summary>
        /// <param name="propertyValue">Valor da propriedade</param>
        /// <param name="expectedValue">Valor esperado</param>
        /// <param name="operator">Operador de comparação</param>
        /// <returns>True se a condição é atendida</returns>
        private static bool EvaluateCondition(string? propertyValue, string expectedValue, FilterOperator @operator)
        {
            if (propertyValue == null)
                return false;

            return @operator switch
            {
                FilterOperator.Equals => string.Equals(propertyValue, expectedValue, System.StringComparison.OrdinalIgnoreCase),
                FilterOperator.NotEquals => !string.Equals(propertyValue, expectedValue, System.StringComparison.OrdinalIgnoreCase),
                FilterOperator.Contains => propertyValue.Contains(expectedValue, System.StringComparison.OrdinalIgnoreCase),
                FilterOperator.StartsWith => propertyValue.StartsWith(expectedValue, System.StringComparison.OrdinalIgnoreCase),
                FilterOperator.EndsWith => propertyValue.EndsWith(expectedValue, System.StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}