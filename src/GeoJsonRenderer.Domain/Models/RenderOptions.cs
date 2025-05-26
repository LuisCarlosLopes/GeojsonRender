namespace GeoJsonRenderer.Domain.Models
{
    /// <summary>
    /// Opções de configuração para a renderização do mapa
    /// </summary>
    public class RenderOptions
    {
        /// <summary>
        /// Largura da imagem em pixels
        /// </summary>
        public int Width { get; set; } = 1920;

        /// <summary>
        /// Altura da imagem em pixels
        /// </summary>
        public int Height { get; set; } = 1080;

        /// <summary>
        /// Formato de saída da imagem
        /// </summary>
        public ImageFormat Format { get; set; } = ImageFormat.Jpeg;

        /// <summary>
        /// Qualidade da imagem (para formatos com compressão como JPEG)
        /// </summary>
        public int Quality { get; set; } = 90;

        /// <summary>
        /// Nível de zoom do mapa
        /// </summary>
        public int? ZoomLevel { get; set; }

        /// <summary>
        /// Determina se deve centralizar automaticamente no bounding box das feições filtradas
        /// </summary>
        public bool AutoCenter { get; set; } = true;

        /// <summary>
        /// Porcentagem de buffer a ser aplicada ao bounding box para centralização
        /// </summary>
        public double BufferPercentage { get; set; } = 0.1;

        /// <summary>
        /// Caminho do arquivo GeoJSON de entrada
        /// </summary>
        public string InputFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Caminho do arquivo de saída da imagem
        /// </summary>
        public string OutputFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Indica se deve mostrar o fundo do mapa (tiles)
        /// </summary>
        public bool ShowMapBackground { get; set; } = true;

        /// <summary>
        /// URL do servidor de tiles (se não for especificado, usa OpenStreetMap)
        /// </summary>
        public string TileServerUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    }

    /// <summary>
    /// Formatos de imagem suportados
    /// </summary>
    public enum ImageFormat
    {
        /// <summary>
        /// Formato JPEG (com compressão com perdas)
        /// </summary>
        Jpeg,

        /// <summary>
        /// Formato PNG (sem perdas, com transparência)
        /// </summary>
        Png
    }
}