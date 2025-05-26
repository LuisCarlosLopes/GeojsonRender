using System;
using System.Drawing;

namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Configuração geral de estilos para a renderização
    /// </summary>
    public class StyleConfig
    {
        /// <summary>
        /// Estilo para feições filtradas (que atendem aos critérios)
        /// </summary>
        public FeatureStyle HighlightStyle { get; set; } = new FeatureStyle
        {
            FillColor = "#FF0000", // Vermelho
            StrokeColor = "#000000", // Preto
            StrokeWidth = 2
        };

        /// <summary>
        /// Estilo para feições não filtradas (padrão)
        /// </summary>
        public FeatureStyle DefaultStyle { get; set; } = new FeatureStyle
        {
            FillColor = "#FFFFFF33", // Branco com 20% de opacidade (33 em hexadecimal)
            StrokeColor = "#888888", // Cinza médio
            StrokeWidth = 0.8f
        };

        /// <summary>
        /// Configuração para renderização de rótulos nas feições
        /// </summary>
        public LabelConfig LabelConfig { get; set; } = new LabelConfig
        {
            Enabled = true,
            PropertyName = "nome_local",
            FontSize = 12,
            FontColor = "#000000", // Preto
            Halo = true,
            HaloColor = "#FFFFFF", // Branco
            HaloWidth = 2
        };
    }

    /// <summary>
    /// Estilo visual para uma feição
    /// </summary>
    public class FeatureStyle
    {
        /// <summary>
        /// Cor de preenchimento (formato: #RRGGBB ou #RRGGBBAA)
        /// </summary>
        public string FillColor { get; set; } = "#0000FF80"; // Azul semi-transparente

        /// <summary>
        /// Cor do contorno (formato: #RRGGBB ou #RRGGBBAA)
        /// </summary>
        public string StrokeColor { get; set; } = "#000000"; // Preto

        /// <summary>
        /// Largura do contorno em pixels
        /// </summary>
        public float StrokeWidth { get; set; } = 1.0f;

        /// <summary>
        /// Converte a string hexadecimal para um objeto Color
        /// </summary>
        /// <param name="hexColor">Cor em formato hexadecimal</param>
        /// <returns>Objeto Color ou null se a entrada for inválida</returns>
        public static System.Drawing.Color? ParseColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor))
            {
                return null;
            }

            try
            {
                // Remove o caractere # se presente
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }

                int a = 255, r, g, b;

                // Formatos suportados: RGB, ARGB, RRGGBB, RRGGBBAA, AARRGGBB
                switch (hexColor.Length)
                {
                    case 3: // RGB
                        r = Convert.ToInt32(new string(hexColor[0], 2), 16);
                        g = Convert.ToInt32(new string(hexColor[1], 2), 16);
                        b = Convert.ToInt32(new string(hexColor[2], 2), 16);
                        break;
                    case 4: // ARGB
                        a = Convert.ToInt32(new string(hexColor[0], 2), 16);
                        r = Convert.ToInt32(new string(hexColor[1], 2), 16);
                        g = Convert.ToInt32(new string(hexColor[2], 2), 16);
                        b = Convert.ToInt32(new string(hexColor[3], 2), 16);
                        break;
                    case 6: // RRGGBB
                        r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                        g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                        b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                        break;
                    case 8: // RRGGBBAA ou AARRGGBB
                        // Verificar se termina com AA (RRGGBBAA) ou começa com AA (AARRGGBB)
                        // Alguns frameworks usam o formato AARRGGBB, outros usam RRGGBBAA
                        // Assumindo que valores muito baixos no início são provavelmente alfa
                        int possibleAlpha = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                        if (possibleAlpha < 50) // Provavelmente AARRGGBB
                        {
                            a = possibleAlpha;
                            r = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                            g = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                            b = Convert.ToInt32(hexColor.Substring(6, 2), 16);
                        }
                        else // Provavelmente RRGGBBAA
                        {
                            r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                            g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                            b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                            a = Convert.ToInt32(hexColor.Substring(6, 2), 16);
                        }
                        break;
                    default:
                        Console.WriteLine($"Formato de cor não suportado: {hexColor}");
                        return null;
                }

                return Color.FromArgb(a, r, g, b);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao analisar cor hexadecimal: {hexColor}, erro: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Configuração para renderização de rótulos
    /// </summary>
    public class LabelConfig
    {
        /// <summary>
        /// Indica se os rótulos devem ser exibidos
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Nome da propriedade a ser usada como rótulo
        /// </summary>
        public string PropertyName { get; set; } = "name";

        /// <summary>
        /// Tamanho da fonte em pixels
        /// </summary>
        public float FontSize { get; set; } = 12.0f;

        /// <summary>
        /// Cor da fonte (formato: #RRGGBB ou #RRGGBBAA)
        /// </summary>
        public string FontColor { get; set; } = "#000000"; // Preto

        /// <summary>
        /// Largura do halo ao redor do texto
        /// </summary>
        public float HaloWidth { get; set; } = 1.0f;

        /// <summary>
        /// Indica se deve ser desenhado um halo (contorno) ao redor do texto
        /// </summary>
        public bool Halo { get; set; } = false;

        /// <summary>
        /// Cor do halo (formato: #RRGGBB ou #RRGGBBAA)
        /// </summary>
        public string HaloColor { get; set; } = "#FFFFFF"; // Branco
    }
}