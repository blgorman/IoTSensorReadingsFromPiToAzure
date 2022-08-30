﻿namespace IoTSensorReadingsFromPiToAzure
{
    public class EnviroSensorData
    {
        public string Temperature { get; set; }
        public string Pressure { get; set; }
        public string Humidity { get; set; }

        public string Altitude { get; set; }
        public string Light { get; set; }
        public string Proximity { get; set; }

        public override string ToString()
        {
            return $"Temperature: {Temperature}\n" +
                    $"Pressure: {Pressure}\n" +
                    $"Relative humidity: {Humidity}" +
                    $"Estimated altitude: {Altitude}" +
                    $"Light: {Light} lux" +
                    $"Proximity: {Proximity}";
        }
    }
}
