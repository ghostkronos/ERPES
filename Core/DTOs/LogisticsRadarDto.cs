namespace ERPES.ControlTower.Core.DTOs;

public class LogisticsRadarDto
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string FlightDataSource { get; set; } = "Hava Kargo Gözetim Radarı";
    public string MarineDataSource { get; set; } = "Deniz Lojistik Gözetim Radarı";
    public List<AircraftPositionDto> Aircraft { get; set; } = new();
    public List<VesselPositionDto> Vessels { get; set; } = new();
    public List<LandVehiclePositionDto> LandVehicles { get; set; } = new();
}

public class AircraftPositionDto
{
    public string Icao24 { get; set; } = string.Empty;
    public string Callsign { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeMeters { get; set; }
    public double VelocityKnots { get; set; }
    public double Heading { get; set; }
    public string CargoType { get; set; } = "Hava Kargo / Komponent";
    public string Route { get; set; } = "Avrupa - Marmara Koridoru";
    public string OriginAirport { get; set; } = "Frankfurt (FRA)";
    public string DestinationAirport { get; set; } = "Sabiha Gökçen (SAW)";
    public string TargetPlantCode { get; set; } = "PLANT-A";
    public string TargetPlantName { get; set; } = "Tesis A — Kocaeli Otomotiv Montaj";
    public string PilotOrOperator { get; set; } = "Kargo Uçuş Ekibi";
    public string Eta { get; set; } = "Zamanında (1s 45dk)";
    public string Status { get; set; } = "OnTime";
    public bool IsLiveAdsb { get; set; } = true;
}

public class VesselPositionDto
{
    public string Mmsi { get; set; } = string.Empty;
    public string VesselName { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SpeedKnots { get; set; }
    public double Course { get; set; }
    public string OriginPort { get; set; } = string.Empty;
    public string DestinationPort { get; set; } = string.Empty;
    public string TargetPlantCode { get; set; } = "PLANT-A";
    public string TargetPlantName { get; set; } = "Tesis A — Kocaeli Otomotiv Montaj";
    public string CaptainOrAgency { get; set; } = "Deniz Acentesi";
    public string CargoManifest { get; set; } = string.Empty;
    public string Eta { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal";
    public int DelayHours { get; set; } = 0;
}

public class LandVehiclePositionDto
{
    public string VehicleCode { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = "Ekspres Tır";
    public string DriverName { get; set; } = string.Empty;
    public string DriverPhone { get; set; } = string.Empty;
    public string OriginLocation { get; set; } = string.Empty;
    public string DestinationLocation { get; set; } = string.Empty;
    public string TargetPlantCode { get; set; } = "PLANT-A";
    public string SourcePlantCode { get; set; } = "PLANT-B";
    public string CargoDescription { get; set; } = string.Empty;
    public string QuantityText { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int SpeedKmh { get; set; } = 85;
    public string Eta { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal";
}
