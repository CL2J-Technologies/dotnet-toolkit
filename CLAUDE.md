# dotnet-toolkit

## Language — English only

Write **everything** in English: code, identifiers, comments, XML documentation, test names,
documentation files, commit messages, pull request titles and descriptions, issue titles and
bodies.

This holds even when the conversation is in French, and even when the surrounding file already
contains French comments. Parts of this codebase predate the rule; match the rule, not the
neighbouring lines.

When you edit a method whose comments are in French, translate them as part of that change. A
deliberate sweep is fine too, but give it its own pull request — mixed into a behaviour change it
buries the part a reviewer needs to read.

## Issues

- Every issue has a type: `Feature`, `Bug`, `Backlog Item` or `Idea`.
- Every `Bug` and `Backlog Item` has a parent of type `Feature`, set at creation:
  `gh issue create --title "..." --body-file body.md --type "Bug" --parent <feature number>`.
  An issue without a parent is incomplete.
- Only a Feature can be a parent. Features and Ideas need no parent. A Bug or Backlog Item never
  has sub-issues.
- Pick the Feature of the area touched, not the one of the current task. List them with
  `gh issue list --search "type:Feature state:open"`. When unsure, ask.
- PRs and commits use `Refs #n`, never `Closes`, `Fixes` or `Resolves`.
- The board is project 5, statuses `Todo` / `In Progress` / `Done`. An issue moves to
  `In Progress` when a PR addresses it, and is closed and set to `Done` after the merge.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full conventions — tests, versioning, and security
reporting.
