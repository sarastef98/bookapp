#!/bin/bash
# PostToolUse: format the edited C# file.
INPUT=$(cat)
FILE=$(echo "$INPUT" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | sed 's#\\\\#/#g')

case "$FILE" in
  *.cs) dotnet format --include "$FILE" >/dev/null 2>&1 ;;
esac
exit 0
