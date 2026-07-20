#!/usr/bin/env bash
# Removes local .NET build artifacts older than BUILD_ARTIFACT_MAX_AGE_DAYS (default 7).
set -euo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$root"

max_age_days="${BUILD_ARTIFACT_MAX_AGE_DAYS:-7}"
removed=0

if date -v-"${max_age_days}"d +%s >/dev/null 2>&1; then
  cutoff_epoch="$(date -v-"${max_age_days}"d +%s)"
else
  cutoff_epoch="$(date -d "${max_age_days} days ago" +%s)"
fi

file_mtime() {
  local file="$1"
  # GNU stat (Linux/Git Bash) first; BSD stat (macOS) as fallback.
  if stat -c %Y "$file" >/dev/null 2>&1; then
    stat -c %Y "$file"
  else
    stat -f %m "$file"
  fi
}

is_older_than_cutoff() {
  local file="$1"
  (( $(file_mtime "$file") < cutoff_epoch ))
}

remove_file() {
  local file="$1"
  rm -f -- "$file"
  removed=$((removed + 1))
}

while IFS= read -r -d '' file; do
  if is_older_than_cutoff "$file"; then
    remove_file "$file"
  fi
done < <(find . \
  \( -path './.git' -o -path './.git/*' \) -prune -o \
  \( \
    -path '*/bin/*' -o \
    -path '*/obj/*' -o \
    -path '*/out/*' -o \
    -path '*/publish/*' -o \
    -path '*/release/*' -o \
    -path '*/.vs/*' -o \
    -path '*/reports/*' \
  \) -type f -print0 2>/dev/null)

while IFS= read -r -d '' file; do
  if is_older_than_cutoff "$file"; then
    remove_file "$file"
  fi
done < <(find . -maxdepth 1 -type f -name 'LLMUninstaller-*-win-*.zip' -print0 2>/dev/null)

while IFS= read -r -d '' dir; do
  rmdir "$dir" 2>/dev/null || true
done < <(find . \
  \( -path './.git' -o -path './.git/*' \) -prune -o \
  \( -type d \( -name bin -o -name obj -o -name out -o -name publish -o -name release -o -name reports \) \) \
  -empty -print0 2>/dev/null)

if [[ "$removed" -gt 0 ]]; then
  echo "Removed $removed build artifact(s) older than ${max_age_days} day(s)."
fi
