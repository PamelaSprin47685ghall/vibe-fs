# Compatibility Ledger — temporary cleanup workbench

Policy: Name the creditor. Name the exit. Or delete the debt.

`UNKNOWN` never means keep. Every survivor is either deleted after first-party
migration or bounded to a named external/durable/deployment creditor.

| ID | Surface | Current creditor | Evidence | Writer | Verdict | Exit condition | Owner |
|---|---|---|---|---|---|---|---|
| LEGACY-005 | FactCodec historical decoders (`containsLegacyFallbackFields` pre-0.5.0, `containsLegacyScoreVectorEntry` tip-v1, `containsLegacyUnanchoredGuideline` HOST-013, `containsHandleCompletedMissingCompletionFields` EXEC-009) | historical durable journal bytes | dated local machine result `cleanup/census/current-worktree-result-2026-08-18.json`, produced from `cleanup/census/current-worktree-roots.txt`: 59 journals / 18,916 lines / all four detector counts 0 / roots digest `0cfd1c96…ca306`. This inventory is explicitly local evidence, not the authoritative supported-workspace inventory. | decode-only refuse (no migration; durable-events HOW §7 forbids upgrade to migrator/shim) | BOUNDED-COMPAT | retention horizon + owner-provided supported-workspace inventory census proves no old bytes anywhere → delete detectors + diagnostic tests | persistence |
| LEGACY-006 | Host V1 TodoTable sink (`CompatibilityTodoRow` + `obligationsToCompatibilityRows` + `replaceCompatibilityArgs` + `projectCompatibilityRows`) | OpenCode Host V1 contract (built-in executor still consumes `{todos:[{content,status,priority}]}`) | boundary: `Mission/Obligation/Todo/Surface.fs` + `OpenCode/HostCodec.fs` + `OpenCode/MagicTodoHostSurface.fs` + `MagicTodoMembrane.fs` before-hook; one-way canonical → V1, non-enumerable `todos` | live projection (not bad-data migration; canonical obligation writer alive and correct; sink is optimistic UI state that never round-trips into canonical) | BOUNDED-COMPAT | Host V1 TodoTable removed from supported host contract → delete sink + canaries | host-boundary |

Struck rows (verified deleted 2026-08-17): LEGACY-001 (`ManagerActivation`),
LEGACY-002 (`RunCompletion.AgentId`), LEGACY-003 (`renderCompletedBatch`),
LEGACY-008 (`js-boundary-baseline.json`), LEGACY-009 (`domain.mjs` test facade),
LEGACY-011 (`ITimerHandle` alias), LEGACY-012 (`RuntimeSnapshot` type),
LEGACY-013 (`Journal.Frontier` type), LEGACY-014 (`IJournalWriter.FilePath`),
LEGACY-015 (`SessionRecoveryWorkflow.Ports` alias),
LEGACY-007 (`false abort` migrate `migrateRetiredFalseAbort`/`tryMigrateRetiredFalseAbort`/`migrateOutcomeToUnit`/`appendMigrationFacts`/`migrateRetiredIfFalseAbort` → `DECODE-ONLY REFUSE`, retired path `Error` fail-closed, `decodeBody`→`LegacyFalseAbort` permanent),
LEGACY-010 (`WorkActivated` compat writer `appendLegacyMigrationWorkActivatedCompat` → deleted, fact case inert, ratchet asserts absence),
LEGACY-016 (`JoinPublished` single-result chain `Host.JoinPublished`/`joinPublishedString`/`Runtime.JoinPublished`/`Job.TryJoin` + `host.test.mjs` stub → deleted, canonical `JoinPublishedAvailable`/`JoinPublishedBatch`).

The ledger is not a permanent architecture document. Delete it after all
bounded survivors have named creditors and exit conditions, and after all
unowned rows are removed.
