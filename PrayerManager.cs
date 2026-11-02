// Language: C#
// Filename: PrayerManager.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows)
// Targets: Prayer Times (Jadwal Sholat & Countdown Kemenag RI + Silent Island Notification)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace DynamicIsland
{
    public class PrayerItem
    {
        public string Name { get; set; } = "";
        public string TimeString { get; set; } = "";
        public TimeSpan Time { get; set; }
        public bool IsNext { get; set; }
        public bool IsPassed { get; set; }
    }

    public class PrayerManager : INotifyPropertyChanged
    {
        private static PrayerManager? _instance;
        public static PrayerManager Instance => _instance ??= new PrayerManager();

        private readonly DispatcherTimer _timer;

        private string _nextPrayerName = "Subuh";
        private string _nextPrayerTime = "04:25";
        private string _nextPrayerCountdown = "0j 0m";
        private string _prayerSummaryBadge = "🕌 Sholat: --";
        private List<PrayerItem> _todayPrayers = new();

        // Prevent spamming the same notification repeatedly within the prayer minute
        private string _lastNotifiedKey = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string NextPrayerName
        {
            get => _nextPrayerName;
            private set { _nextPrayerName = value; OnPropertyChanged(); }
        }

        public string NextPrayerTime
        {
            get => _nextPrayerTime;
            private set { _nextPrayerTime = value; OnPropertyChanged(); }
        }

        public string NextPrayerCountdown
        {
            get => _nextPrayerCountdown;
            private set { _nextPrayerCountdown = value; OnPropertyChanged(); }
        }

        public string PrayerSummaryBadge
        {
            get => _prayerSummaryBadge;
            private set { _prayerSummaryBadge = value; OnPropertyChanged(); }
        }

        public List<PrayerItem> TodayPrayers
        {
            get => _todayPrayers;
            private set { _todayPrayers = value; OnPropertyChanged(); }
        }

        public PrayerManager()
        {
            CalculateDailyPrayers();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(20)
            };
            _timer.Tick += (s, e) =>
            {
                CalculateDailyPrayers();
                CheckPrayerNotificationTime();
            };
            _timer.Start();
        }

        /// <summary>
        /// Gets city coordinates (Lat, Lon, TimezoneOffset hours)
        /// Supported Indonesian zones: WIB (+7), WITA (+8), WIT (+9)
        /// </summary>
        private (double lat, double lon, double tz) GetCityCoordinates(string cityName)
        {
            string city = cityName.ToLowerInvariant();
            if (city.Contains("surabaya"))
                return (-7.2575, 112.7521, 7.0);
            if (city.Contains("bandung"))
                return (-6.9175, 107.6191, 7.0);
            if (city.Contains("semarang"))
                return (-6.9667, 110.4167, 7.0);
            if (city.Contains("yogyakarta") || city.Contains("jogja"))
                return (-7.7956, 110.3695, 7.0);
            if (city.Contains("medan"))
                return (3.5952, 98.6722, 7.0);
            if (city.Contains("makassar"))
                return (-5.1477, 119.4327, 8.0);
            if (city.Contains("denpasar") || city.Contains("bali"))
                return (-8.6705, 115.2126, 8.0);
            if (city.Contains("jayapura"))
                return (-2.5337, 140.7181, 9.0);

            // Default: Jakarta (WIB)
            return (-6.2088, 106.8456, 7.0);
        }

        public void CalculateDailyPrayers()
        {
            DateTime now = DateTime.Now;

            string selectedCity = SettingsManager.Instance?.PrayerCity ?? "Jakarta (WIB)";
            var (lat, lon, tz) = GetCityCoordinates(selectedCity);

            // Kemenag RI calculation basis (Ephemeris calculation)
            int dayOfYear = now.DayOfYear;
            double b = 2.0 * Math.PI * (dayOfYear - 81) / 365.0;
            double eot = 9.87 * Math.Sin(2 * b) - 7.53 * Math.Cos(b) - 1.5 * Math.Sin(b); // Equation of Time in minutes
            double declination = 23.45 * Math.Sin(2.0 * Math.PI * (284 + dayOfYear) / 365.0) * (Math.PI / 180.0);

            double latRad = lat * (Math.PI / 180.0);
            // Solar noon in local solar time adjusted for standard meridian (tz * 15°)
            double standardMeridian = tz * 15.0;
            double noonHour = 12.0 - (eot / 60.0) + (standardMeridian - lon) / 15.0;

            // Calculation for hour angles (Subuh: 20° Kemenag, Terbit: 0.833°, Isya: 18° Kemenag)
            double CalcHourAngle(double angleDeg)
            {
                double altRad = -angleDeg * (Math.PI / 180.0);
                double cosH = (Math.Sin(altRad) - Math.Sin(latRad) * Math.Sin(declination)) /
                              (Math.Cos(latRad) * Math.Cos(declination));
                cosH = Math.Clamp(cosH, -1.0, 1.0);
                return Math.Acos(cosH) * (180.0 / Math.PI) / 15.0;
            }

            double subuhH = CalcHourAngle(20.0);
            double terbitH = CalcHourAngle(0.833);
            double isyaH = CalcHourAngle(18.0);

            // Ashar calculation: shadow = 1 + tan|lat - decl| (Shafi'i)
            double asharAlt = Math.Atan(1.0 / (1.0 + Math.Tan(Math.Abs(latRad - declination)))) * (180.0 / Math.PI);
            double asharH = CalcHourAngle(-asharAlt);

            TimeSpan subuhTime = TimeSpan.FromHours(noonHour - subuhH);
            TimeSpan terbitTime = TimeSpan.FromHours(noonHour - terbitH);
            TimeSpan dzuhurTime = TimeSpan.FromHours(noonHour + (2.0 / 60.0)); // +2m ihtiyat Kemenag
            TimeSpan asharTime = TimeSpan.FromHours(noonHour + asharH);
            TimeSpan maghribTime = TimeSpan.FromHours(noonHour + terbitH + (3.0 / 60.0)); // sunset +3m ihtiyat
            TimeSpan isyaTime = TimeSpan.FromHours(noonHour + isyaH);

            var list = new List<PrayerItem>
            {
                new() { Name = "Subuh", Time = subuhTime, TimeString = $"{subuhTime.Hours:D2}:{subuhTime.Minutes:D2}" },
                new() { Name = "Terbit", Time = terbitTime, TimeString = $"{terbitTime.Hours:D2}:{terbitTime.Minutes:D2}" },
                new() { Name = "Dzuhur", Time = dzuhurTime, TimeString = $"{dzuhurTime.Hours:D2}:{dzuhurTime.Minutes:D2}" },
                new() { Name = "Ashar", Time = asharTime, TimeString = $"{asharTime.Hours:D2}:{asharTime.Minutes:D2}" },
                new() { Name = "Maghrib", Time = maghribTime, TimeString = $"{maghribTime.Hours:D2}:{maghribTime.Minutes:D2}" },
                new() { Name = "Isya", Time = isyaTime, TimeString = $"{isyaTime.Hours:D2}:{isyaTime.Minutes:D2}" }
            };

            TimeSpan currentTod = now.TimeOfDay;
            PrayerItem? nextItem = null;

            foreach (var item in list)
            {
                if (item.Name == "Terbit") continue; // 5 mandatory prayers
                if (item.Time > currentTod)
                {
                    item.IsNext = true;
                    nextItem = item;
                    break;
                }
                else
                {
                    item.IsPassed = true;
                }
            }

            if (nextItem == null)
            {
                var subuh = list[0];
                subuh.IsNext = true;
                nextItem = subuh;
                TimeSpan remaining = (TimeSpan.FromHours(24) - currentTod) + subuh.Time;
                NextPrayerName = "Subuh";
                NextPrayerTime = subuh.TimeString;
                NextPrayerCountdown = $"{remaining.Hours}j {remaining.Minutes}m";
            }
            else
            {
                TimeSpan diff = nextItem.Time - currentTod;
                NextPrayerName = nextItem.Name;
                NextPrayerTime = nextItem.TimeString;
                NextPrayerCountdown = $"{diff.Hours}j {diff.Minutes}m";
            }

            PrayerSummaryBadge = $"🕌 {NextPrayerName} {NextPrayerTime} ({NextPrayerCountdown})";
            TodayPrayers = list;
        }

        /// <summary>
        /// Checks if current minute matches any of the 5 daily prayer times.
        /// If prayer notification setting is enabled, fires a visual notification banner in Dynamic Island.
        /// STRICT CONSTRAINT: Zero Adzan audio sound (visual banner only + soft feedback click).
        /// </summary>
        private void CheckPrayerNotificationTime()
        {
            var settings = SettingsManager.Instance;
            if (settings == null || !settings.PrayerNotification) return;

            DateTime now = DateTime.Now;
            string todayDate = now.ToString("yyyy-MM-dd");

            foreach (var prayer in _todayPrayers)
            {
                if (prayer.Name == "Terbit") continue;

                // Match exact hour and minute
                if (prayer.Time.Hours == now.Hour && prayer.Time.Minutes == now.Minute)
                {
                    string notificationKey = $"{todayDate}:{prayer.Name}";
                    if (_lastNotifiedKey == notificationKey)
                    {
                        return; // Already notified this prayer today
                    }

                    _lastNotifiedKey = notificationKey;

                    // Trigger Dynamic Island Notification Banner
                    string city = settings.PrayerCity ?? "Jakarta (WIB)";
                    string title = $"🕌 Masuk Waktu {prayer.Name}";
                    string message = $"Telah masuk waktu sholat {prayer.Name} ({prayer.TimeString}) untuk wilayah {city}.";

                    NotificationManager.Instance?.ShowNotification(
                        title,
                        message,
                        "Dynamic Island Prayer",
                        "#10B981" // Elegant emerald green accent
                    );

                    // Note: No adzan audio played as explicitly requested by RIOT.
                    break;
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
