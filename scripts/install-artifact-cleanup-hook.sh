#!/usr/bin/env bash
# Installs a post-commit hook that removes stale local build artifacts.
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
hook="$root/.git/hooks/post-commit"
cleanup="$root/scripts/clean-build-artifacts.sh"
mkdir -p "$(dirname "$hook")"

if [[ ! -x "$cleanup" ]]; then
  chmod +x "$cleanup"
fi

cat > "$hook" <<EOF
#!/usr/bin/env bash
set -euo pipefail
"$cleanup"
EOF

chmod +x "$hook"
echo "Installed post-commit hook: $hook"
