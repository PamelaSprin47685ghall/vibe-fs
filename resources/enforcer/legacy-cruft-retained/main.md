# legacy-cruft-retained — Main

## What To Do Now
Delete the obsolete surface the clean-break decision already retired: aliases, adapters, old names, compatibility branches, and legacy formats.

## Why This Matters
A migration that keeps its predecessor alive has not reduced complexity; it has merely renamed one side “legacy.” Every surviving branch remains a supported possibility until code makes it impossible, so tests, docs, and future refactors must keep asking whether the old world still matters.

## Repair Strategy
Use the clean-break decision as authority, migrate any remaining repository-owned callers, and remove the old representation completely. Record only explicit external exceptions with a retirement condition.

## Wrong Fixes
Do not keep a hidden alias “for safety,” a deprecated parser “just in case,” or commented guidance for the old path. Those reintroduce ambiguity the clean break was meant to eliminate.

## Verification
Search for old names and formats, exercise the canonical path, and ensure architecture/tests contain no fallback that silently accepts the retired surface.

## Done When
The codebase supports the post-break world only, and version control—not production code—carries the memory of what came before.
