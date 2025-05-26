using System.Collections.Generic;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Domain.Interfaces
{
    /// <summary>
    /// Interface para renderização de mapas
    /// </summary>
    public interface IMapRenderer
    {
        /// <summary>
        /// Renderiza um mapa com as feições geográficas e salva como imagem
        /// </summary>
        /// <param name="features">Lista de feições a serem renderizadas</param>
        /// <param name="boundingBox">Bounding box para centralização do mapa</param>
        /// <param name="styleConfig">Configuração de estilos para renderização</param>
        /// <param name="options">Opções de renderização</param>
        /// <returns>Caminho do arquivo de imagem gerado</returns>
        Task<string> RenderMapAsync(
            List<GeoFeature> features,
            BoundingBox boundingBox,
            StyleConfig styleConfig,
            RenderOptions options);
    }
}