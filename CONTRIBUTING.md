# Contributing

Use Windows 10/11 x64 with .NET Framework 4.8. Build and verify with:

```powershell
.\scripts\build.ps1
.\scripts\verify.ps1
```

For layout changes, render at least one landscape, portrait and square preview:

```powershell
.\ReiCast.exe --render-preview landscape.png idle 1024x600
.\ReiCast.exe --render-preview portrait.png dot 480x854
.\ReiCast.exe --render-preview compact.png busy 480x480
```

Keep status reads bounded, use a worker for slow data sources, and preserve the
one-second UI timer and minimum sampling intervals. Handle an absent monitor and
ambiguous auto detection without occupying the primary screen. A test should
cover a new behavioral boundary rather than merely repeat implementation code.

Hardware reports are welcome. Use the issue template and distinguish actual
panel output from a screenshot or a valid software heartbeat. Keep real chat
content, credentials, runtime/config files and complete Codex logs out of commits.

Use a focused pull request explaining the concrete behavior, reproduction steps,
validation and remaining hardware limits. Contributions are under the repository's
MIT license. Include provenance and suitable licensing for any new bundled asset.
