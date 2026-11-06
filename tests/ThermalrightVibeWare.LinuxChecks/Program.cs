// VBWR B
// TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
// Explicit offline checks. No native library or host hardware is opened.
// VBWR E

var checks = 0;
void Check(bool value, string message)
{
    if (!value) { throw new InvalidOperationException(message); }
    checks++;
}

var fixtures = Path.GetFullPath(Path.Combine("artifacts", "linux-checks", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(fixtures);
try
{
    LinuxNvmlChecks.Run(Check);
    LinuxGpuCompositionChecks.Run(Check);
    LinuxAccessChecks.Run(fixtures, Check);
    Console.WriteLine($"{checks} offline Linux sensor checks passed. No host hardware, USB, driver or installation actions.");
}
finally { Directory.Delete(fixtures, recursive: true); }
