using System;
using System.Collections.Generic;
using System.Globalization;

namespace Worksheet.Core.Models.Gates
{
    public readonly record struct GateAxisStatistics(double Mean, double Std, double Var, double CvPercent);

    public sealed class GateStatistics
    {
        public double Percent { get; init; }
        public int Total { get; init; }
        public GateAxisStatistics? X { get; init; }
        public GateAxisStatistics? Y { get; init; }

        public Dictionary<string, string> ToDisplayDictionary()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Num"] = $"{Math.Round(Percent, 1).ToString("0.0", CultureInfo.InvariantCulture)} %",
                ["Total"] = Total.ToString(CultureInfo.InvariantCulture),
            };

            if (X == null && Y == null)
            {
                dict["CV"] = "0.0";
                dict["Mean"] = "0.0";
                dict["STD"] = "0.0";
                dict["Var"] = "0.0";
                return dict;
            }

            if (X != null && Y == null)
            {
                dict["CV"] = Math.Round(X.Value.CvPercent, 1).ToString("0.0", CultureInfo.InvariantCulture);
                dict["Mean"] = Math.Round(X.Value.Mean, 2).ToString("0.00", CultureInfo.InvariantCulture);
                dict["STD"] = Math.Round(X.Value.Std, 2).ToString("0.00", CultureInfo.InvariantCulture);
                dict["Var"] = Math.Round(X.Value.Var, 2).ToString("0.00", CultureInfo.InvariantCulture);
                return dict;
            }

            if (X != null && Y != null)
            {
                dict["CV"] = $"({Math.Round(X.Value.CvPercent, 1).ToString("0.0", CultureInfo.InvariantCulture)}, {Math.Round(Y.Value.CvPercent, 1).ToString("0.0", CultureInfo.InvariantCulture)})";
                dict["Mean"] = $"({Math.Round(X.Value.Mean, 2).ToString("0.00", CultureInfo.InvariantCulture)}, {Math.Round(Y.Value.Mean, 2).ToString("0.00", CultureInfo.InvariantCulture)})";
                dict["STD"] = $"({Math.Round(X.Value.Std, 2).ToString("0.00", CultureInfo.InvariantCulture)}, {Math.Round(Y.Value.Std, 2).ToString("0.00", CultureInfo.InvariantCulture)})";
                dict["Var"] = $"({Math.Round(X.Value.Var, 2).ToString("0.00", CultureInfo.InvariantCulture)}, {Math.Round(Y.Value.Var, 2).ToString("0.00", CultureInfo.InvariantCulture)})";
                return dict;
            }

            dict["CV"] = "0.0";
            dict["Mean"] = "0.0";
            dict["STD"] = "0.0";
            dict["Var"] = "0.0";
            return dict;
        }
    }
}
