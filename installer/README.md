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

Run the Windows Debug build and reinstall command from an elevated PowerShell 7
session.

```powershell
./scripts/vibeware.ps1 build -Architecture x64 -PrepareImports
```

Build from the current repository source with the existing Visual Studio C++
tools. Restore missing dependencies from official sources. Never use an installed
app, an older MSI or a release binary as the app payload.

The preparation phase stages the pinned WiX 4.0.6 packages alongside the other
external inputs. Defender scans new archives and extracted files in staging.
Packaging checks the result in the same PowerShell session. A failed, incomplete
or unavailable scan stops preparation before the existing app is removed.

The reinstall command removes generated output after setup succeeds and keeps
the final MSI under `artifacts/packages/`.

Cancelled elevation, shared or foreign drivers, locked or pending driver
removal, upgrades, repair and rollback need further checks. See `cave.md`.

References: [WiX UI](https://docs.firegiant.com/wix/tools/wixext/wixui/),
[upgrade order](https://docs.firegiant.com/wix/schema/wxs/majorupgrade/),
[service deletion](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-deleteservice).
The completion page uses an original layout and WiX's supported overridable
success sequence. I studied the upstream [4.0.6 ExitDialog](https://github.com/wixtoolset/wix/blob/v4.0.6/src/ext/UI/wixlib/ExitDialog.wxs)
and [sequence rules](https://learn.microsoft.com/en-us/windows/win32/msi/installuisequence-table).
