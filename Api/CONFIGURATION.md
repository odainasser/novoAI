# Configuration & Secrets

`appsettings.json` (and `appsettings.*.json`) are **git-ignored** and intended for
local development only. **Never put production secrets in them.** Production
values must be supplied out-of-band via environment variables, .NET user-secrets
(dev), or a secret store (Azure Key Vault, AWS Secrets Manager, …).

.NET configuration maps nested keys to environment variables with a double
underscore (`__`) separator. The following **must** be set in any non-Development
environment:

| Setting | Environment variable | Notes |
|---|---|---|
| JWT signing key | `JwtSettings__Secret` | ≥ 32 chars, high entropy. App refuses to start in non-Development if missing/weak or equal to the dev placeholder. |
| DB connection | `ConnectionStrings__DefaultConnection` | |
| SMTP user | `EmailSettings__SmtpUsername` | |
| SMTP password | `EmailSettings__SmtpPassword` | |
| Ollama URL | `OllamaSettings__BaseUrl` | Must be a **trusted, internal** host — business data is sent there for answer phrasing. |

Example (Linux/macOS):

```bash
export JwtSettings__Secret="$(openssl rand -base64 48)"
export ConnectionStrings__DefaultConnection="Server=...;Database=ByteMartDB;..."
export EmailSettings__SmtpPassword="..."
```

Example (Windows PowerShell):

```powershell
$env:JwtSettings__Secret = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
```

## Rotation

The JWT secret and SMTP credentials that previously lived in committed-style
config should be **rotated**, since any value that has been shared in plaintext
must be treated as compromised.
