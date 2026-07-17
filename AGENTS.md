# Agent / contributor rules

## Dependencies (NuGet)

This is a .NET project — use NuGet tooling, not npm.

1. **Before adding a package:** resolve the version from nuget.org (`dotnet add package X` picks current latest). Do not invent versions from memory.
2. **After every install or version bump:** run
   ```bash
   dotnet restore -p:EnableWindowsTargeting=true
   dotnet list package --vulnerable --include-transitive
   ```
   Fail the change if High or Critical findings remain.
3. **Regular outdated check** (at least before a release):
   ```bash
   dotnet list package --outdated
   ```
4. Prefer staying on the project TFM line (`net8.0-*`) unless an explicit upgrade is requested. Prefer a direct `PackageReference` override for a vulnerable transitive package over jumping major frameworks solely to quiet an advisory.
5. New packages go through the same gate as `/check-dep`: registry status, recent release, no critical advisories, name not a typosquat.

## Secrets

- Never commit `.env`, tokens, API keys, or private certificates.
- Run `gitleaks detect --source .` before release; pre-commit uses `gitleaks protect --staged` (see `scripts/install-gitleaks-hook.sh`).

## Security CI

GitHub Actions workflow `.github/workflows/security.yml` runs on every push and PR:

- `dotnet list package --vulnerable --include-transitive` (fails on High/Critical)
- `gitleaks` full-history scan
