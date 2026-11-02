# AGENTS.md

Guidance for AI coding agents and contributors working on Paper Route.

## What this is

A Windows 11 tray app (C# / .NET 10, WinForms) that is a **local-only SMTP server**. Calibre-Web /
Calibre-Web-Automated "Send to eReader" e-mails a book to it; Paper Route checks the message, converts
EPUB to AZW3 with the user's installed Calibre, copies the book onto a USB-connected Kindle, writes the
Kindle cover thumbnail, and only then replies `250` to CWA. Every failure is a readable SMTP reply that
CWA shows on its Tasks page. See README.md for the user's view.

## Layout

| Path | Role |
| --- | --- |
| `src/PaperRoute.Core` (`net10.0`) | Config, SMTP host (SmtpServer), auth/recipient/size guards, MIME → attachment, file-type check, MOBI header parsing, delivery decisions, activity log. No Windows APIs. |
| `src/PaperRoute.Devices` (`net10.0-windows`) | Kindle discovery and copy: USB mass storage (WMI hardware ID), MTP (experimental), cover thumbnails (GDI+), Calibre conversion (process in a kill-on-close job object). |
| `src/PaperRoute` (`net10.0-windows`, WinExe) | Tray UI only: tray icon/menu, Settings window, autostart, wiring. |
| `tests/*` | xUnit v3 on Microsoft Testing Platform; FsCheck property tests; `PaperRoute.FakeConverter` stands in for `ebook-convert`; `PaperRoute.CompatHost` + `tests/compat/*.py` replay CWA's exact `smtplib` code. |
| `build/coverage_gate.py` | CI gate: `PaperRoute.Core` line coverage ≥ 85%. |

## Build and test (Windows, .NET 10 SDK)

```powershell
dotnet build PaperRoute.slnx -c Release          # analyzers on, warnings are errors
dotnet test --solution PaperRoute.slnx -c Release
dotnet format PaperRoute.slnx --verify-no-changes
python tests/compat/run_compat.py dotnet tests/PaperRoute.CompatHost/bin/Release/net10.0/PaperRoute.CompatHost.dll
```

Restores use NuGet lock files (`packages.lock.json`); CI restores with `--locked-mode`. After changing
packages, run a normal `dotnet restore` and commit the updated lock files.

## Rules that must hold (security and behaviour)

- Listen on `127.0.0.1` by default; never default to `0.0.0.0`. Docker Desktop delivers
  `host.docker.internal` connections to Windows loopback.
- SMTP AUTH is required (random 32-character password, compared in constant time); the password is
  never logged and is stored only DPAPI-encrypted (`authPasswordProtected`, current user, app entropy);
  a plain `authPassword` from older settings files is migrated on load.
- Never reply `250` for a book before it is on the Kindle and its size verified. No queue, no storage:
  a failed delivery is a rejected message. Any unexpected exception becomes a `451` reply plus a log
  line, never a dropped connection.
- Books only for the configured recipient, one recipient, one book per message; messages without an
  attachment are CWA's test e-mail and are accepted for any address. Nothing is ever relayed.
- Attachment names are sanitized (illegal characters, Windows device names such as `CON.tar.azw3`,
  path traversal, 150-character limit without splitting surrogate pairs).
- Content must match the type (`BOOKMOBI`/`TEXtREAd` at byte 60, `%PDF-`, KFX container, ZIP for EPUB,
  no NUL in TXT); programs (`MZ`, ELF) are always refused. Converter output is checked again.
- A drive is a Kindle when Windows reports Kindle hardware (`VEN_KINDLE`/`VEN_AMAZON` in the disk's
  PnP ID); the same hardware ID must still be on that drive letter right before writing.
- Writes go to `documents\` via a temp name + rename; failures leave nothing behind. Thumbnails go to
  `system\thumbnails\thumbnail_<ASIN>_<TYPE>_portrait.jpg`, max 214×330, cover images over 4096×4096
  or 10 MB are not decoded; a thumbnail problem never fails a delivery.
- Calibre runs without a shell, with fixed file names in a private temp folder (`CALIBRE_TEMP_DIR`
  inside it), a 5-minute limit, and a job object that stops helper processes; the folder is always
  deleted.
- SMTP reply texts are user-facing (CWA shows them verbatim): keep them plain, specific and actionable,
  and update README "Error messages" when they change.

## How to work here

- Test first: write the failing test, watch it fail for the right reason, then implement. Hardware and
  UI code that cannot be unit-tested gets a fake (see the test `Fakes/` folders) or a clear note.
- All analyzers (Microsoft, SonarAnalyzer, Roslynator, Meziantou) run with warnings as errors; fix
  findings rather than suppressing them. A suppression needs a written justification next to it.
- One type per file; LF line endings; `dotnet format` clean.
- GitHub Actions: pin every action to a full commit SHA with a version comment, keep `permissions:`
  minimal, never interpolate `${{ }}` into `run:` scripts; `actionlint` and `zizmor --persona=pedantic`
  must be clean.
- Don't commit secrets, personal data, device serial numbers or machine-specific paths.

## CI and releases

- Any branch / pull request: `ci.yml` (format, analyzers, tests, coverage gate, CWA compatibility test),
  `security.yml` (Semgrep, Gitleaks, OSV-Scanner, Trivy, actionlint, zizmor, markdownlint, typos);
  CodeQL and dependency review on pull requests.
- Merge to `main`: the same, plus `release.yml` - tests, single-file x64 + ARM64 builds, Defender scan,
  SBOM. It publishes GitHub release `v<Version>` (from `Directory.Build.props`) with attestations only
  when that release doesn't exist yet. **To release, bump `<Version>` in the same pull request.**
