#!/usr/bin/env bash
# Runs the package's tests headless via the Unity CLI.
# Creates a throwaway host project in .testhost/ on first run (gitignored, reused after).
# Usage: scripts/test.sh [EditMode|PlayMode] [extra `unity test` args...]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HOST="$ROOT/.testhost"
EDITOR_VERSION="${UNITY_EDITOR_VERSION:-6000.6.0f1}"
MODE="${1:-EditMode}"
shift || true

if [ ! -f "$HOST/Packages/manifest.json" ]; then
  mkdir -p "$HOST/Packages" "$HOST/ProjectSettings" "$HOST/Assets"
  cat > "$HOST/Packages/manifest.json" <<'EOF'
{
  "dependencies": {
    "com.juris.abilities": "file:../..",
    "com.unity.inputsystem": "1.20.0",
    "com.unity.test-framework": "1.4.6"
  },
  "testables": ["com.juris.abilities"]
}
EOF
  echo "m_EditorVersion: $EDITOR_VERSION" > "$HOST/ProjectSettings/ProjectVersion.txt"
fi

unity test "$HOST" --mode "$MODE" --output "$HOST/test-results-$MODE.xml" "$@"
