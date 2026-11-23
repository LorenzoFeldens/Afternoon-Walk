using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using UiPath.CodedWorkflows;

namespace AfternoonWalk
{
    /// <summary>
    /// Port of the n8n "Afternoon Walk" checks. Decides whether a walk notification should go out now:
    ///  1. not already sent today (state file holds the last sent date), and inside the StartHour window,
    ///  2. current weather is clear (OpenWeather condition id >= 800),
    ///  3. feels-like temperature below the threshold, or this is the final check (at/after EndHour).
    /// Uses a single OpenWeather call (free 2.5 "current weather" endpoint).
    /// Settings come from Orchestrator Text assets (names below); the OpenWeather API key from a
    /// Credential asset (password field).
    /// </summary>
    public class CheckWalkConditions : CodedWorkflow
    {
        // Orchestrator asset names for the changeable settings.
        private const string LatitudeAsset = "Latitude";
        private const string LongitudeAsset = "Longitude";
        private const string TemperatureAsset = "Temperature";   // feels-like threshold in °C
        private const string StartHourAsset = "StartHour";       // e.g. 18:00
        private const string EndHourAsset = "EndHour";           // e.g. 20:00 (final check)

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        [Workflow]
        public (bool ShouldNotify, string Subject, string Body, string Today, string Recipient, string FeelsLike) Execute(
            string OpenWeatherCredentialAsset,
            string RecipientAsset,
            string AssetFolder,
            string TimeZoneId,
            string StateFilePath)
        {
            folder = string.IsNullOrWhiteSpace(AssetFolder) ? null : AssetFolder;

            var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            var today = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // 1. Already notified today? Stop.
            var stateDir = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrEmpty(stateDir)) Directory.CreateDirectory(stateDir);
            if (File.Exists(StateFilePath) && File.ReadAllText(StateFilePath).Trim() == today)
            {
                Log($"Notification already sent today ({today}).");
                return (false, string.Empty, string.Empty, today, string.Empty, string.Empty);
            }

            var startTime = AssetTime(StartHourAsset);
            var endTime = AssetTime(EndHourAsset);
            var endText = endTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            if (localNow.TimeOfDay < startTime)
            {
                Log($"Before {startTime:hh\\:mm}; nothing to check yet.");
                return (false, string.Empty, string.Empty, today, string.Empty, string.Empty);
            }

            var threshold = AssetNumber(TemperatureAsset);
            var lat = AssetNumber(LatitudeAsset).ToString(CultureInfo.InvariantCulture);
            var lon = AssetNumber(LongitudeAsset).ToString(CultureInfo.InvariantCulture);

            var (_, apiKeySecure) = system.GetCredential(OpenWeatherCredentialAsset, folder);
            var apiKey = new NetworkCredential(string.Empty, apiKeySecure).Password;
            var key = Uri.EscapeDataString(apiKey);

            // 2. Current conditions.
            using var current = GetJson(
                $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&units=metric&lang=pt&appid={key}");
            var feelsLike = current.RootElement.GetProperty("main").GetProperty("feels_like").GetDouble();
            var weatherId = current.RootElement.GetProperty("weather")[0].GetProperty("id").GetInt32();
            var temp = feelsLike.ToString("0.#", CultureInfo.InvariantCulture);
            var limit = threshold.ToString("0.#", CultureInfo.InvariantCulture);
            Log($"Now: feels like {temp}°C, condition id {weatherId}.");

            if (weatherId < 800)
            {
                Log("Current weather is not clear; no notification.");
                return (false, string.Empty, string.Empty, today, string.Empty, string.Empty);
            }

            // 3. Temperature rule, or the final check of the day.
            string subject;
            string body;
            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            var tempBr = feelsLike.ToString("0.#", ptBr);
            var limitBr = threshold.ToString("0.#", ptBr);
            if (feelsLike < threshold)
            {
                subject = $"Sensação térmica de {tempBr}°C (abaixo de {limitBr}°C)";
                body = $"Agora a sensação térmica é de {tempBr}°C, abaixo do seu limite de {limitBr}°C. O tempo está aberto: bom momento para o passeio!";
            }
            else if (localNow.TimeOfDay >= endTime)
            {
                subject = $"Ainda {tempBr}°C às {endText}";
                body = $"A sensação térmica não ficou abaixo de {limitBr}°C até as {endText}. Atual: {tempBr}°C. O tempo está aberto.";
            }
            else
            {
                Log($"Still at or above {limit}°C and before {endText}; keep waiting.");
                return (false, string.Empty, string.Empty, today, string.Empty, string.Empty);
            }

            Log($"All checks passed: {subject}");
            var recipient = AssetText(RecipientAsset);
            return (true, subject, body, today, recipient, tempBr);
        }

        // Orchestrator folder holding the assets; null = the folder the job runs in.
        private string folder;

        private string AssetText(string name)
        {
            var text = Convert.ToString(system.GetAsset(name, folder), CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException($"Orchestrator asset '{name}' is empty.");
            return text;
        }

        // Accepts "26", "26.5" or "26,5" (and the same for coordinates), whatever the robot's locale.
        private double AssetNumber(string name)
        {
            var text = AssetText(name).Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException($"Orchestrator asset '{name}' must be a number, got '{text}'.");
            return value;
        }

        // Accepts "18:00", "18:30" or a bare hour like "18".
        private TimeSpan AssetTime(string name)
        {
            var text = AssetText(name);
            if (!text.Contains(':') && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
                return TimeSpan.FromHours(hour);
            if (!TimeSpan.TryParseExact(text, new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out var time))
                throw new InvalidOperationException($"Orchestrator asset '{name}' must be a time like 18:00, got '{text}'.");
            return time;
        }

        private static JsonDocument GetJson(string url)
        {
            using var response = Http.GetAsync(url).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                var safeUrl = url.Substring(0, url.IndexOf('?'));
                throw new HttpRequestException($"OpenWeather request to {safeUrl} failed with {(int)response.StatusCode}: {content}");
            }
            return JsonDocument.Parse(content);
        }
    }
}
