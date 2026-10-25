# Windows MSI

`Package.wxs` installs the published x64 or ARM64 payload for the current user.
It creates one Start entry and one saved-session startup entry. The MSI owns
Windows uninstall registration. User settings stay outside its install folder.
Opening the MSI shows a license/setup page, progress, and a completion or error
page. Successful interactive installation offers two independent, default-checked
options: **Start after install** and **See the latest news**. Finish starts the
installed app and/or opens its [GitHub Releases page](https://github.com/umbertotechnopreneur/thermalright-vibeware/releases?utm_source=thermalright-vibeware&utm_medium=installer&utm_campaign=release_news).
The fixed UTM tags identify the installer link. Repair, removal, silent setup,
cancellation and failed setup do not run these actions.

Before install, repair or removal changes files, a native helper closes only
verified Thermalright VibeWare processes. New studios support a normal installer exit;
old or stuck copies are stopped after a bounded wait. The helper also releases
an owned Open Hardware Monitor driver: stop, wait, delete service, wait for the
registration to disappear, then remove and check the generated `.sys` files.
It preserves drivers owned by other software and fails clearly on shared,
busy or pending-deletion drivers. Reading a locked file is not this check.

Cleanup runs as the current user. It requests administrator permission only
when needed, including an old elevated studio or driver. A silent, unelevated
install fails instead of opening an unexpected permission dialog. Details go
to `%LOCALAPPDATA%\ThermalrightVibeWare\logs\setup.log`. The helper is embedded in the MSI;
it adds no installed service or runtime dependency.

The new MSI releases resources before removing an older MSI, including 0.1.1.
During file changes, a per-user named event blocks new studio launches. The
installer clears it on commit or rollback; ending its host process also releases
it. Setup gives the studio up to sixty seconds to drain its worker and engine
before its existing scoped termination fallback.
Keep the existing payload component identities stable for this upgrade order.
Setup replaces its complete owned payload, including preview assemblies with
unchanged file versions. Edited bundled files can be replaced; user themes and
settings belong outside this install tree.
MSI handles rollback for its files, shortcuts and registration. It does not
restart the studio or reload a removed runtime driver on rollback; OHM can
create its driver again on a later owner-requested sensor read.

Publish with `scripts/Publish.ps1`, then pass its staging folder to:

```powershell
./scripts/build/Build-Msi.ps1 -PayloadPath artifacts/staging/<published-folder> -Architecture x64
```

Build from the current repository source with the existing Visual Studio C++
tools. Restore missing dependencies from official sources. Never use an installed
app, an older MSI or a release binary as the app payload.

Before first use, stage and explicitly scan each new dependency archive and its
extracted files. The pinned native source/SDK and Python inputs support separate
preparation with `scripts/build/Build-PhotinoNative.ps1 -PrepareImports` and
`scripts/build/Bundle-Python.ps1 -PrepareImports -Destination artifacts/dependency-stage`.
Both need `-Architecture x64` or `ARM64`. For restored NuGet packages and the
WiX 4.0.6/UI extension tree, use `scripts/maintenance/Assert-DefenderClean.ps1 -ScanNewImport` with
their staged path and official `-Source`. This fails on missing Defender or an
unfinished, failed or detected scan. No installed SDK/runtime tree is scanned.

Builds only check recorded file hashes. They stop on missing evidence and do
not start scans. Unchanged content reuses evidence from
`%LOCALAPPDATA%/ThermalrightVibeWare/build-security/first-import-scans.jsonl`, which survives
repository output cleanup. Generated app files and the MSI are not imports.

The build restores WiX 4.0.6 and its matching UI extension locally when needed.
It runs no tests, cleanup helper or MSI validation. After delivery, run
`scripts/maintenance/Clean-BuildOutputs.ps1 -KeepMsiPath`
with the final file under `artifacts/packages/`. This removes generated output,
including the local build tool, while preserving that MSI.

Windows x64 removal and visible installation of 0.1.4 returned status 0 on the
owner's machine. Removal of 0.1.2 stopped its elevated app and released its own
driver. One obsolete pre-MSI uninstall registration remains unresolved despite
the registry-removal action. Cancelled elevation, shared/foreign drivers,
locked/pending driver removal, in-place upgrades, repair and rollback still need
owner-approved checks. See `cave.md` for the exact boundaries.

References: [WiX UI](https://docs.firegiant.com/wix/tools/wixext/wixui/),
[upgrade order](https://docs.firegiant.com/wix/schema/wxs/majorupgrade/),
[service deletion](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-deleteservice).
The completion page uses an original layout and WiX's supported overridable
success sequence. I studied the upstream [4.0.6 ExitDialog](https://github.com/wixtoolset/wix/blob/v4.0.6/src/ext/UI/wixlib/ExitDialog.wxs)
and [sequence rules](https://learn.microsoft.com/en-us/windows/win32/msi/installuisequence-table).
