using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UiPath.CodedWorkflows;

namespace AfternoonWalk
{
    /// <summary>
    /// Sends a Voice Monkey announcement (POST https://api-v3.voicemonkey.io/announce).
    /// The Voice Monkey API token is read from an Orchestrator Credential asset (password field),
    /// and the device ID and the fixed speech text from Text assets.
    /// </summary>
    public class AnnounceOnAlexa : CodedWorkflow
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        [Workflow]
        public void Execute(string VoiceMonkeyCredentialAsset, string VoiceMonkeyDeviceAsset, string AssetFolder, string VoiceMonkeySpeechAsset)
        {
            var folder = string.IsNullOrWhiteSpace(AssetFolder) ? null : AssetFolder;
            var Device = Convert.ToString(system.GetAsset(VoiceMonkeyDeviceAsset, folder))?.Trim();
            if (string.IsNullOrWhiteSpace(Device))
            {
                Log($"Asset '{VoiceMonkeyDeviceAsset}' is empty; skipping the announcement.", LogLevel.Warn);
                return;
            }

            var Speech = Convert.ToString(system.GetAsset(VoiceMonkeySpeechAsset, folder))?.Trim();
            if (string.IsNullOrWhiteSpace(Speech))
            {
                Log($"Asset '{VoiceMonkeySpeechAsset}' is empty; skipping the announcement.", LogLevel.Warn);
                return;
            }

            var (_, tokenSecure) = system.GetCredential(VoiceMonkeyCredentialAsset, folder);
            var token = new NetworkCredential(string.Empty, tokenSecure).Password;

            var payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["device"] = Device,
                ["speech"] = Speech,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api-v3.voicemonkey.io/announce")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = Http.SendAsync(request).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Voice Monkey announce failed with {(int)response.StatusCode}: {content}");
            }
            Log($"Voice Monkey announcement sent to '{Device}'.");
        }
    }
}
