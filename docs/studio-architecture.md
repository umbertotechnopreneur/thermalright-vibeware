# Studio state and provider boundaries

The display loop belongs to `StudioSession`, not the window. Closing a preview, hiding the window or blocking its dispatcher must not stop an active output session.

```mermaid
flowchart TD
    View[OpenSilver view] --> VM[StudioViewModel]
    VM --> Model[StudioModel: atomic state]
    Model --> Store[StudioSettingsStore: versioned JSON]
    Session[StudioSession: background scheduler] --> Model
    VM --> Session
    Session --> Client[IPanelClient: JSON-lines]
    Client --> Engine[Windows engine host]
    Engine --> Data[ThermalrightVibeWare.Data: portable models and collection]
    Data --> Providers[Provider interfaces]
    Providers --> Windows[Windows media, audio, metrics and network]
    Providers --> Fixtures[Offline test providers]
```

## State and persistence

`StudioModel.Update` commits preferences, screen choices and palette overrides under one lock. Editors and readers receive detached copies. A failed edit commits nothing. Each frame takes one copy, including its palette and screen selections. Automatic rotation checks the observed revision before committing, so it cannot overwrite a newer user edit.

`StudioViewModel` owns catalog normalization and selection commands. It also exposes typed font, Codex and diagnostics operations and their latest results. The view keeps native controls, layout and temporary dialogs; it does not send raw protocol method names. `StudioSession` owns output scheduling and engine serialization.

`StudioSettingsStore` reads at most 1 MiB and writes `schemaVersion: 1`. Unversioned settings are rejected and left untouched. A save flushes a temporary file, then atomically replaces the current file and preserves its previous contents as `.bak`. A corrupt file falls back to a valid backup. Recovery does not replace that good backup with corrupt data. Unsupported versions and unrecoverable files are left untouched, with a visible notice; saving remains disabled for that session. Runtime revisions and timestamps are not persisted.

`StudioPreferences.IsFirstRun` defaults to `true` and is stored as `isFirstRun` in the same versioned file. The first Windows launch shows the studio even when sign-in passes tray arguments. Once the main page has loaded, `StudioModel.CompleteFirstRun` saves a detached state with the flag set to `false`, then publishes it. A failed save leaves the flag true and reports a notice. Later Windows launches start in the tray; Open, About and ordinary activation still restore the existing window. Without a working tray, the host keeps its visible-window fallback.

See [Code maintenance](code-maintenance.md) for the source file map and documentation conventions.

## Platform boundary

`ThermalrightVibeWare.Data` targets plain `net10.0`. It holds data records, provider interfaces and the shared collection service. It references Contracts, with no Windows APIs or hardware libraries. `ISensorProvider` adds raw diagnostics and access metadata to `IMetricsProvider`. The host selects adapters through `PlatformDashboardProviders`; constructing them does not open or sample hardware. Disabled features do not read their providers. A failed playback provider cannot discard available media or sensor results.

`ThermalrightVibeWare.Backends.Windows` owns the existing Windows sensor, media,
volume and loopback implementations. Only the Windows engine target references it
and the pinned LibreHardwareMonitor fork. `WindowsDashboardProviders` keeps the
Windows composition and optional artist provider.

`ThermalrightVibeWare.Backends.Linux` reads Linux sensors through procfs and sysfs.
It has no monitoring-library dependency. Core and CLI have separate `net10.0` and
Windows targets. The Linux studio starts the bundled `engine/trv` child. It negotiates
supported methods through `hello`, then loads manifests and Python/Skia previews.
Linux dashboards use the existing kernel sensor reader; media, audio and network
sources remain explicitly unavailable. USB requests fail before device access.
Windows raster conversion is excluded. See [Linux sensors](linux-sensors.md).

Linux sensor coverage depends on kernel drivers and access permissions. Windows
runtime regression behavior remains unverified for this separation. Tests and
runtime trials require owner approval.
