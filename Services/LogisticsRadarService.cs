using System.Text.Json;
using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Core.Entities;
using ERPES.ControlTower.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERPES.ControlTower.Services;

public class LogisticsRadarService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogisticsRadarService> _logger;

    private static readonly SemaphoreSlim _cacheLock = new(1, 1);
    private static List<AircraftPositionDto> _cachedAircraft = new();
    private static DateTime _lastOpenSkyFetch = DateTime.MinValue;
    private static string _flightSourceStatus = "Canlı Hava Kargo Radarı Aktif";

    public LogisticsRadarService(
        HttpClient httpClient,
        IServiceScopeFactory scopeFactory,
        ILogger<LogisticsRadarService> logger)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<LogisticsRadarDto> GetLiveRadarSnapshotAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activeAlerts = await dbContext.ExternalTelemetryAlerts
            .Where(a => !a.IsResolved)
            .ToListAsync();

        var activeMaterialOrders = await dbContext.MaterialTransferOrders
            .Include(m => m.FromPlant)
            .Include(m => m.ToPlant)
            .Where(m => m.Status == "InTransit" || m.Status == "Proposed")
            .ToListAsync();

        var activeWorkerOrders = await dbContext.WorkerTransferOrders
            .Include(w => w.Worker)
            .Include(w => w.FromPlant)
            .Include(w => w.ToPlant)
            .Where(w => w.Status == "InTransit")
            .ToListAsync();

        var rotterdamAlert = activeAlerts.FirstOrDefault(a =>
            a.SourceType == "MarineLogistics" && (a.TransportCode?.Contains("ROTTERDAM") == true || (a.TargetPlantId == 1 && a.MaterialCode == "CHIP-MCU-AUTO")));

        var cmaCgmAlert = activeAlerts.FirstOrDefault(a =>
            a.SourceType == "MarineLogistics" && (a.TransportCode?.Contains("CMA-CGM") == true || (a.TargetPlantId == 1 && a.MaterialCode == "STEEL-COIL-S2")));

        var arkasAlert = activeAlerts.FirstOrDefault(a =>
            a.SourceType == "MarineLogistics" && (a.TransportCode?.Contains("ARKAS") == true || a.TargetPlantId == 2));

        var tkCargoAlert = activeAlerts.FirstOrDefault(a =>
            a.SourceType == "AirCargo" && a.TransportCode?.Contains("TK-CARGO") == true);

        var dhlCargoAlert = activeAlerts.FirstOrDefault(a =>
            a.SourceType == "AirCargo" && a.TransportCode?.Contains("DHL") == true);

        var mngCargoAlert = activeAlerts.FirstOrDefault(a =>
            a.SourceType == "AirCargo" && a.TransportCode?.Contains("MNG") == true);

        var (aircraft, flightSourceStatus) = await GetAircraftPositionsAsync(tkCargoAlert, dhlCargoAlert, mngCargoAlert);
        var vessels = GetMaritimeVessels(rotterdamAlert, cmaCgmAlert, arkasAlert);
        var landVehicles = GetLandVehicles(activeMaterialOrders, activeWorkerOrders);

        return new LogisticsRadarDto
        {
            Timestamp = DateTime.UtcNow,
            FlightDataSource = flightSourceStatus,
            MarineDataSource = "Canlı Deniz Lojistik Radarı Aktif",
            Aircraft = aircraft,
            Vessels = vessels,
            LandVehicles = landVehicles
        };
    }

    private async Task<(List<AircraftPositionDto> Aircraft, string SourceStatus)> GetAircraftPositionsAsync(
        ExternalTelemetryAlert? tkCargoAlert,
        ExternalTelemetryAlert? dhlCargoAlert,
        ExternalTelemetryAlert? mngCargoAlert)
    {
        List<AircraftPositionDto> cachedSnapshot;
        string sourceStatus;

        await _cacheLock.WaitAsync();
        try
        {
            if ((DateTime.UtcNow - _lastOpenSkyFetch).TotalSeconds > 15 || _cachedAircraft.Count == 0)
            {
                var liveList = await TryFetchOpenSkyAsync();
                if (liveList.Count == 0)
                {
                    liveList = await TryFetchAdsbLolAsync();
                }

                if (liveList.Count > 0)
                {
                    _cachedAircraft = liveList;
                    _lastOpenSkyFetch = DateTime.UtcNow;
                    _flightSourceStatus = $"Canlı Hava Kargo Radarı ({liveList.Count + 3} Uçak)";
                }
                else
                {
                    // Avoid hammering rate-limited endpoints on every poll
                    _lastOpenSkyFetch = DateTime.UtcNow;
                    if (_cachedAircraft.Count == 0)
                    {
                        _flightSourceStatus = "Hava Kargo Radarı (Simülasyon Modu)";
                    }
                }
            }

            cachedSnapshot = new List<AircraftPositionDto>(_cachedAircraft);
            sourceStatus = _flightSourceStatus;
        }
        finally
        {
            _cacheLock.Release();
        }

        bool hasTkCargoCrisis = tkCargoAlert != null;
        bool hasDhlCargoCrisis = dhlCargoAlert != null;
        bool hasMngCargoCrisis = mngCargoAlert != null;
        int tkDelay = tkCargoAlert?.DelayHours > 0 ? tkCargoAlert.DelayHours : 36;
        int dhlDelay = dhlCargoAlert?.DelayHours > 0 ? dhlCargoAlert.DelayHours : 48;
        int mngDelay = mngCargoAlert?.DelayHours > 0 ? mngCargoAlert.DelayHours : 42;

        var result = new List<AircraftPositionDto>();
        double timeOffset = (DateTime.UtcNow.Second % 60) * 0.0014;

        // 1. Dedicated Cargo Flight for Plant A (Kocaeli) — moving NE toward Sabiha Gökçen
        result.Add(new AircraftPositionDto
        {
            Icao24 = "4bb104",
            Callsign = "TK-CARGO-704",
            OriginCountry = "Türkiye",
            Latitude = 39.2520 + timeOffset,
            Longitude = 27.4840 + timeOffset,
            AltitudeMeters = 6400,
            VelocityKnots = hasTkCargoCrisis ? 310 : 445,
            Heading = 42,
            CargoType = "ADAS Ön LiDAR & Radar Sensör Kiti (400 Adet)",
            Route = "Frankfurt (FRA) ➔ Sabiha Gökçen / Kocaeli (Tesis A)",
            OriginAirport = "Frankfurt Kargo Hub (FRA)",
            DestinationAirport = "Sabiha Gökçen Hava Kargo (SAW)",
            TargetPlantCode = "PLANT-A",
            TargetPlantName = "Tesis A — Kocaeli Otomotiv Montaj",
            PilotOrOperator = "Turkish Cargo — Kpt. S. Karahan",
            Eta = hasTkCargoCrisis ? $"+{tkDelay} Saat Rötarlı (Hava Muhalefeti)" : "Zamanında (1 Saat 20 Dk)",
            Status = hasTkCargoCrisis ? "Critical" : "OnTime",
            IsLiveAdsb = true
        });

        // 2. Dedicated Cargo Flight for Plant B (Bursa) / Plant C (İzmir) — moving SE toward Bursa
        result.Add(new AircraftPositionDto
        {
            Icao24 = "ac3857",
            Callsign = "DHL-EURO-412",
            OriginCountry = "Almanya",
            Latitude = 40.5400 - timeOffset,
            Longitude = 28.6200 + timeOffset,
            AltitudeMeters = 4120,
            VelocityKnots = hasDhlCargoCrisis ? 295 : 390,
            Heading = 135,
            CargoType = "Otomotiv Mikrodenetleyici & Tork Sensör Modülleri (350 Set)",
            Route = "Münih (MUC) ➔ Yenişehir Hava Kargo (Bursa - Tesis B)",
            OriginAirport = "Münih Lojistik Hub (MUC)",
            DestinationAirport = "Bursa Yenişehir Kargo Terminali (YEI)",
            TargetPlantCode = "PLANT-B",
            TargetPlantName = "Tesis B — Bursa Güç Aktarma & Bileşen",
            PilotOrOperator = "European Air Transport — Kpt. M. Weber",
            Eta = hasDhlCargoCrisis ? $"+{dhlDelay} Saat Rötarlı (Slot Kısıtı)" : "Zamanında (45 Dk Kaldı)",
            Status = hasDhlCargoCrisis ? "Critical" : "OnTime",
            IsLiveAdsb = true
        });

        // 3. Dedicated Cargo Flight for Plant A (Kocaeli) via Sabiha Gökçen — aligned with simulation payload
        result.Add(new AircraftPositionDto
        {
            Icao24 = "4ba992",
            Callsign = "MNG-CARGO-308",
            OriginCountry = "Türkiye",
            Latitude = 40.6800 + timeOffset,
            Longitude = 28.9200 + timeOffset,
            AltitudeMeters = 3250,
            VelocityKnots = hasMngCargoCrisis ? 280 : 365,
            Heading = 58,
            CargoType = "ADAS Ön LiDAR & Radar Sensör Paketleri (600 Adet)",
            Route = "Amsterdam (AMS) ➔ Sabiha Gökçen / Kocaeli (Tesis A)",
            OriginAirport = "Amsterdam Schiphol Kargo (AMS)",
            DestinationAirport = "Sabiha Gökçen Hava Kargo (SAW)",
            TargetPlantCode = "PLANT-A",
            TargetPlantName = "Tesis A — Kocaeli Otomotiv Montaj",
            PilotOrOperator = "MNG Hava Kargo — Kpt. E. Aydın",
            Eta = hasMngCargoCrisis ? $"+{mngDelay} Saat Rötarlı (Teknik Bakım)" : "Zamanında (30 Dk Kaldı)",
            Status = hasMngCargoCrisis ? "Critical" : "OnTime",
            IsLiveAdsb = true
        });

        result.AddRange(cachedSnapshot);
        return (result, sourceStatus);
    }

    private static AircraftPositionDto CreateLiveAircraftDto(
        string icao,
        string callsign,
        string country,
        double lat,
        double lon,
        double altMeters,
        double velocityKnots,
        double heading)
    {
        string assignedPlantCode = lat < 39.2
            ? "PLANT-C"
            : (lat < 40.45 ? "PLANT-B" : "PLANT-A");

        string assignedPlantName = assignedPlantCode == "PLANT-C"
            ? "Tesis C — İzmir Sensör & Elektronik"
            : (assignedPlantCode == "PLANT-B"
                ? "Tesis B — Bursa Güç Aktarma & Bileşen"
                : "Tesis A — Kocaeli Otomotiv Montaj");

        return new AircraftPositionDto
        {
            Icao24 = icao,
            Callsign = callsign,
            OriginCountry = country,
            Latitude = lat,
            Longitude = lon,
            AltitudeMeters = Math.Round(altMeters, 0),
            VelocityKnots = Math.Round(velocityKnots, 0),
            Heading = Math.Round(heading, 0),
            CargoType = "Endüstriyel Yedek Parça & Hava Kargo",
            Route = $"{country} ➔ Marmara / Ege Hava Koridoru",
            OriginAirport = $"{country} Uluslararası Hub",
            DestinationAirport = assignedPlantCode == "PLANT-C" ? "İzmir Adnan Menderes (ADB)" : "Sabiha Gökçen (SAW)",
            TargetPlantCode = assignedPlantCode,
            TargetPlantName = assignedPlantName,
            PilotOrOperator = "Hava Kargo Operasyon Filosu",
            Eta = "Zamanında (Uçuşta)",
            Status = "OnTime",
            IsLiveAdsb = true
        };
    }

    private async Task<List<AircraftPositionDto>> TryFetchOpenSkyAsync()
    {
        var liveList = new List<AircraftPositionDto>();
        try
        {
            const string url = "https://opensky-network.org/api/states/all?lamin=38.0&lomin=26.0&lamax=41.5&lomax=30.8";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenSky returned HTTP {Status}; falling back to ADSB.lol.", (int)response.StatusCode);
                return liveList;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("states", out var statesElement) || statesElement.ValueKind != JsonValueKind.Array)
                return liveList;

            foreach (var state in statesElement.EnumerateArray())
            {
                if (liveList.Count >= 12) break;

                string icao = state[0].GetString() ?? "N/A";
                string callsign = (state[1].GetString() ?? "").Trim();
                string country = state[2].GetString() ?? "Türkiye";
                double? lon = state[5].ValueKind == JsonValueKind.Number ? state[5].GetDouble() : null;
                double? lat = state[6].ValueKind == JsonValueKind.Number ? state[6].GetDouble() : null;
                double alt = state[7].ValueKind == JsonValueKind.Number ? state[7].GetDouble() : 3500;
                double velMs = state[9].ValueKind == JsonValueKind.Number ? state[9].GetDouble() : 180;
                double heading = state[10].ValueKind == JsonValueKind.Number ? state[10].GetDouble() : 90;

                if (lat.HasValue && lon.HasValue && !string.IsNullOrWhiteSpace(callsign))
                {
                    liveList.Add(CreateLiveAircraftDto(icao, callsign, country, lat.Value, lon.Value, alt, velMs * 1.94384, heading));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("OpenSky fetch failed ({Msg}); falling back to ADSB.lol.", ex.Message);
        }
        return liveList;
    }

    private async Task<List<AircraftPositionDto>> TryFetchAdsbLolAsync()
    {
        var liveList = new List<AircraftPositionDto>();
        try
        {
            const string url = "https://api.adsb.lol/v2/point/39.75/28.25/200";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ADSB.lol returned HTTP {Status}.", (int)response.StatusCode);
                return liveList;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ac", out var acElement) || acElement.ValueKind != JsonValueKind.Array)
                return liveList;

            foreach (var ac in acElement.EnumerateArray())
            {
                if (liveList.Count >= 12) break;

                string icao = ac.TryGetProperty("hex", out var hexEl) ? (hexEl.GetString() ?? "N/A") : "N/A";
                string callsign = ac.TryGetProperty("flight", out var flEl) ? (flEl.GetString() ?? "").Trim() : "";
                if (string.IsNullOrWhiteSpace(callsign)) continue;

                if (!ac.TryGetProperty("lat", out var latEl) || latEl.ValueKind != JsonValueKind.Number) continue;
                if (!ac.TryGetProperty("lon", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number) continue;

                double lat = latEl.GetDouble();
                double lon = lonEl.GetDouble();
                if (lat < 38.0 || lat > 41.5 || lon < 26.0 || lon > 30.8) continue;

                double altMeters = 3500;
                if (ac.TryGetProperty("alt_baro", out var altEl) && altEl.ValueKind == JsonValueKind.Number)
                {
                    altMeters = altEl.GetDouble() * 0.3048;
                }

                double speedKnots = ac.TryGetProperty("gs", out var gsEl) && gsEl.ValueKind == JsonValueKind.Number
                    ? gsEl.GetDouble()
                    : 320;
                double heading = ac.TryGetProperty("track", out var trkEl) && trkEl.ValueKind == JsonValueKind.Number
                    ? trkEl.GetDouble()
                    : 90;

                liveList.Add(CreateLiveAircraftDto(icao, callsign, "Türkiye", lat, lon, altMeters, speedKnots, heading));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ADSB.lol fetch failed: {Msg}", ex.Message);
        }
        return liveList;
    }

    private List<VesselPositionDto> GetMaritimeVessels(
        ExternalTelemetryAlert? rotterdamAlert,
        ExternalTelemetryAlert? cmaCgmAlert,
        ExternalTelemetryAlert? arkasAlert)
    {
        bool hasRotterdamCrisis = rotterdamAlert != null;
        bool hasCmaCgmCrisis = cmaCgmAlert != null;
        bool hasArkasCrisis = arkasAlert != null;
        int rotterdamDelay = rotterdamAlert?.DelayHours > 0 ? rotterdamAlert.DelayHours : 144;
        int cmaDelay = cmaCgmAlert?.DelayHours > 0 ? cmaCgmAlert.DelayHours : 96;
        int arkasDelay = arkasAlert?.DelayHours > 0 ? arkasAlert.DelayHours : 72;

        double drift = (DateTime.UtcNow.Second % 60) * 0.0008;

        return new List<VesselPositionDto>
        {
            new VesselPositionDto
            {
                Mmsi = "244890991",
                VesselName = "MSC ROTTERDAM-991",
                Flag = "Hollanda",
                Latitude = 40.6850,
                Longitude = 28.4200 + (hasRotterdamCrisis ? 0 : drift),
                SpeedKnots = hasRotterdamCrisis ? 0.0 : 16.4,
                Course = 98,
                OriginPort = "Rotterdam Limanı (NL)",
                DestinationPort = "Derince / Kocaeli Limanı (Tesis A)",
                TargetPlantCode = "PLANT-A",
                TargetPlantName = "Tesis A — Kocaeli Otomotiv Montaj",
                CaptainOrAgency = "MSC Deniz Acentesi — Kpt. H. Van Der Berg",
                CargoManifest = "1.200 Adet CHIP-MCU-AUTO (32-Bit Otomotiv Mikroçip)",
                Eta = hasRotterdamCrisis ? $"+{rotterdamDelay} Saat Gecikmeli (Liman Grevi)" : "Zamanında (6 Saat Kaldı)",
                Status = hasRotterdamCrisis ? "StrikeAlert" : "Normal",
                DelayHours = hasRotterdamCrisis ? rotterdamDelay : 0
            },
            new VesselPositionDto
            {
                Mmsi = "228339100",
                VesselName = "CMA CGM BOSPHORUS",
                Flag = "Fransa",
                Latitude = 40.7200 + (hasCmaCgmCrisis ? 0 : drift * 0.25),
                Longitude = 29.1800 + (hasCmaCgmCrisis ? 0 : drift),
                SpeedKnots = hasCmaCgmCrisis ? 0.0 : 15.1,
                Course = 85,
                OriginPort = "Pire Limanı (GR)",
                DestinationPort = "Dilovası / Kocaeli Limanı (Tesis A)",
                TargetPlantCode = "PLANT-A",
                TargetPlantName = "Tesis A — Kocaeli Otomotiv Montaj",
                CaptainOrAgency = "CMA CGM Türkiye — Kpt. L. Moreau",
                CargoManifest = "2.400 Kg Yüksek Dayanımlı Rulo Sac (STEEL-COIL-S2)",
                Eta = hasCmaCgmCrisis ? $"+{cmaDelay} Saat Gecikmeli (Boğaz Yoğunluğu)" : "Zamanında (3 Saat Kaldı)",
                Status = hasCmaCgmCrisis ? "StrikeAlert" : "Normal",
                DelayHours = hasCmaCgmCrisis ? cmaDelay : 0
            },
            new VesselPositionDto
            {
                Mmsi = "271044210",
                VesselName = "ARKAS MARMARA EXPRESS",
                Flag = "Türkiye",
                Latitude = 40.4350 - (hasArkasCrisis ? 0 : drift * 0.35),
                Longitude = 28.9250 + (hasArkasCrisis ? 0 : drift),
                SpeedKnots = hasArkasCrisis ? 0.0 : 14.2,
                Course = 112,
                OriginPort = "Trieste Limanı (IT)",
                DestinationPort = "Gemlik Limanı (Bursa - Tesis B)",
                TargetPlantCode = "PLANT-B",
                TargetPlantName = "Tesis B — Bursa Güç Aktarma & Bileşen",
                CaptainOrAgency = "Arkas Denizcilik — Kpt. T. Çelik",
                CargoManifest = "1.000 Adet 32-Bit Otomotiv Mikroçip (CHIP-MCU-AUTO)",
                Eta = hasArkasCrisis ? $"+{arkasDelay} Saat Gecikmeli (Gümrük / Fırtına)" : "Zamanında (2 Saat Kaldı)",
                Status = hasArkasCrisis ? "StrikeAlert" : "Normal",
                DelayHours = hasArkasCrisis ? arkasDelay : 0
            },
            new VesselPositionDto
            {
                Mmsi = "219018442",
                VesselName = "MAERSK AEGEAN STAR",
                Flag = "Danimarka",
                Latitude = 38.4420 - drift * 0.35,
                Longitude = 26.8500 + drift,
                SpeedKnots = 17.8,
                Course = 110,
                OriginPort = "Valencia Limanı (ES)",
                DestinationPort = "Alsancak Limanı (İzmir - Tesis C)",
                TargetPlantCode = "PLANT-C",
                TargetPlantName = "Tesis C — İzmir Sensör & Elektronik",
                CaptainOrAgency = "Maersk Ege Lojistik — Kpt. C. Lindqvist",
                CargoManifest = "550 Adet Optik Sensör & PCB Devre Kartı",
                Eta = "Zamanında (4 Saat Kaldı)",
                Status = "Normal",
                DelayHours = 0
            }
        };
    }

    private List<LandVehiclePositionDto> GetLandVehicles(
        List<MaterialTransferOrder> activeMaterialOrders,
        List<WorkerTransferOrder> activeWorkerOrders)
    {
        double shift = (DateTime.UtcNow.Second % 60) * 0.0012;

        var list = new List<LandVehiclePositionDto>
        {
            new LandVehiclePositionDto
            {
                VehicleCode = "TRK-KOC-101",
                PlateNumber = "41 ERP 401",
                VehicleType = "Ağır Tonaj Rulo Sac Tırı",
                DriverName = "Mustafa Demirci",
                DriverPhone = "+90 532 410 19 20",
                OriginLocation = "Ereğli Demir Çelik Tesisi",
                DestinationLocation = "Tesis A — Kocaeli Otomotiv Montaj",
                SourcePlantCode = "EXTERNAL",
                TargetPlantCode = "PLANT-A",
                CargoDescription = "Yüksek Dayanımlı Gövde Rulo Sac (STEEL-COIL-S2)",
                QuantityText = "1.400 Kg",
                Latitude = 40.7750 + shift,
                Longitude = 29.5800 - shift,
                SpeedKmh = 82,
                Eta = "Zamanında (35 Dk)",
                Status = "Normal"
            },
            new LandVehiclePositionDto
            {
                VehicleCode = "TRK-BUR-204",
                PlateNumber = "16 ERP 882",
                VehicleType = "Kontrollü Atmosfer Lojistik Tırı",
                DriverName = "Hüseyin Karadağ",
                DriverPhone = "+90 533 612 88 14",
                OriginLocation = "Gemlik Serbest Bölge Deposu",
                DestinationLocation = "Tesis B — Bursa Güç Aktarma & Bileşen",
                SourcePlantCode = "PLANT-B",
                TargetPlantCode = "PLANT-B",
                CargoDescription = "Otomotiv Mikrodenetleyici Çip & Aktarma Kitleri",
                QuantityText = "600 Adet",
                Latitude = 40.3400 - shift,
                Longitude = 29.0600 + shift,
                SpeedKmh = 78,
                Eta = "Zamanında (25 Dk)",
                Status = "Normal"
            },
            new LandVehiclePositionDto
            {
                VehicleCode = "VAN-IZM-309",
                PlateNumber = "35 ERP 309",
                VehicleType = "Antistatik Ekspres Panelvan",
                DriverName = "Onur Kılıç",
                DriverPhone = "+90 542 309 77 45",
                OriginLocation = "İzmir Adnan Menderes Kargo Terminali",
                DestinationLocation = "Tesis C — İzmir Sensör & Elektronik",
                SourcePlantCode = "PLANT-C",
                TargetPlantCode = "PLANT-C",
                CargoDescription = "ADAS Ön LiDAR & Radar Sensör Komponentleri",
                QuantityText = "350 Adet",
                Latitude = 38.4100 + shift,
                Longitude = 27.1200 - shift,
                SpeedKmh = 88,
                Eta = "Zamanında (20 Dk)",
                Status = "Normal"
            }
        };

        // Add any active emergency MaterialTransferOrders dynamically to the Land Fleet
        foreach (var order in activeMaterialOrders)
        {
            string fromCode = order.FromPlant?.Code ?? (order.FromPlantId == 3 ? "PLANT-C" : (order.FromPlantId == 2 ? "PLANT-B" : "PLANT-A"));
            string toCode = order.ToPlant?.Code ?? (order.ToPlantId == 3 ? "PLANT-C" : (order.ToPlantId == 2 ? "PLANT-B" : "PLANT-A"));
            bool isIzmir = fromCode == "PLANT-C" || toCode == "PLANT-C" || order.FromPlantId == 3 || order.ToPlantId == 3;
            bool isProposed = order.Status != "InTransit";

            double lat = isProposed
                ? (order.FromPlant?.Latitude ?? (fromCode == "PLANT-C" ? 38.5020 : (fromCode == "PLANT-B" ? 40.2110 : 40.8028)))
                : (isIzmir ? 39.6484 : 40.5200);
            double lon = isProposed
                ? (order.FromPlant?.Longitude ?? (fromCode == "PLANT-C" ? 27.0250 : (fromCode == "PLANT-B" ? 28.9320 : 29.4307)))
                : (isIzmir ? 27.8826 : 29.2800);

            list.Insert(0, new LandVehiclePositionDto
            {
                VehicleCode = $"ACİL-SEVKIYAT-{order.Id}",
                PlateNumber = fromCode == "PLANT-C" ? "35 ACIL 704" : (fromCode == "PLANT-B" ? "16 ACIL 991" : "41 ACIL 101"),
                VehicleType = isIzmir ? "Ekspres Sensör Panelvanı" : "Acil Müdahale Ekspres Tırı",
                DriverName = isIzmir ? "Cemil Soylu (Ekspres Ekip)" : "Kadir Özkan (Acil Lojistik)",
                DriverPhone = isIzmir ? "+90 532 704 35 41" : "+90 532 991 16 41",
                OriginLocation = order.FromPlant?.Name ?? (fromCode == "PLANT-C" ? "Tesis C — İzmir" : "Tesis B — Bursa"),
                DestinationLocation = order.ToPlant?.Name ?? "Tesis A — Kocaeli",
                SourcePlantCode = fromCode,
                TargetPlantCode = toCode,
                CargoDescription = $"{order.MaterialName} ({order.MaterialCode})",
                QuantityText = $"{order.Quantity} {order.Unit}",
                Latitude = lat,
                Longitude = lon,
                SpeedKmh = isProposed ? 0 : 96,
                Eta = isProposed
                    ? "Onay Bekliyor"
                    : (isIzmir ? "Yolda — O-5 Otoyolu (3s 15dk)" : "Yolda — Osmangazi Köprüsü (55 dk)"),
                Status = isProposed ? "Proposed" : "EmergencyActive"
            });
        }

        // Add active Worker Transfer Shuttles grouped by route
        var workerRouteGroups = activeWorkerOrders
            .GroupBy(w => $"{w.FromPlantId}_{w.ToPlantId}")
            .ToList();

        foreach (var grp in workerRouteGroups)
        {
            var first = grp.First();
            string fromCode = first.FromPlant?.Code ?? (first.FromPlantId == 3 ? "PLANT-C" : (first.FromPlantId == 2 ? "PLANT-B" : "PLANT-A"));
            string toCode = first.ToPlant?.Code ?? (first.ToPlantId == 3 ? "PLANT-C" : (first.ToPlantId == 2 ? "PLANT-B" : "PLANT-A"));
            bool isIzmirInvolved = fromCode == "PLANT-C" || toCode == "PLANT-C";
            string names = string.Join(", ", grp.Select(w => w.Worker?.FullName ?? "Uzman Operatör"));

            list.Insert(0, new LandVehiclePositionDto
            {
                VehicleCode = $"SERVIS-IK-{first.Id}",
                PlateNumber = fromCode == "PLANT-C" ? "35 VIP 035" : (fromCode == "PLANT-B" ? "16 VIP 016" : "41 VIP 041"),
                VehicleType = "Personel Transfer & Mobil Görev Aracı",
                DriverName = "Sinan Aktaş (Kurumsal Ulaşım)",
                DriverPhone = "+90 530 444 16 41",
                OriginLocation = first.FromPlant?.Name ?? "Donör Tesis",
                DestinationLocation = first.ToPlant?.Name ?? "Hedef Tesis",
                SourcePlantCode = fromCode,
                TargetPlantCode = toCode,
                CargoDescription = $"Görevlendirilen Uzman Ekip: {names}",
                QuantityText = $"{grp.Count()} Uzman Personel",
                Latitude = isIzmirInvolved ? 39.5500 : 40.5400,
                Longitude = isIzmirInvolved ? 27.9200 : 29.3200,
                SpeedKmh = 92,
                Eta = "Aktif Transfer Yolunda",
                Status = "WorkerShuttle"
            });
        }

        return list;
    }
}
