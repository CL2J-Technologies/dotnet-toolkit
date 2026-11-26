#!/usr/bin/env bash
# Fails when a change to cl2j.Tooling or cl2j.Scripting does not bump the Database family (#93).
#
# cl2j.Database references both, and `dotnet pack` pins a ProjectReference at the referenced
# project's version. A Tooling or Scripting change that ships without a new Database family version
# leaves the Database packages resolving the old dependency (the stale floors of #17).
#
# Usage: check-database-version.sh <base-ref>     e.g. origin/main
#
# Only files that were added, copied, modified or deleted count: a pure move or rename is not a
# change to what ships. Tests and READMEs sit outside the two library folders and do not count.
set -euo pipefail

base=${1:?usage: check-database-version.sh <base-ref>}
family=cl2j.Database/Directory.Build.props

changed=$(git diff --name-only --diff-filter=ACMD "$base"...HEAD -- cl2j.Tooling/cl2j.Tooling cl2j.Scripting/cl2j.Scripting)
if [ -z "$changed" ]; then
  echo "cl2j.Tooling and cl2j.Scripting are unchanged: nothing to check."
  exit 0
fi

version() { sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' | head -n 1; }

new=$(version < "$family")
# Before the family had a file of its own it shared the root version.
if git cat-file -e "$base:$family" 2>/dev/null; then
  old=$(git show "$base:$family" | version)
else
  old=$(git show "$base:Directory.Build.props" | version)
fi

if [ "$old" != "$new" ]; then
  echo "cl2j.Tooling or cl2j.Scripting changed and the Database family moved from $old to $new."
  exit 0
fi

echo "::error file=$family::cl2j.Tooling or cl2j.Scripting changed, but the Database family is still at $new."
echo "Changed files:"
echo "$changed" | sed 's/^/  /'
echo
echo "cl2j.Database references both packages and dotnet pack pins them at the version in force, so the"
echo "Database packages would keep resolving the old dependency. Bump <Version> in $family"
echo "by at least a patch. See Versioning in CONTRIBUTING.md."
exit 1
