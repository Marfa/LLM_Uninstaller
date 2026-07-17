#!/usr/bin/env bash
# Installs a pre-commit hook that blocks commits with suspected secrets.
set -euo pipefail

if ! command -v gitleaks >/dev/null 2>&1; then
  echo "gitleaks not found. Install with: brew install gitleaks" >&2
  exit 1
fi

root="$(git rev-parse --show-toplevel)"
hook="$root/.git/hooks/pre-commit"
mkdir -p "$(dirname "$hook")"

cat > "$hook" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if ! command -v gitleaks >/dev/null 2>&1; then
  echo "gitleaks missing; skip secret scan (brew install gitleaks)" >&2
  exit 0
fi
gitleaks protect --staged --verbose --redact
EOF

chmod +x "$hook"
echo "Installed pre-commit hook: $hook"
