# Plot Processing and Rendering Audit

This audit covers the current processing/rendering path for:

- `Histogram`
- `Pseudocolor`
- `SpectralRibbon`

## Current Pipeline

1. `ProcessingEngine.Tick()` invokes plot processing when plot settings, target size, or `DataVersion` changes.
2. `DataStore` holds the latest `ProcessedPlotData` per plot.
3. `RenderingEngine.Tick()` compares object reference and renders when data changed.
4. Pseudocolor and spectral ribbon views present precomputed pixel buffers through `DynamicBitmap`.
5. ScottPlot still owns axes, labels, ticks, borders, gate overlays, and static plot chrome.

## Findings (by impact)

### 1) DataSource snapshots are live ring-buffer views (high)

`DataSource.GetSnapshot(...)` returns references to the internal retained channel arrays. The metadata is captured under lock, but the arrays remain live after the lock is released.

- `Worksheet.Chasm/DataSource.cs`
- `Worksheet.Core/Buffers/ChannelWindowSnapshot.cs`
- `Worksheet.Core/Buffers/MultiChannelWindowSnapshot.cs`

Impact:

- Fast and allocation-free snapshots.
- Processing can observe partially newer values if ingestion writes while a processor is scanning a snapshot.
- This is a deliberate performance tradeoff, but it should be treated as a weak snapshot contract.
- `GetSnapshotCopy(...)` is available when a stable contiguous snapshot is needed, but it allocates and copies selected columns.

Measured copy cost from the current profile test run:

```text
1 selected signal:       1.60 ms for 20 copied snapshots
2 selected signals:   0.63-0.76 ms for 20 copied snapshots
42 selected signals: 56.95-59.40 ms for 20 copied snapshots
```

Use live snapshots for hot plot processors by default. Use copied snapshots only where correctness or thread-boundary isolation matters more than copy cost.

### 2) Processing is incremental, but still scheduled per data version (medium)

`ProcessingEngine` still checks every active plot when `DataVersion` changes, but `PlotProcessor` maintains per-plot incremental state:

- histogram ring-bin state
- pseudocolor packed-bin state
- spectral ribbon row state

Files:

- `Worksheet.Processing/ProcessingEngine.cs`
- `Worksheet.Processing/PlotProcessor.cs`

Impact:

- The scheduling cost still scales with active plot count.
- The compute cost is usually delta-based instead of full-window-based.
- Sequence gaps or settings changes still force full rebuilds.

### 3) Pseudocolor and spectral buffers are retained in processor state (medium)

The old design allocated more transient heatmap state. Current `PlotProcessor` keeps reusable state objects:

- `RawCounts`
- `Normalized`
- `PixelBuffer`
- event-contribution rings

File:

- `Worksheet.Processing/PlotProcessor.cs`

Impact:

- Better allocation behavior than the older heatmap path.
- Work still scales with bin count, pixel size, and selected channel count.
- Large target sizes increase pixel-buffer render-preparation cost.

### 4) Rendering dispatch is coalesced asynchronously (medium)

`RenderingEngine` queues pending renders and schedules a single UI-thread render pass through `Dispatcher.BeginInvoke`.

- `Worksheet.App/Services/Viewport/RenderingEngine.cs`

Impact:

- Better than per-target synchronous dispatch.
- Rendering still runs on the UI thread.
- Large numbers of plots can still saturate the UI thread.

### 5) Static ScottPlot refresh still happens for axis/config changes (low)

Pseudocolor and spectral data pixels are presented through `DynamicBitmap`, but static axis/tick/label changes still call `plot.Refresh()`.

- `Worksheet.App/Views/PlotViews/PlotView.cs`
- `Worksheet.App/Views/PlotViews/PseudocolorPlotView.cs`
- `Worksheet.App/Views/PlotViews/SpectralRibbonPlotView.cs`

Impact:

- Normal data frames avoid ScottPlot heatmap update cost.
- Feature/axis/bin changes still refresh ScottPlot chrome.

### 6) Gate processing is separate incremental state (medium)

Gate processing has its own processor and cached state. It is still keyed by gate geometry, plot settings, and data version.

- `Worksheet.Processing/ProcessingEngine.cs`
- `Worksheet.Processing/Gates/GateProcessor.cs`

Impact:

- Gate work is separated from plot rendering.
- Complex gates and many active gates can still add measurable compute load.

### 7) Runtime metrics can be reset, but are simple averages (low)

Metrics can be reset from the app/session surface, but they are still cumulative averages after reset rather than rolling-window percentiles.

- `Worksheet.Processing/ProcessingEngine.cs`
- `Worksheet.App/Services/Viewport/RenderingEngine.cs`
- `Worksheet.App/Services/Viewport/ViewportSession.cs`

Impact:

- Useful for quick A/B checks.
- Not enough for detailed latency distribution analysis.

## Current Bitmap Path

The app is already using the high-control bitmap path for pseudocolor and spectral ribbon data:

```text
PlotProcessor
  -> RawCounts / Normalized
  -> PixelBuffer
  -> HeatmapProcessedData or SpectralRibbonProcessedData
  -> PlotView.Render(...)
  -> DynamicBitmap.PresentBitmap(...)
```

ScottPlot remains useful for axes, ticks, labels, borders, and static interaction surfaces. The high-frequency data layer is a WPF image aligned to ScottPlot's data rectangle.

This split is the right direction for high-throughput multi-plot display.

## Recommended Next Steps (ordered)

1. Keep the current split snapshot contract: live snapshots for hot processors, copied snapshots for isolation-sensitive paths.
2. Move `FeatureSelectionStrategy` away from static global state if multiple independent sessions or channel maps become important.
3. Add rolling or percentile latency metrics for processing/rendering, not only averages.
4. Profile large active-plot counts with live ingestion to quantify UI-thread saturation.
5. Split tests into `Worksheet.Core.Tests` and `Worksheet.App.Tests` if WPF test setup starts slowing Core-only validation.
