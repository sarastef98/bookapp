#!/bin/bash
# PreToolUse: block edits to existing migrations and production config.
INPUT=$(cat)
FILE=$(echo "$INPUT" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | sed 's#\\\\#/#g')

if echo "$FILE" | grep -q "appsettings.Production.json"; then
  echo "Blocked: do not edit appsettings.Production.json (see CLAUDE.md)." >&2
  exit 2
fi

if echo "$FILE" | grep -q "/Migrations/" && [ -f "$FILE" ]; then
  echo "Blocked: never edit an existing migration. Add a new one with dotnet ef migrations add." >&2
  exit 2
fi
exit 0
