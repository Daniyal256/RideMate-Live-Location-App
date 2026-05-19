namespace RideMate.Application.Interfaces;

public interface IMapService
{
    // Calculates distance using Haversine: d = R * c [4]
    double GetDistance(double lat1, double lon1, double lat2, double lon2);
}