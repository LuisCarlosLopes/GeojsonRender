using System;
using System.IO;
using System.Threading.Tasks;
using GeoJsonRenderer.Application.Configuration;
using GeoJsonRenderer.Application.Services;
using GeoJsonRenderer.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace GeoJsonRenderer.ConsoleApp
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("== GeoJSON Renderer ==");
                Console.WriteLine("Iniciando...");

                // Verificar argumentos
                if (args.Length == 0)
                {
                    Console.WriteLine("Uso: GeoJsonRenderer.ConsoleApp <caminho-arquivo-configuracao>");
                    Console.WriteLine("Exemplo: GeoJsonRenderer.ConsoleApp config.json");
                    return 1;
                }

                string configPath = args[0];
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Erro: O arquivo de configuração '{configPath}' não foi encontrado.");
                    return 1;
                }

                Console.WriteLine($"Utilizando arquivo de configuração: {configPath}");

                // Configurar DI
                var services = ConfigureServices();
                await using var serviceProvider = services.BuildServiceProvider();

                // Executar o processo
                var processingResult = await ExecuteProcessingAsync(serviceProvider, configPath);
                Console.WriteLine($"Processamento concluído. Imagem gerada em: {processingResult}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static ServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            // Adiciona serviços da infraestrutura
            services.AddGeoJsonRendererInfrastructure();

            // Adiciona serviços da aplicação
            services.AddScoped<GeoJsonService>();
            services.AddTransient<GeoJsonConfigParser>();

            return services;
        }

        private static async Task<string> ExecuteProcessingAsync(ServiceProvider serviceProvider, string configPath)
        {
            // Obter o parser de configuração
            var configParser = serviceProvider.GetRequiredService<GeoJsonConfigParser>();

            // Carregar configurações
            Console.WriteLine("Carregando configurações...");
            var (filter, styleConfig, renderOptions) = await configParser.LoadConfigAsync(configPath);

            // Validar configurações
            Console.WriteLine("Validando configurações...");
            if (string.IsNullOrEmpty(renderOptions.InputFilePath))
            {
                throw new InvalidOperationException("O caminho do arquivo GeoJSON de entrada não foi especificado na configuração.");
            }

            if (string.IsNullOrEmpty(renderOptions.OutputFilePath))
            {
                renderOptions.OutputFilePath = Path.Combine(
                    Path.GetDirectoryName(renderOptions.InputFilePath),
                    Path.GetFileNameWithoutExtension(renderOptions.InputFilePath) + ".jpg");
                Console.WriteLine($"Caminho de saída não especificado. Utilizando: {renderOptions.OutputFilePath}");
            }

            // Processar GeoJSON e renderizar mapa
            Console.WriteLine($"Processando arquivo GeoJSON: {renderOptions.InputFilePath}");
            var geoJsonService = serviceProvider.GetRequiredService<GeoJsonService>();
            string outputPath = await geoJsonService.ProcessAndRenderAsync(renderOptions, filter, styleConfig);

            return outputPath;
        }
    }
}
