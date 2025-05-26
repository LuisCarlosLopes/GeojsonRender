using System.IO;
using System.Threading.Tasks;
using GeoJsonRenderer.Domain.Interfaces;
using GeoJsonRenderer.Domain.Models;
using GeoJsonRenderer.Infrastructure.Interfaces;

namespace GeoJsonRenderer.Infrastructure.Mapping
{
    /// <summary>
    /// Adaptador que implementa ITileProvider do Domain usando ITileProvider da Infrastructure
    /// </summary>
    public class ITileProviderAdapter : Domain.Interfaces.ITileProvider
    {
        private readonly Infrastructure.Interfaces.ITileProvider _infrastructureTileProvider;

        public ITileProviderAdapter(Infrastructure.Interfaces.ITileProvider infrastructureTileProvider)
        {
            _infrastructureTileProvider = infrastructureTileProvider;
        }

        public int TileSize => _infrastructureTileProvider.TileSize;

        public Task<Stream> GetTileAsync(int x, int y, int zoom)
        {
            return _infrastructureTileProvider.GetTileAsync(x, y, zoom);
        }

        public int CalculateOptimalZoomLevel(BoundingBox boundingBox, int width, int height)
        {
            return _infrastructureTileProvider.CalculateOptimalZoomLevel(boundingBox, width, height);
        }

        public (double X, double Y) GeoToPixel(double longitude, double latitude, int zoom, int width, int height, BoundingBox boundingBox)
        {
            return _infrastructureTileProvider.GeoToPixel(longitude, latitude, zoom, width, height, boundingBox);
        }
    }
}