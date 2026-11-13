#!/bin/bash
# Stop: run tests before Claude is allowed to finish.
INPUT=$(cat)

# Avoid an endless loop if Claude is already retrying after a failure.
if echo "$INPUT" | grep -q '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi

OUTPUT=$(dotnet test --nologo --verbosity quiet 2>&1)
if [ $? -ne 0 ]; then
  echo "Tests are failing. Fix them before finishing:" >&2
  echo "$OUTPUT" | tail -30 >&2
  exit 2
fi
exit 0
