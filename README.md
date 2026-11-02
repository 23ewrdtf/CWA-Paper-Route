# Paper Route

A local **"Send to eReader"** for [Calibre-Web](https://github.com/janeczku/calibre-web) and
[Calibre-Web-Automated (CWA)](https://github.com/crocodilestick/Calibre-Web-Automated): click *Send*
in CWA and the book lands on the Kindle plugged into your PC's USB port. No internet, no e-mail
account, no Amazon *Send to Kindle*.

## What it does

Paper Route is a small Windows tray app that acts as a **local-only e-mail server**. You point CWA's
existing e-mail settings at it. When CWA "e-mails" a book, Paper Route checks it and copies it into
the connected Kindle's `documents` folder, and only then tells CWA it was delivered. If something is
wrong (no Kindle plugged in, wrong format, …) CWA shows a clear error instead. Nothing is queued,
and nothing is kept on your PC (EPUB books pass through a temporary folder while Calibre converts
them, which is deleted straight afterwards).

## Unofficial

Paper Route is an independent project. It is **not affiliated with** the Calibre-Web or
Calibre-Web-Automated projects, or with Amazon. Kindle is a trademark of Amazon.

## Requirements

- Windows 11 (x64 or ARM64).
- [Calibre-Web](https://github.com/janeczku/calibre-web) or CWA **on the same PC** — in Docker Desktop
  (SMTP hostname `host.docker.internal`) or installed directly on Windows (SMTP hostname `127.0.0.1`).
  Calibre-Web on another machine (for example a NAS) isn't supported: Paper Route only accepts
  connections from its own PC.
- A Kindle connected by USB (see [Supported Kindles](#supported-kindles)).
- **[Calibre](https://calibre-ebook.com/download_windows)** (free) for EPUB books. CWA always sends
  EPUB, and Kindles can't read EPUB over USB, so Paper Route converts it to AZW3 with Calibre's
  converter. Install it with `winget install calibre.calibre` or from the website; Paper Route finds
  it automatically (or set `calibrePath` in the settings file).

## Install

1. Download `PaperRoute-<version>-win-x64.zip` (or `win-arm64` for ARM PCs) from
   [Releases](https://github.com/23ewrdtf/CWA-Paper-Route/releases).
2. Unzip it anywhere, for example `C:\Apps\PaperRoute`, and run `PaperRoute.exe`.
   Windows may warn about an unknown app — see [Windows SmartScreen](#windows-smartscreen).
3. The tray icon appears (look under the **^** arrow next to the clock). On first start the
   Settings window opens; tick **Start with Windows** in the tray menu if you want it always on.

## Set up CWA

Works the same for Calibre-Web and CWA (the e-mail code and settings are the same).

1. In Paper Route: tray icon → **Settings…** → **Copy CWA settings**.
2. In Calibre-Web/CWA: **Admin → Basic Configuration → E-mail Server Settings**, choose *Standard e-mail
   account* and fill in the copied values:

   | CWA field | Value |
   | --- | --- |
   | SMTP hostname | `host.docker.internal` (Calibre-Web/CWA in Docker) or `127.0.0.1` (installed on Windows) |
   | SMTP port | `2525` (or the port shown in Paper Route) |
   | Encryption | **None** |
   | SMTP login | `cwa` |
   | SMTP password | the password from Paper Route |
   | From e-mail | `cwa@localhost` |

3. Click **Save and send test e-mail**. Paper Route shows *"Test e-mail received - CWA is connected"*.
4. In your CWA **user profile**, set *Send to eReader e-mail* to `kindle@paperroute.local`.

## Using it

Plug in the Kindle, open a book in CWA and choose **Send to eReader**. Paper Route converts EPUB
books to AZW3 (a few seconds to a minute), copies the book and shows *"Delivered … to Kindle (E:)"*.
Eject the Kindle and the book is in your library. The tray menu shows whether Calibre was found.

- **Re-sending a book replaces the copy on the Kindle** (same file name).
- **Covers:** for Kindles connected as a drive, Paper Route also writes the book's cover thumbnail
  (`system\thumbnails`), so the cover shows straight away instead of a blank tile. Tip: Kindles on
  WiFi may replace covers of copied books with blank ones — keep WiFi off if that happens.
- One book per send; only one Kindle may be connected at a time.
- Accepted types: EPUB (converted to AZW3), AZW3, AZW, MOBI, PRC, KFX, PDF, TXT.

## Error messages

CWA shows these in its **Tasks** page:

| Message | What to do |
| --- | --- |
| `Kindle not connected - plug it in and send again` | Connect the Kindle by USB and send again. |
| `More than one Kindle connected - unplug all but one` | Leave only one Kindle connected. |
| `Install Calibre (calibre-ebook.com) so Paper Route can convert EPUB books` | Install Calibre (see [Requirements](#requirements)), then send again. |
| `Converting … to AZW3 failed: … - nothing was copied` | Calibre couldn't convert this book (the reason is shown). Try converting it in Calibre desktop to see why. |
| `This doesn't look like a real AZW3 file - nothing was copied` | The file isn't what its name says; check the book in CWA. |
| `No e-book attachment found` | The format sent isn't a Kindle book type. |
| `Only one book per message` | Send books one at a time. |
| `Not enough space on Kindle (E:): N MB needed, M MB free` | Free up space on the Kindle. |
| `Copy to Kindle (E:) failed: … - nothing was saved, send again` | Usually the Kindle was unplugged during the copy. |
| `Book is larger than the 200 MB limit set in Paper Route` | Raise `maxMessageMB` in the settings file if you really need to. |
| `Authentication required - …` / `Wrong login or password - …` | Copy the login and password from Paper Route's Settings window into CWA again. |
| `Unknown recipient - Paper Route only accepts mail for kindle@paperroute.local` | Fix the *Send to eReader e-mail* in your CWA profile. |
| `Unrecognized command` | Usually a typo in CWA's *From e-mail* (for example a trailing dot in `cwa@localhost.`). Use `cwa@localhost`. |
| `Socket Error sending e-mail: Connection refused` | Paper Route isn't running (or uses another port). Start `PaperRoute.exe` again from the folder you unzipped it to. |

## Supported Kindles

- **USB drive Kindles** (the Kindle shows up as a drive letter): tested end to end on a Kindle
  Paperwhite 6th generation (CWA → EPUB conversion → copy → cover shown). Other drive-letter Kindles
  should work the same way; reports welcome.
- **MTP Kindles** (newer models that show up as a device without a drive letter): **experimental**,
  not tested on real hardware yet. Please [open an issue](https://github.com/23ewrdtf/CWA-Paper-Route/issues)
  with your Kindle model and whether it worked.

## Security

- Listens on **127.0.0.1 only** and accepts connections from this PC only (Docker Desktop delivers
  CWA's connections there).
- Requires a **login and a random 32-character password**; the third wrong password in a
  connection closes it, and every refusal shows a tray warning.
- Accepts mail for **one address only**, one recipient, one book — it can't be used to relay mail.
- **Checks each book's content** matches its type (Kindle books carry a `BOOKMOBI` marker, PDFs start
  with `%PDF-`, …); disguised files and programs are refused.
- Never runs a book. It reads only what it needs: the MOBI/AZW3 header and the embedded cover image
  (decoded with Windows' image codecs, refused above 4096x4096 or 10 MB) for the Kindle thumbnail.
  AZW3/MOBI/PDF books go from memory straight to the Kindle; EPUB books are converted by Calibre in a private temporary folder that is deleted
  afterwards (Calibre runs as a separate process, without a shell, and is stopped after 5 minutes). Keep Windows and your Kindle's software up to date — that protects
  against malformed books whatever way they reach them.
- Logs (`%LOCALAPPDATA%\PaperRoute\logs`, kept 14 days) record time, client, file name, size and
  result — never the password. Settings live in `%APPDATA%\PaperRoute\config.json`; the password is
  stored **encrypted with Windows DPAPI** for your Windows account, so a copied or backed-up settings
  file doesn't reveal it. (Moved to another PC or account, Paper Route makes a new password and asks
  you to paste it into CWA.)
- Found a security problem? Please report it privately — see [SECURITY.md](SECURITY.md).

## Windows SmartScreen

Releases are not code-signed yet, so Windows may show *"Windows protected your PC"*. Click
**More info → Run anyway**. To check your download is genuine:

```powershell
# compare with SHA256SUMS.txt from the release
Get-FileHash .\PaperRoute-<version>-win-x64.zip -Algorithm SHA256
# verify it was built by this repository's GitHub Actions (needs the GitHub CLI)
gh attestation verify .\PaperRoute-<version>-win-x64.zip --repo 23ewrdtf/CWA-Paper-Route
```

Every release also includes an SBOM (`paperroute.cdx.json`) listing all components, and each build
is scanned with Microsoft Defender before it is published.

## Releases

Every merge to `main` is built and tested; a new release `v<Version>` is published automatically when
the version in `Directory.Build.props` is new. Download it from
[Releases](https://github.com/23ewrdtf/CWA-Paper-Route/releases).

## Building from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet test --solution PaperRoute.slnx
dotnet run --project src/PaperRoute
```

CI runs the analyzers (warnings are errors), the tests with a coverage gate, a CWA compatibility
test using CWA's own e-mail code, CodeQL, Semgrep, Gitleaks, OSV-Scanner, Trivy, zizmor, actionlint,
markdownlint, typos, dependency review and OpenSSF Scorecard.

## No warranty

Paper Route is provided **as is, without warranty of any kind**, express or implied — including
merchantability, fitness for a particular purpose and non-infringement. Use it at your own risk: keep
backups of your books and your e-reader's contents. The authors are not liable for any damage or data
loss. See the [LICENSE](LICENSE) for the full terms.

## License

[MIT](LICENSE)
