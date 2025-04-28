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
                Console.WriteLine("Starting...");

                // Verificar argumentos
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: GeoJsonRenderer.ConsoleApp <configuration-file-path>");
                    Console.WriteLine("Example: GeoJsonRenderer.ConsoleApp config.json");
                    return 1;
                }

                string configPath = args[0];
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Error: The configuration file '{configPath}' was not found.");
                    return 1;
                }

                Console.WriteLine($"Using configuration file: {configPath}");

                // Configurar DI
                var services = ConfigureServices();
                await using var serviceProvider = services.BuildServiceProvider();

                // Executar o processo
                var processingResult = await ExecuteProcessingAsync(serviceProvider, configPath);
                Console.WriteLine($"Processing completed. Image generated at: {processingResult}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }


        private static ServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();
        
            // Adiciona logging (necessário para ILogger<T>)
            services.AddLogging();
        
            // Adiciona serviços da infraestrutura
            services.AddGeoJsonRendererInfrastructure();
        
            // Adiciona serviços da aplicação
            services.AddScoped<GeoJsonService>();
            services.AddTransient<GeoJsonConfigParser>();
        
            return services;
        }
        // ...existing code...

        private static async Task<string> ExecuteProcessingAsync(ServiceProvider serviceProvider, string configPath)
        {
            // Obter o parser de configuração
            var configParser = serviceProvider.GetRequiredService<GeoJsonConfigParser>();

            // Carregar configurações
            Console.WriteLine("Loading configurations...");
            var (filter, styleConfig, renderOptions) = await configParser.LoadConfigAsync(configPath);

            // Validar configurações
            Console.WriteLine("Validating configurations...");
            if (string.IsNullOrEmpty(renderOptions.InputFilePath))
            {
                throw new InvalidOperationException("The input GeoJSON file path was not specified in the configuration.");
            }

            if (string.IsNullOrEmpty(renderOptions.OutputFilePath))
            {
                renderOptions.OutputFilePath = Path.Combine(
                    Path.GetDirectoryName(renderOptions.InputFilePath),
                    Path.GetFileNameWithoutExtension(renderOptions.InputFilePath) + ".jpg");
                Console.WriteLine($"Output path not specified. Using: {renderOptions.OutputFilePath}");
            }

            // Processar GeoJSON e renderizar mapa
            Console.WriteLine($"Processing GeoJSON file: {renderOptions.InputFilePath}");
            var geoJsonService = serviceProvider.GetRequiredService<GeoJsonService>();
            string outputPath = await geoJsonService.ProcessAndRenderAsync(renderOptions, filter, styleConfig);

            return outputPath;
        }
    }
}
