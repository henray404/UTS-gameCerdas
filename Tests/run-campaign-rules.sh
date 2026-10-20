#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
editor="${1:-${UNITY_EDITOR:-}}"
mono_dir=""

if [[ -n "$editor" ]]; then
    if [[ ! -f "$editor" ]]; then
        printf 'Unity editor not found: %s\n' "$editor" >&2
        exit 1
    fi
    editor_dir="$(cd -- "$(dirname -- "$editor")" && pwd)"
    for candidate in "$editor_dir/Data/MonoBleedingEdge/bin" "$editor_dir/../MonoBleedingEdge/bin"; do
        if [[ -x "$candidate/mono" && -f "$candidate/../lib/mono/4.5/mcs.exe" ]]; then
            mono_dir="$candidate"
            break
        fi
    done
else
    shopt -s nullglob
    candidates=(
        "$HOME"/Unity/Hub/Editor/*/Editor/Data/MonoBleedingEdge/bin
        /opt/unity/Editor/Data/MonoBleedingEdge/bin
        /opt/Unity/Hub/Editor/*/Editor/Data/MonoBleedingEdge/bin
        /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MonoBleedingEdge/bin
    )
    for candidate in "${candidates[@]}"; do
        if [[ -x "$candidate/mono" && -f "$candidate/../lib/mono/4.5/mcs.exe" ]]; then
            mono_dir="$candidate"
            break
        fi
    done
fi

if [[ -z "$mono_dir" ]]; then
    printf '%s\n' 'Unity Mono compiler unavailable. Pass Unity editor path or set UNITY_EDITOR.' >&2
    exit 1
fi

test_dir="$(mktemp -d "${TMPDIR:-/tmp}/sneakhouse-rules.XXXXXX")"
trap 'rm -rf -- "$test_dir"' EXIT
"$mono_dir/mono" "$mono_dir/../lib/mono/4.5/mcs.exe" -langversion:latest \
    -out:"$test_dir/campaign-rules.exe" \
    "$project_dir/Assets/SneakHouse/Scripts/CampaignLevels.cs" \
    "$project_dir/Assets/SneakHouse/Scripts/PatrolPlans.cs" \
    "$project_dir/Tests/CampaignRulesTests.cs"
"$mono_dir/mono" "$test_dir/campaign-rules.exe"
"$mono_dir/mono" "$mono_dir/../lib/mono/4.5/mcs.exe" -langversion:latest \
    -out:"$test_dir/score-rules.exe" \
    "$project_dir/Assets/SneakHouse/Scripts/ScoreRules.cs" \
    "$project_dir/Assets/SneakHouse/Scripts/ScoreData.cs" \
    "$project_dir/Tests/ScoreRulesTests.cs"
"$mono_dir/mono" "$test_dir/score-rules.exe"
