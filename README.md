# Geojson Render

## 🗂️ Sumário
- [Sobre o Projeto](#sobre-o-projeto)
- [Principais Funcionalidades](#principais-funcionalidades)
- [Arquitetura](#arquitetura)
- [Como Usar](#como-usar)
- [Integração em Projetos .NET](#integração-em-projetos-net)
- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Documentação Adicional](#documentação-adicional)
- [Testes](#testes)
- [Licença](#licença)


## 📋 Sobre o Projeto

Geojson Render é uma biblioteca .NET especializada em renderização de arquivos GeoJSON, oferecendo recursos avançados de filtragem, estilização e exportação de mapas. Ideal para aplicações que necessitam de visualização e processamento de dados geoespaciais.

## 🚀 Principais Funcionalidades

- ✨ Renderização de arquivos GeoJSON com alta qualidade
- 🎨 Estilos personalizáveis para feições geográficas
- 🔍 Sistema de filtros dinâmicos com operações OR (destaca feições que atendem a qualquer condição)
- 🔍 Limitação de filtros: O filtro só suporta igualdade simples de propriedades (não há operadores como >, <, contém, etc).
- 🏷️ Suporte a rótulos (labels) configuráveis
- 🎯 Centralização automática baseada em feições filtradas
- 🗺️ Integração com tiles de OpenStreetMap, cobrindo 100% da área via projeção Web Mercator
- 📸 Exportação para JPEG/PNG em alta resolução

## 🏗️ Arquitetura

O projeto segue os princípios de Clean Architecture, organizado em camadas:

```
GeoJsonRenderer/
├── src/
│   ├── GeoJsonRenderer.Domain/        # Entidades e regras de negócio
│   ├── GeoJsonRenderer.Application/   # Casos de uso e lógica de aplicação
│   ├── GeoJsonRenderer.Infrastructure/# Implementações externas
│   ├── GeoJsonRenderer.Mapping/       # Serviços de mapeamento
│   └── GeoJsonRenderer.ConsoleApp/    # Interface de linha de comando
└── tests/                             # Testes automatizados
```

## 💻 Como Usar

### Configuração do Ambiente

1. Requisitos:
   - .NET 8 SDK 
   - Conexão com Internet (para tiles de mapa)

2. Clone o repositório:
   ```bash
   git clone [url-do-repositorio]
   cd print-maps
   ```

3. Restaure as dependências:
   ```bash
   dotnet restore
   ```

### Exemplo de Uso

1. Crie um arquivo de configuração `config.json`:
```json
{
  "inputFilePath": "caminho/para/seu/arquivo.geojson",
  "outputFilePath": "saida/mapa.jpg",
  
  "filters": [
    {"property": "regiao", "value": "norte"},
    {"property": "tipo", "value": "urbano"}
  ],
  
  "renderOptions": {
    "width": 1920,
    "height": 1080,
    "format": "jpeg",
    "quality": 90,
    "autoCenter": true
  }
}
```

2. Execute via linha de comando:
```bash
dotnet run --project src/GeoJsonRenderer.ConsoleApp/GeoJsonRenderer.ConsoleApp.csproj config.json
```

### Integração em Projetos .NET

```csharp
// Configurar serviços
services.AddGeoJsonRendererInfrastructure();

// Usar o serviço
var geoJsonService = serviceProvider.GetRequiredService<GeoJsonService>();
var resultado = await geoJsonService.ProcessAndRenderAsync(options, filtros, estilos);
```

## 🛠️ Tecnologias Utilizadas

- **NetTopologySuite** (v2.5.0) e **NetTopologySuite.IO.GeoJSON** (v3.0.0): Processamento e leitura de geometrias GeoJSON
- **SkiaSharp** (v2.88.7): Renderização gráfica de alta performance
- **Microsoft.Extensions.DependencyInjection** (v8.0.0): Injeção de dependência
- **Microsoft.Extensions.Logging** (v8.0.0) e **Microsoft.Extensions.Logging.Abstractions** (v8.0.0): Logging estruturado
- **Newtonsoft.Json** (v13.x): Desserialização de GeoJSON no processador

## 📚 Documentação Adicional

## 🧪 Testes

Execute os testes automatizados:
```bash
dotnet test
```

## 🤝 Contribuindo

1. Fork o projeto
2. Crie sua branch de feature (`git checkout -b feature/NovaFeature`)
3. Commit suas mudanças (`git commit -m "Adiciona nova feature"`)
4. Push para a branch (`git push origin feature/NovaFeature`)
5. Abra um Pull Request

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

