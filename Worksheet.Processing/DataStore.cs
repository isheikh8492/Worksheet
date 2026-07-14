using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;
using Worksheet.Core.Models.Gates;
using ViewportModel = Worksheet.Core.Models.Viewport;

namespace Worksheet.Processing
{
    public class DataStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, PlotSettings> _settings = new();
        private readonly Dictionary<Guid, ProcessedPlotData> _processed = new();
        private readonly Dictionary<Guid, RenderTargetSize> _renderTargetSizes = new();
        private readonly Dictionary<Guid, GateSettings> _gates = new();
        private readonly Dictionary<Guid, GateResult> _gateResults = new();
        private readonly ViewportModel _viewport = new();

        public ViewportModel Viewport
        {
            get
            {
                lock (_lock)
                {
                    return _viewport;
                }
            }
        }

        public void UpsertSettings(PlotSettings settings)
        {
            lock (_lock)
            {
                _settings[settings.Id] = settings;
                if (!_viewport.PlotIds.Contains(settings.Id))
                    _viewport.PlotIds.Add(settings.Id);
            }
        }

        public bool TryGetSettings(Guid plotId, out PlotSettings settings)
        {
            lock (_lock)
            {
                return _settings.TryGetValue(plotId, out settings!);
            }
        }

        public IReadOnlyList<PlotSettings> GetAllSettings()
        {
            lock (_lock)
            {
                return _settings.Values.ToList();
            }
        }

        public void RemovePlot(Guid plotId)
        {
            lock (_lock)
            {
                _settings.Remove(plotId);
                _processed.Remove(plotId);
                _renderTargetSizes.Remove(plotId);
                _viewport.PlotIds.Remove(plotId);
            }
        }

        public void SetRenderTargetSize(Guid plotId, RenderTargetSize size)
        {
            lock (_lock)
            {
                _renderTargetSizes[plotId] = size;
            }
        }

        public RenderTargetSize GetRenderTargetSize(Guid plotId)
        {
            lock (_lock)
            {
                return _renderTargetSizes.TryGetValue(plotId, out var size)
                    ? size
                    : RenderTargetSize.Empty;
            }
        }

        public void SetProcessedData(ProcessedPlotData data)
        {
            lock (_lock)
            {
                _processed[data.PlotId] = data;
            }
        }

        public bool TryGetProcessedData(Guid plotId, out ProcessedPlotData data)
        {
            lock (_lock)
            {
                return _processed.TryGetValue(plotId, out data!);
            }
        }

        public void UpsertGate(GateSettings gate)
        {
            lock (_lock)
            {
                if (_gates.TryGetValue(gate.GateId, out var existing))
                {
                    // Preserve stable metadata like name/created time once assigned.
                    _gates[gate.GateId] = new GateSettings
                    {
                        GateId = existing.GateId,
                        Name = existing.Name,
                        CreatedUtc = existing.CreatedUtc,
                        UpdatedUtc = gate.UpdatedUtc,
                        Plot = gate.Plot,
                        GateType = gate.GateType,
                        Geometry = gate.Geometry,
                    };
                    return;
                }

                string name = string.IsNullOrWhiteSpace(gate.Name) ? GenerateNextGateName(_gates.Values.Select(g => g.Name)) : gate.Name;
                _gates[gate.GateId] = new GateSettings
                {
                    GateId = gate.GateId,
                    Name = name,
                    CreatedUtc = gate.CreatedUtc == default ? DateTime.UtcNow : gate.CreatedUtc,
                    UpdatedUtc = gate.UpdatedUtc == default ? DateTime.UtcNow : gate.UpdatedUtc,
                    Plot = gate.Plot,
                    GateType = gate.GateType,
                    Geometry = gate.Geometry,
                };
            }
        }

        public void RemoveGate(Guid gateId)
        {
            lock (_lock)
            {
                _gates.Remove(gateId);
                _gateResults.Remove(gateId);
            }
        }

        public IReadOnlyList<GateSettings> GetAllGates()
        {
            lock (_lock)
            {
                return _gates.Values.ToList();
            }
        }

        public void SetGateResult(GateResult result)
        {
            lock (_lock)
            {
                _gateResults[result.GateId] = result;
            }
        }

        public bool TryGetGateResult(Guid gateId, out GateResult result)
        {
            lock (_lock)
            {
                return _gateResults.TryGetValue(gateId, out result!);
            }
        }

        private static string GenerateNextGateName(IEnumerable<string> existingNames)
        {
            int maxIndex = -1;
            foreach (var raw in existingNames)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                // Keep only alphabetic chars.
                var letters = new string(raw.Where(char.IsLetter).ToArray()).ToUpperInvariant();
                if (letters.Length == 0)
                    continue;

                if (TryParseExcelLabelToIndex(letters, out int idx))
                    maxIndex = Math.Max(maxIndex, idx);
            }

            int nextIndex = maxIndex + 1;
            return ExcelIndexToLabel(nextIndex);
        }

        private static bool TryParseExcelLabelToIndex(string letters, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(letters))
                return false;

            int num = 0;
            foreach (char c in letters)
            {
                if (c < 'A' || c > 'Z')
                    return false;

                int v = (c - 'A') + 1; // A=1..Z=26
                num = checked(num * 26 + v);
            }

            index = num - 1; // 0-based
            return index >= 0;
        }

        private static string ExcelIndexToLabel(int index)
        {
            if (index < 0)
                index = 0;

            int num = index + 1; // 1-based
            string letters = "";
            while (num > 0)
            {
                num--; // shift to 0-based remainder
                int rem = num % 26;
                letters = (char)('A' + rem) + letters;
                num /= 26;
            }
            return letters;
        }
    }
}
