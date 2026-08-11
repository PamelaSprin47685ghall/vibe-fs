# truncation-skips-damaged — Enforcer

## Definition
Recovery skips damage when it encounters corruption inside durable history, discards or bypasses the damaged region, and continues applying later records as though the missing prefix still had a defined meaning.

## Governing Principle
An ordered log gives later facts meaning relative to the prefix before them. Interior corruption removes part of that premise, so subsequent replay no longer has a trustworthy starting state. A final incomplete record is different: if the storage contract permits torn tail writes, truncating only that uncommitted suffix preserves a complete committed prefix. Interior damage breaks the chain of derivation itself.

## Trigger When
Trigger when recovery ignores malformed/checksum-failed/missing records in the middle of durable history and resumes at later entries.

## Do Not Trigger When
Do not trigger when only the final record is provably incomplete under the storage protocol and recovery truncates precisely that uncommitted tail while preserving the verified prefix.

## Distinguish From
overwrite-history deliberately edits past facts. partial-write-assumption invents failure states. This rule concerns proceeding after actual interior historical evidence is no longer trustworthy.

## Decision Procedure
Locate the first damaged byte/record and determine whether a verified committed record follows it. If yes, fail closed: the later history cannot be interpreted safely without the missing prefix.

## Nudge
A log is a causal chain, not a bag of records. Truncate only a provably incomplete tail; interior corruption destroys the premise of every later replay step and must fail closed.
