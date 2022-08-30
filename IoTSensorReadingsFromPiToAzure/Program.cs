using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;
using System.Device.I2c;
using System.Text;

namespace IoTSensorReadingsFromPiToAzure
{
    

    public class Program
    {
        private static IConfigurationRoot _configuration;
        private static DeviceClient _deviceClient;
        private static string _deviceConnectionString = "";
        private static int _telemetryReadForSeconds = 30;

        public static async Task Main(string[] args)
        {
            BuildOptions();

            await ReadSensorData();

            Console.WriteLine("Program Completed");
        }

        private static void BuildOptions()
        {
            _configuration = ConfigurationBuilderSingleton.ConfigurationRoot;
        }

        private static async Task ReadSensorData()
        {
            Console.WriteLine("Would you like to output each telemetry reading to the console [y/n]?");
            var shouldShowIndivudualTelemetry = Console.ReadLine()?.StartsWith("y", StringComparison.OrdinalIgnoreCase) ?? false;
        
            //get device connection string
            _deviceConnectionString = _configuration["Device:AzureConnectionString"];
            //get configured read duration [default/min => 30 seconds]
            var duration = _configuration["Device:TelemetryReadDurationInSeconds"];
            if (!string.IsNullOrWhiteSpace(duration))
            {
                int.TryParse(duration, out int readDurationSeconds);
                if (readDurationSeconds > 30)
                {
                    _telemetryReadForSeconds = readDurationSeconds;
                }   
            }

            //set up the device client
            _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);

            var endReadingsAtTime = DateTime.Now.AddSeconds(_telemetryReadForSeconds);

            //utilize the library to read Bme280 data
            var i2cSettings = new I2cConnectionSettings(1, Bme280.SecondaryI2cAddress);
            using I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
            using var bme280 = new Bme280(i2cDevice);

            //device readings created by python script execution on the device:
            int measurementTime = bme280.GetMeasurementDuration();
            var command = "python";
            var script = @"~/enviro/enviroplus-python/examples/singlelight.py"; 
            var args = $"{script}"; 

            while(DateTime.Now < endReadingsAtTime)
            {
                bme280.SetPowerMode(Bmx280PowerMode.Forced);
                Thread.Sleep(measurementTime);

                bme280.TryReadTemperature(out var tempValue);
                bme280.TryReadPressure(out var preValue);
                bme280.TryReadHumidity(out var humValue);
                bme280.TryReadAltitude(out var altValue);

                var envData = new EnviroSensorData();

                envData.Temperature = $"{tempValue.DegreesCelsius:0.#}\u00B0C";
                envData.Humidity = $"{humValue.Percent:#.##}%";
                envData.Pressure = $"{preValue.Hectopascals:#.##} hPa";
                envData.Altitude = $"{altValue.Meters:#} m";

                string lightProx = string.Empty;

                using (Process process = new Process())
                {

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = command;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    StreamReader sr = process.StandardOutput;
                    lightProx = sr.ReadToEnd();
                    process.WaitForExit();
                }

                var result = lightProx.Split('\'');
                envData.Light = result[3];
                envData.Proximity = result[7];

                
                if(shouldShowIndivudualTelemetry)
                {
                    Console.WriteLine(new string('*', 80));
                    Console.WriteLine("* Telemetry Data: ");
                    Console.WriteLine(envData);
                    Console.WriteLine(new string('*', 80));
                }

                var telemetryObject = new BME280PlusLTR559(envData.Temperature, envData.Pressure, 
                                                            envData.Humidity, envData.Altitude, 
                                                            envData.Light, envData.Proximity);

                var telemetryMessage = telemetryObject.ToJson();

                var msg = new Message(Encoding.ASCII.GetBytes(telemetryMessage));

                //send the telemetry to azure
                await _deviceClient.SendEventAsync(msg);

                Console.WriteLine($"Telemetry sent {DateTime.Now.ToShortTimeString()}");
                Thread.Sleep(500);
            }

            Console.WriteLine("All telemetry read");
        }
    }
}
