# Still to do

This is a hobby project, provided as is, without warranty.

- Check memory use over longer runs, performance and security.
- Try more displays and settings, and the Codex plugin with real activity.
- Linux and macOS USB and audio support are not implemented.
- Sensor readings depend on available hardware and access. A missing reading is not a measured zero.
- Linux indicator visibility depends on the desktop panel’s AppIndicator support. Notification delivery follows desktop preferences.
- Installer upgrade, repair, rollback and cancellation need further checks.

**Themes run Python code on your computer. For now, get them only from the [official repository](https://github.com/umbertotechnopreneur/thermalright-vibeware). The security review is unfinished.**

## Latest local build

Linux amd64 Debug 0.1.29 builds on Ubuntu 26.04 with Python 3.14 dependencies.
The latest run passed 44 offline Linux sensor checks, 61 studio checks and 60
light/dark theme previews. On the local RTX 4060, NVML returned temperature,
utilization, clocks, VRAM and fan target percentage. Board power was unsupported
and stayed unavailable. Sensor access distinguished detection from a real sample.

Linux USB, audio, media sessions and DIMM/SPD identities remain unavailable.
No driver was installed, no system permissions were changed and no USB trial was
performed. Ubuntu 24.04 CI, Windows regression and longer sessions remain unverified.
