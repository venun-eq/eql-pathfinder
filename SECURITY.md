# Security Policy

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Instead, use GitHub's **Private Vulnerability Reporting** (**Security → Report a vulnerability** on this repository).
Include a clear description of the issue, reproduction steps, and any affected component (`src/`, `server/`, `site/`, or
release/update infrastructure).

## Scope

Security reports are especially welcome for:

- **Desktop app (`src/`):** WebView2 host isolation, log/file parsing, local SQLite storage, update verification, or
  unexpected data leaving the PC.
- **Community API (`server/`):** authentication (`X-EQLP-*` ECDSA P-256 signatures), input validation, quota/rate-limit
  bypasses, R2 screenshot handling, or privacy regressions (the server must never store raw IP addresses).
- **Community job (`CommunityJob.cs`):** handling of untrusted or malformed rows from D1.
- **Website (`site/`):** CSP/header regressions or privacy policy mismatches.

## Supported Versions

Only the latest published release of EQL Pathfinder and the current `main` branch of `server/` and `site/` are
supported. The desktop app updates itself automatically.
