# Coding Standards

Repository-wide coding standards for the `Worksheet` solution. `Worksheet.Core`, `Worksheet.Chasm`, and `Worksheet.Processing` target `net8.0` (no UI); `Worksheet.App` targets `net8.0-windows` (WPF, ScottPlot 5).

## Core Principles

- Keep implementations small, explicit, and easy to reason about.
- Fix root causes instead of layering temporary workarounds.
- Preserve behavior unless the task explicitly changes behavior.
- Prefer deterministic, testable code over implicit side effects.

## Solution Structure

Five projects, layered with a strict one-way dependency direction. Each project's root namespace equals its assembly name, and sub-namespaces mirror the folder tree.

- **`Worksheet.Core`** (`net8.0`, no dependencies) — the pure leaf: domain models and DTOs (`Models/`, `Models/Data/`, `Models/Gates/`), the shared data-buffer contracts and shapes (`Buffers/`: `IChannelDataBuffer`, `IOscilloscopeBuffer`, the window snapshots, `AnalogCapture`), and cross-cutting services (`Services/`: `AppLog`, channel/feature configuration).
- **`Worksheet.Chasm`** (`net8.0` → Core) — the ingestion runtime: producers, `ChasmEngine`, the consumer, the `DataSource` ring buffer, `ChasmDataSource`, the oscilloscope buffer, and event batches.
- **`Worksheet.Processing`** (`net8.0` → Core) — the viewport engine: `PlotProcessor`, `ProcessingEngine`, plot pipelines, `GateProcessor`. Reads data only through Core's `IChannelDataBuffer` port — it never references `Worksheet.Chasm`.
- **`Worksheet.App`** (`net8.0-windows`, WPF → Core, Chasm, Processing) — UI and composition root: views, plot views, context menus, dialogs, interaction wiring, and the WPF-thread engines (`RenderingEngine`, `ViewportSession`).
- **`Worksheet.Tests`** → all.

Dependency direction: `Core ← Chasm`, `Core ← Processing`, `Core, Chasm, Processing ← App`. `Core` depends on nothing; `Chasm` and `Processing` are sibling adapters that meet only through Core's ports (composed in `App`). Keep the graph acyclic.

Do not move a type across a project boundary, or add a project reference, without a clear reason — in particular, never introduce a `Chasm ↔ Processing` dependency (route through a Core port instead).

## C# Conventions

- Nullable reference types are enabled. Respect nullability annotations.
- Use explicit names (`plotSettings`, `visibleLength`) over vague names (`data`, `tmp`).
- Keep methods focused. Extract private helpers when branching grows.
- Avoid expensive work in property getters.
- Prefer immutable local values in hot paths.

## WPF and UI Rules

- Keep code-behind UI-focused. Processing belongs in `Services/`.
- Avoid repeated layout literals. Promote reused values to constants/resources.
- Reuse existing plottables during updates where possible.
- Avoid unnecessary visual-tree complexity in frequently refreshing views.

## Plot and Performance Rules

The viewport path is performance-sensitive:

- Avoid per-frame allocations in `PlotProcessor`, `GateProcessor`, and plot view `Render()` code.
- Reuse buffers and plottables when practical.
- Keep hot loops simple; avoid avoidable expensive math in tight loops.
- Do not add logging in hot loops.
- Consider scaling with `BinCount`, channel count, and gate count.

## Logging and Diagnostics

- Use `AppLog` for meaningful exceptions/events.
- Temporary diagnostics must be clearly scoped and removed before completion unless explicitly requested.
- Do not leave commented-out debug code.

## Dependencies

- Keep dependencies minimal.
- If a package is added or updated, include rationale in the task/PR summary.

## Validation Expectations

For regular code changes, run at least:

- `dotnet build .\\Worksheet.sln -c Release`

For processing/rendering behavior changes, include a brief manual verification note.

## Completion Checklist

A change is not complete if it leaves:

- dead or duplicated code introduced by the task
- temporary instrumentation
- avoidable performance regressions in hot paths
- unclear naming or hidden side effects
