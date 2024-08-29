using System;
using System.Collections.Generic; // Include this namespace
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;

public class ESPHomeReader : MonoBehaviour
{
    private static readonly HttpClient client = new HttpClient();
    private const string espHomeUrl = "http://192.168.0.109/events"; // Replace with your ESPHome server URL

    public Renderer sphereRenderer; // Reference to the Renderer component of the sphere
    public TextMeshPro sensorDataText; // Reference to the TextMeshPro component (3D TextMeshPro)

    // Dictionary to store the latest value for each sensor
    private Dictionary<string, string> sensorData = new Dictionary<string, string>();

    // Dictionary to map sensor IDs to formatted labels
    private Dictionary<string, (string label, string unit)> sensorLabels = new Dictionary<string, (string label, string unit)>
    {
        { "sensor-co_", ("eCO2", "ppm") },
        { "sensor-formaldehyde", ("eCHO2", "ug/m3") },
        { "sensor-tvoc", ("TVOC", "ug/m3") },
        { "sensor-pm2_5", ("PM2.5", "ug/m3") },
        { "sensor-pm10", ("PM10", "ug/m3") },
        { "sensor-temperature", ("Temp", "C") },
        { "sensor-humidity", ("Humt", "%RH") }
    };

    async void Start()
    {
        if (sphereRenderer == null)
        {
            Debug.LogError("Sphere Renderer is not assigned!");
            return;
        }

        if (sensorDataText == null)
        {
            Debug.LogError("Sensor Data Text is not assigned!");
            return;
        }

        // Set the font size to 14
        sensorDataText.fontSize = 14;

        await ReadEventsFromESPHome(espHomeUrl);
    }

    private async Task ReadEventsFromESPHome(string url)
    {
        try
        {
            using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new System.IO.StreamReader(stream))
                {
                    while (true)
                    {
                        string line = await reader.ReadLineAsync();
                        if (line == null) break; // Exit the loop if no more data

                        // Filter lines starting with "event: state" and "data:" for JSON content
                        if (line.StartsWith("event: state"))
                        {
                            string dataLine = await reader.ReadLineAsync();
                            if (dataLine != null && dataLine.StartsWith("data:"))
                            {
                                string jsonData = dataLine.Substring(5).Trim();

                                // Process and store data
                                ProcessAndStoreData(jsonData);

                                // Update the TextMeshPro display
                                UpdateDisplay();
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reading from ESPHome: {e.Message}");
        }
    }

    private void ProcessAndStoreData(string jsonData)
    {
        try
        {
            var id = ExtractValue(jsonData, "id");
            var valueStr = ExtractValue(jsonData, "value");

            // Store the latest value for the sensor
            if (sensorLabels.ContainsKey(id))
            {
                sensorData[id] = valueStr;

                // If the data is for the CO2 sensor, change the color
                if (id == "sensor-co_" && float.TryParse(valueStr, out float coValue))
                {
                    ChangeColorBasedOnCO(coValue);
                }
            }

            Debug.Log($"State Data - ID: {id}, Value: {valueStr}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing JSON data: {e.Message}");
        }
    }

    private void UpdateDisplay()
    {
        sensorDataText.text = string.Empty;
        foreach (var entry in sensorData)
        {
            if (sensorLabels.ContainsKey(entry.Key))
            {
                var (label, unit) = sensorLabels[entry.Key];
                sensorDataText.text += $"{label}: {entry.Value} {unit}\n";
            }
        }
    }

    private void ChangeColorBasedOnCO(float level)
    {
        if (level < 410)
        {
            sphereRenderer.material.color = Color.blue;
        }
        else
        {
            sphereRenderer.material.color = Color.red;
        }
    }

    private string ExtractValue(string jsonData, string key)
    {
        string pattern = $"\"{key}\":\"?(?<value>[^\"]*?)\"?(,|}})";
        var match = Regex.Match(jsonData, pattern);
        if (match.Success)
        {
            return match.Groups["value"].Value;
        }
        return "Not Found";
    }
}
