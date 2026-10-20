#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
assets_dir="${1:-$project_dir/Assets}"
if [[ ! -d "$assets_dir" ]]; then
    printf 'Asset directory not found: %s\n' "$assets_dir" >&2
    exit 1
fi

declare -A seen=()
checked=0
errors=0
while IFS= read -r -d '' metadata; do
    checked=$((checked + 1))
    guid="$(awk '/^guid:[[:space:]]/ {print $2}' "$metadata")"
    if [[ ! "$guid" =~ ^[0-9a-fA-F]{32}$ ]]; then
        printf 'FAIL: %s has an invalid GUID (%s characters; expected 32 hexadecimal characters)\n' \
            "$metadata" "${#guid}" >&2
        errors=$((errors + 1))
        continue
    fi
    guid="${guid,,}"
    if [[ -n "${seen[$guid]+present}" ]]; then
        printf 'FAIL: duplicate GUID in %s and %s\n' "${seen[$guid]}" "$metadata" >&2
        errors=$((errors + 1))
    else
        seen[$guid]="$metadata"
    fi
done < <(rg --files --hidden --no-ignore -0 -g '*.meta' "$assets_dir")

if (( checked == 0 || errors > 0 )); then
    printf 'FAIL: checked %s metadata files; %s invalid or duplicate GUIDs\n' "$checked" "$errors" >&2
    exit 1
fi
printf 'PASS: %s metadata files have valid, unique GUIDs\n' "$checked"
