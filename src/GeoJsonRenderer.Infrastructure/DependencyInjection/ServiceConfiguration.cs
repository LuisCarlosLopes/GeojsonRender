using GeoJsonRenderer.Domain.Interfaces;
using GeoJsonRenderer.Infrastructure.GeoJson;
using GeoJsonRenderer.Infrastructure.Mapping;
using GeoJsonRenderer.Infrastructure.Rendering;
using Microsoft.Extensions.DependencyInjection;
using InfraITileProvider = GeoJsonRenderer.Infrastructure.Interfaces.ITileProvider;
using DomainITileProvider = GeoJsonRenderer.Domain.Interfaces.ITileProvider;

namespace GeoJsonRenderer.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Configuração de serviços para injeção de dependência
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Adiciona os serviços da infraestrutura ao contêiner de injeção de dependência
        /// </summary>
        /// <param name="services">Coleção de serviços</param>
        /// <returns>Coleção de serviços com os serviços adicionados</returns>
        public static IServiceCollection AddGeoJsonRendererInfrastructure(this IServiceCollection services)
        {
            // Registrando o processador de GeoJSON
            services.AddScoped<IGeoJsonProcessor, GeoJsonProcessor>();

            // Registrando o provedor de tiles
            services.AddScoped<InfraITileProvider, TileProvider>();
            services.AddScoped<DomainITileProvider, ITileProviderAdapter>();

            // Registrando o renderizador de mapas
            services.AddScoped<IMapRenderer, SkiaMapRenderer>();

            return services;
        }
    }
}