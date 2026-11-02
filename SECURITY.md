# Security policy

Paper Route accepts e-mail on your PC and writes files to your e-reader, so security reports are
very welcome.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Report it privately:

- GitHub private vulnerability reporting:
  <https://github.com/23ewrdtf/CWA-Paper-Route/security/advisories/new>
  (repository **Security** tab → **Report a vulnerability**)

Include what you found, how to reproduce it, the Paper Route version and your Windows version.

## What happens next

- You get an acknowledgement within 7 days.
- Confirmed issues are fixed in a new release as soon as practical, normally within 30 days; you are
  kept informed and credited in the release notes unless you prefer otherwise.
- Please give us a reasonable chance to release a fix before disclosing details publicly.

## Scope

In scope: the Paper Route app and its GitHub Actions workflows - for example SMTP authentication or
recipient checks being bypassed, files written outside the e-reader's `documents` and
`system\thumbnails` folders, crashes or hangs caused by crafted e-mails or books, secrets exposed in
logs or settings.

Out of scope: bugs in Calibre, Calibre-Web/CWA, Windows or the Kindle itself (please report those to
their projects), and anything requiring an attacker who already runs code as your Windows user.

## Supported versions

Only the latest release receives security fixes.
