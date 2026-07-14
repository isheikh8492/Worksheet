# AI Agent Policy

This document defines how AI agents should operate in this repository.

## Required Reading

Before making edits, read:

1. `docs/AI_AGENT_POLICY.md`
2. `docs/CODING_STANDARDS.md`

If either document conflicts with direct user instructions, user instructions win.

## Repository Context

- Stack: `C#`, `WPF`, `ScottPlot.WPF` (v5), `MathNet.Numerics`
- Target frameworks: `net8.0` for `Worksheet.Core`/`Worksheet.Chasm`/`Worksheet.Processing`; `net8.0-windows` for `Worksheet.App`
- Projects (strict one-way deps; see `CODING_STANDARDS.md` for detail):
  - `Worksheet.Core` — models, DTOs, buffer contracts (`Buffers/`), core services; no dependencies
  - `Worksheet.Chasm` — ingestion runtime (producers, `ChasmEngine`, `DataSource`) → Core
  - `Worksheet.Processing` — viewport engine (processors, pipelines, `GateProcessor`) → Core
  - `Worksheet.App` — WPF UI + composition root → Core, Chasm, Processing
  - `Worksheet.Tests` → all

## Agent Behavior Rules

- Make focused, minimal changes that solve the user request.
- Preserve existing architecture unless explicitly asked to restructure.
- Do not add new top-level folders or patterns casually.
- Prefer extending existing abstractions over creating parallel ones.
- Name new types by responsibility, not generic utility labels.

## Performance-Sensitive Areas

Treat these as hot paths:

- `Worksheet.Processing/PlotProcessor.cs`
- `Worksheet.Processing/Gates/GateProcessor.cs`
- `Worksheet.App/Views/PlotViews/*` render/update code
- timer-driven engines: `Worksheet.Processing/ProcessingEngine.cs`, `Worksheet.App/Services/Viewport/RenderingEngine.cs`

In these areas:

- avoid unnecessary allocations
- avoid debug logging in loops
- avoid repeated subscriptions/unsubscriptions in refresh cycles

## Diagnostics Policy

- Use `AppLog` for durable, meaningful diagnostics.
- Temporary instrumentation must be explicitly marked and removed before completion unless the user asks to keep it.
- Do not leave commented-out code.

## Validation Policy

After code edits, run (unless user says not to):

- `dotnet build .\\Worksheet.sln -c Release`

If behavior changes are visual or interaction-based, include what was manually verified.

## File Hygiene

- Do not edit generated outputs in `bin/` or `obj/`.
- Do not commit transient logs or temporary artifacts created only for debugging.
- Do not remove or alter user-owned files unrelated to the task.

## Completion Standard

Work is complete when:

- requested behavior is implemented
- no temporary scaffolding is left behind
- affected code follows `docs/CODING_STANDARDS.md`
- build succeeds or remaining blockers are clearly reported
