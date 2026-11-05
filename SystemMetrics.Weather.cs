// Live weather from Open-Meteo (free, no API key) for the city chosen in Settings > Weather,
// in °C or °F. Fills the weather card, the full Weather tab (hourly + 7-day) and refreshes every 15 min.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class WeatherSlot
    {
        public string Label { get; set; } = "";
        public Geometry? Icon { get; set; }
        public Brush IconBrush { get; set; } = Brushes.White;
        public string Temp { get; set; } = "";
        public string Sub { get; set; } = "";
        public Brush SubBrush { get; set; } = Brushes.White;
    }

    public class AgendaDay
    {
        public string Number { get; set; } = "";
        public bool IsToday { get; set; }
        public bool IsSunday { get; set; }
    }

    public partial class SystemMetrics
    {
        private DateTime _agendaDate;
        private string _agendaMonthTitle = "";
        private System.Collections.Generic.List<AgendaDay> _agendaWeek = new();
        public string AgendaMonthTitle { get => _agendaMonthTitle; private set { _agendaMonthTitle = value; OnPropertyChanged(); } }
        public System.Collections.Generic.List<AgendaDay> AgendaWeek { get => _agendaWeek; private set { _agendaWeek = value; OnPropertyChanged(); } }

        private string _agendaKey = "";
        private System.Collections.Generic.List<string> _agendaHeaders = new();
        /// <summary>Day-of-week letters for the mini calendar, in the order chosen in Settings > Calendar.</summary>
        public System.Collections.Generic.List<string> AgendaHeaders { get => _agendaHeaders; private set { _agendaHeaders = value; OnPropertyChanged(); } }

        /// <summary>Current week (Monday- or Sunday-first, per Settings > Calendar) for the Agenda mini calendar.</summary>
        internal void UpdateAgendaWeek(DateTime now)
        {
            bool mondayFirst = SettingsManager.Instance?.CalendarWeekStartsMonday ?? true;
            bool markToday = SettingsManager.Instance?.CalendarMarkToday ?? true;
            string key = $"{now.Date:yyyyMMdd}|{mondayFirst}|{markToday}";
            if (_agendaKey == key && _agendaWeek.Count == 7) return;
            _agendaKey = key;
            _agendaDate = now.Date;
            AgendaMonthTitle = now.ToString("MMMM yyyy", new CultureInfo("id-ID"));
            int offset = mondayFirst ? ((int)now.DayOfWeek + 6) % 7 : (int)now.DayOfWeek;
            DateTime first = now.Date.AddDays(-offset);
            var list = new System.Collections.Generic.List<AgendaDay>();
            var headers = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 7; i++)
            {
                var d = first.AddDays(i);
                list.Add(new AgendaDay { Number = d.Day.ToString(), IsToday = markToday && d == now.Date, IsSunday = d.DayOfWeek == DayOfWeek.Sunday });
                headers.Add(d.DayOfWeek.ToString().Substring(0, 2));
            }
            AgendaHeaders = headers;
            AgendaWeek = list;
        }

        /// <summary>Rebuilds the mini calendar right away (used when its settings change).</summary>
        public void RefreshAgenda() => UpdateAgendaWeek(DateTime.Now);

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
        private DispatcherTimer? _weatherTimer;
        private Geometry? _weatherIcon;
        private Brush _weatherIconBrush = Brushes.White;
        private string _weatherStatus = "Memuat cuaca...";

        public ObservableCollection<WeatherSlot> WeatherHourly { get; } = new();
        public ObservableCollection<WeatherSlot> WeatherDaily { get; } = new();

        public Geometry? WeatherIcon { get => _weatherIcon; private set { _weatherIcon = value; OnPropertyChanged(); } }
        public Brush WeatherIconBrush { get => _weatherIconBrush; private set { _weatherIconBrush = value; OnPropertyChanged(); } }
        public string WeatherStatus { get => _weatherStatus; private set { _weatherStatus = value; OnPropertyChanged(); } }

        // ---------- icons ----------
        private static readonly Geometry GSun = Freeze("M12,7a5,5 0 1,0 0.001,0z M11,0.5h2v3.5h-2z M11,20h2v3.5h-2z M0.5,11h3.5v2H0.5z M20,11h3.5v2H20z M3.5,4.9l1.4-1.4 2.5,2.5-1.4,1.4z M16.6,18l1.4-1.4 2.5,2.5-1.4,1.4z M3.5,19.1l2.5-2.5 1.4,1.4-2.5,2.5z M16.6,6l2.5-2.5 1.4,1.4-2.5,2.5z");
        private static readonly Geometry GMoon = Freeze("M12.3,2C6.9,2.5,2.7,6.9,2.7,12.3c0,5.5,4.5,10,10,10c5.4,0,9.8-4.2,10.3-9.6c-0.9,0.3-1.8,0.5-2.8,0.5c-4.4,0-8-3.6-8-8C12.2,4.1,12.5,3,12.3,2z");
        private static readonly Geometry GCloud = Freeze("M19.35,10.04C18.67,6.59,15.64,4,12,4c-3.72,0-6.84,2.56-7.65,6.04C1.84,10.61,0,12.8,0,15.5 0,18.54,2.46,21,5.5,21h13c2.48,0,4.5-2.02,4.5-4.5 0-2.3-1.73-4.18-3.95-4.46z");
        private static readonly Geometry GRain = Freeze("M14.5,6.5C13.9,4.5,12.1,3,10,3C7.6,3,5.6,4.8,5.2,7.1C3.4,7.5,2,9.1,2,11c0,2.2,1.8,4,4,4h8.5c1.9,0,3.5-1.6,3.5-3.5 0-1.8-1.5-3.4-3.5-5z M6,16.5l-1,3.5h1.2l1,-3.5z M9.5,16.5l-1,3.5h1.2l1,-3.5z M13,16.5l-1,3.5h1.2l1,-3.5z");
        private static readonly Geometry GStorm = Freeze("M14.5,6.5C13.9,4.5,12.1,3,10,3C7.6,3,5.6,4.8,5.2,7.1C3.4,7.5,2,9.1,2,11c0,2.2,1.8,4,4,4h8.5c1.9,0,3.5-1.6,3.5-3.5 0-1.8-1.5-3.4-3.5-5z M10.5,15l-2.5,4.5h2.2l-1,4 4-5.5h-2.4l1.2-3z");
        private static readonly Geometry GFog = Freeze("M3,7h18v2H3z M3,11h18v2H3z M3,15h18v2H3z");
        private static readonly Geometry GSnow = Freeze("M11,2h2v20h-2z M2,11h20v2H2z M4.2,5.6l1.4-1.4 14.2,14.2-1.4,1.4z M4.2,18.4l14.2-14.2 1.4,1.4-14.2,14.2z");

        private static readonly Brush BYellow = FreezeB(0xFF, 0xD6, 0x0A);
        private static readonly Brush BBlue = FreezeB(0x5A, 0xC8, 0xFA);
        private static readonly Brush BWhite = FreezeB(0xFF, 0xFF, 0xFF);
        private static readonly Brush BGray = FreezeB(0xC7, 0xC7, 0xCC);
        private static readonly Brush BSecondary = new SolidColorBrush(Color.FromArgb(0x99, 0xEB, 0xEB, 0xF5));

        private static Geometry Freeze(string d) { var g = Geometry.Parse(d); g.Freeze(); return g; }
        private static Brush FreezeB(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

        private static (Geometry, Brush) IconFor(int code, bool isDay) => code switch
        {
            0 or 1 => isDay ? (GSun, BYellow) : (GMoon, BYellow),
            2 or 3 => (GCloud, BWhite),
            45 or 48 => (GFog, BGray),
            >= 51 and <= 67 => (GRain, BBlue),
            >= 71 and <= 77 => (GSnow, BWhite),
            >= 80 and <= 82 => (GRain, BBlue),
            85 or 86 => (GSnow, BWhite),
            >= 95 => (GStorm, BYellow),
            _ => (GCloud, BWhite)
        };

        private static string Describe(int code) => code switch
        {
            0 => "Cerah",
            1 => "Cerah berawan",
            2 => "Berawan sebagian",
            3 => "Berawan",
            45 or 48 => "Berkabut",
            51 or 53 or 55 => "Gerimis",
            56 or 57 => "Gerimis beku",
            61 => "Hujan ringan",
            63 => "Hujan sedang",
            65 => "Hujan lebat",
            66 or 67 => "Hujan beku",
            71 or 73 or 75 or 77 => "Salju",
            80 => "Hujan lokal ringan",
            81 => "Hujan lokal",
            82 => "Hujan lokal lebat",
            85 or 86 => "Hujan salju",
            95 => "Badai petir",
            96 or 99 => "Badai petir & es",
            _ => "Berawan"
        };

        private static (double lat, double lon, string name) CityCoords(string? city) => (city ?? "").Trim().ToLowerInvariant() switch
        {
            "surabaya" => (-7.2575, 112.7521, "Surabaya"),
            "bandung" => (-6.9175, 107.6191, "Bandung"),
            "bali" => (-8.6705, 115.2126, "Denpasar, Bali"),
            "yogyakarta" => (-7.7956, 110.3695, "Yogyakarta"),
            "medan" => (3.5952, 98.6722, "Medan"),
            _ => (-6.2088, 106.8456, "Jakarta")
        };

        /// <summary>Starts periodic weather updates and follows Settings changes.</summary>
        public void StartWeather()
        {
            if (_weatherTimer != null) return;
            _weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _weatherTimer.Tick += async (s, e) => await RefreshWeatherAsync();
            _weatherTimer.Start();

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName is nameof(SettingsManager.WeatherCity) or nameof(SettingsManager.WeatherUnit))
                        await RefreshWeatherAsync();
                };
            }
            _ = RefreshWeatherAsync();
        }

        public async Task RefreshWeatherAsync()
        {
            var settings = SettingsManager.Instance;
            var (lat, lon, name) = CityCoords(settings?.WeatherCity);
            bool fahrenheit = (settings?.WeatherUnit ?? "").Contains("Fahrenheit", StringComparison.OrdinalIgnoreCase);

            string url = "https://api.open-meteo.com/v1/forecast?" +
                $"latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,surface_pressure,is_day" +
                "&hourly=temperature_2m,precipitation_probability,weather_code,is_day" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max" +
                "&timezone=auto&forecast_days=7" + (fahrenheit ? "&temperature_unit=fahrenheit" : "");

            try
            {
                string json = await Http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                _uiDispatcher.Invoke(() => ApplyWeather(root, name));
            }
            catch
            {
                _uiDispatcher.Invoke(() =>
                {
                    WeatherStatus = "Cuaca tidak tersedia (offline)";
                    if (WeatherHourly.Count == 0)
                    {
                        WeatherLocation = name.ToUpperInvariant();
                        WeatherSubtitle = $"{name}, Indonesia";
                        WeatherCondition = "Tidak ada koneksi";
                        WeatherTemp = "--°";
                        WeatherHighLow = "";
                    }
                });
            }
        }

        private static int Round(JsonElement e) => (int)Math.Round(e.GetDouble());

        private void ApplyWeather(JsonElement root, string cityName)
        {
            var cur = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            var hourly = root.GetProperty("hourly");

            int code = cur.GetProperty("weather_code").GetInt32();
            bool isDay = cur.GetProperty("is_day").GetInt32() == 1;
            int temp = Round(cur.GetProperty("temperature_2m"));
            int feels = Round(cur.GetProperty("apparent_temperature"));
            int hi = Round(daily.GetProperty("temperature_2m_max")[0]);
            int lo = Round(daily.GetProperty("temperature_2m_min")[0]);

            WeatherLocation = cityName.ToUpperInvariant();
            WeatherSubtitle = $"{cityName}, Indonesia";
            WeatherCondition = Describe(code);
            WeatherTemp = $"{temp}°";
            TemperatureText = $"{temp}°";
            WeatherHighLow = $"H: {hi}° · L: {lo}°";
            WeatherFeelsLike = $"Terasa {feels}°";
            WeatherHumidity = $"{Round(cur.GetProperty("relative_humidity_2m"))}%";
            WeatherWind = $"{Round(cur.GetProperty("wind_speed_10m"))} km/h";
            WeatherPressure = $"{Round(cur.GetProperty("surface_pressure"))} hPa";
            WeatherUv = $"UV {Round(daily.GetProperty("uv_index_max")[0])}";
            WeatherSunrise = FormatClock(daily.GetProperty("sunrise")[0].GetString());
            WeatherSunset = FormatClock(daily.GetProperty("sunset")[0].GetString());

            var (icon, brush) = IconFor(code, isDay);
            WeatherIcon = icon;
            WeatherIconBrush = brush;
            WeatherStatus = $"Diperbarui {DateTime.Now:HH:mm}";

            // Hourly: now + next 6 hours, every 3 hours
            WeatherHourly.Clear();
            var times = hourly.GetProperty("time");
            int start = 0;
            DateTime now = DateTime.Now;
            for (int i = 0; i < times.GetArrayLength(); i++)
            {
                if (DateTime.TryParse(times[i].GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) && t > now.AddHours(-1))
                { start = i; break; }
            }
            for (int k = 0; k < 7; k++)
            {
                int i = start + k * 3;
                if (i >= times.GetArrayLength()) break;
                DateTime.TryParse(times[i].GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t);
                var (hIcon, hBrush) = IconFor(hourly.GetProperty("weather_code")[i].GetInt32(), hourly.GetProperty("is_day")[i].GetInt32() == 1);
                var pp = hourly.GetProperty("precipitation_probability")[i];
                WeatherHourly.Add(new WeatherSlot
                {
                    Label = k == 0 ? "Kini" : t.ToString("HH:mm"),
                    Icon = hIcon,
                    IconBrush = hBrush,
                    Temp = $"{Round(hourly.GetProperty("temperature_2m")[i])}°",
                    Sub = pp.ValueKind == JsonValueKind.Number ? $"{pp.GetInt32()}%" : "-",
                    SubBrush = BBlue
                });
            }

            // Daily: 7 days
            WeatherDaily.Clear();
            var dTimes = daily.GetProperty("time");
            var idCulture = new CultureInfo("id-ID");
            for (int i = 0; i < dTimes.GetArrayLength() && i < 7; i++)
            {
                DateTime.TryParse(dTimes[i].GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d);
                var (dIcon, dBrush) = IconFor(daily.GetProperty("weather_code")[i].GetInt32(), true);
                WeatherDaily.Add(new WeatherSlot
                {
                    Label = i == 0 ? "Hari ini" : d.ToString("ddd", idCulture),
                    Icon = dIcon,
                    IconBrush = dBrush,
                    Temp = $"{Round(daily.GetProperty("temperature_2m_max")[i])}°",
                    Sub = $"{Round(daily.GetProperty("temperature_2m_min")[i])}°",
                    SubBrush = BSecondary
                });
            }
        }

        private static string FormatClock(string? iso) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm") : "--:--";
    }
}
