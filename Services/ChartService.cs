using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Colors = Microsoft.UI.Colors;

namespace English_Listen_WinUI.Services
{
    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public Color Color { get; set; }
    }

    public class ChartService
    {
        private static readonly Color[] DefaultColors = 
        {
            Colors.Blue,
            Colors.Green,
            Colors.Orange,
            Colors.Purple,
            Colors.Red,
            Colors.Cyan,
            Colors.Magenta,
            Colors.Yellow
        };

        public static List<ChartDataPoint> GenerateAccuracyTrendData(List<Models.TestResult> history, int maxPoints = 10)
        {
            var data = new List<ChartDataPoint>();
            var recentHistory = history
                .OrderByDescending(h => h.Timestamp)
                .Take(maxPoints)
                .Reverse()
                .ToList();

            for (int i = 0; i < recentHistory.Count; i++)
            {
                data.Add(new ChartDataPoint
                {
                    Label = recentHistory[i].Timestamp.ToString("MM/dd"),
                    Value = recentHistory[i].Accuracy,
                    Color = DefaultColors[i % DefaultColors.Length]
                });
            }

            return data;
        }

        public static List<ChartDataPoint> GenerateWordListPerformanceData(List<Models.TestResult> history)
        {
            var grouped = history
                .GroupBy(h => h.WordListName)
                .Select(g => new 
                { 
                    WordListName = g.Key, 
                    AvgAccuracy = g.Average(h => h.Accuracy),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.AvgAccuracy)
                .Take(5)
                .ToList();

            var data = new List<ChartDataPoint>();
            for (int i = 0; i < grouped.Count; i++)
            {
                data.Add(new ChartDataPoint
                {
                    Label = grouped[i].WordListName,
                    Value = grouped[i].AvgAccuracy,
                    Color = DefaultColors[i % DefaultColors.Length]
                });
            }

            return data;
        }
    }
}