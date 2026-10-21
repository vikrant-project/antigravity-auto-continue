# Security

Do not post tokens, login databases, account snapshots, or AppData folders in issues. If a credential is exposed, revoke it with its provider.

The controller accesses the signed-in IDE's local service and stores saved account snapshots using Windows CurrentUser DPAPI. See [the data and account guide](docs/USER-GUIDE.md#credential-storage). The portable distribution contains no account vault.

Report vulnerabilities using GitHub's private vulnerability reporting when available. Otherwise open an issue containing only a high-level description and ask for a private reporting channel. Never include working credentials.
