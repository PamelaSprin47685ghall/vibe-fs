# Compatibility Ledger — temporary cleanup workbench

Policy: Name the creditor. Name the exit. Or delete the debt.

`UNKNOWN` never means keep. Every survivor is either deleted after first-party
migration or bounded to a named external/durable/deployment creditor.

| ID | Surface | Current creditor | Evidence | Writer | Verdict | Exit condition | Owner |
|---|---|---|---|---|---|---|---|
| LEGACY-005 | FactCodec historical decoders (`containsLegacyFallbackFields` pre-0.5.0, `containsLegacyScoreVectorEntry` tip-v1, `containsLegacyUnanchoredGuideline` HOST-013, `containsHandleCompletedMissingCompletionFields` EXEC-009) | historical durable journal bytes | 48-journal census 2026-08-17: pre-0.5.0 markers 0/0, `ScoreVectorRef` 0 (all 38 `BlogObservationCommitted` carry `TipRuleId`), unanchored `PairProgrammingGuidelineAppended` 0, `HandleCompleted` missing `CompletionRef`/`CompletionDigest` 0 of 142 | decode-only refuse (no migration; durable-events HOW §7 forbids upgrade to migrator/shim) | BOUNDED-COMPAT | retention horizon + external-workspace census proves no old bytes anywhere → delete detectors + diagnostic tests | persistence |
| LEGACY-006 | Host V1 TodoTable sink (`CompatibilityTodoRow` + `obligationsToCompatibilityRows` + `replaceCompatibilityArgs` + `projectCompatibilityRows`) | OpenCode Host V1 contract (built-in executor still consumes `{todos:[{content,status,priority}]}`) | boundary: `Mission/Obligation/Todo/Surface.fs` + `OpenCode/HostCodec.fs` + `OpenCode/MagicTodoHostSurface.fs` + `MagicTodoMembrane.fs` before-hook; one-way canonical → V1, non-enumerable `todos` | live projection (not bad-data migration; canonical obligation writer alive and correct; sink is optimistic UI state that never round-trips into canonical) | BOUNDED-COMPAT | Host V1 TodoTable removed from supported host contract → delete sink + canaries | host-boundary |
| LEGACY-007 | `false abort` runtime migration | historical deployment journals with retired false-abort tombstones (zero real samples in 48-journal census 2026-08-17) | no production writer of aborted finality (`encodeOutcome` has no aborted branch; p0 gate `codec-encode-finality-aborted` in `scripts/checks/p0-recovery-join.mjs` enforces); 48-journal census 2026-08-17: 0 aborted blobs, 0 migration facts ever fired → bad-data set observably empty → migrate collapsed to fail-closed refuse | no (decode-only detect + fail-closed refuse) | DECODE-ONLY REFUSE | complete; migrate → refuse cutover executed 2026-08-17; `decodeBody` → `LegacyFalseAbort` detect permanent; retired path returns Error (fail-closed); deleted `migrateRetiredFalseAbort` / `tryMigrateRetiredFalseAbort` / `migrateOutcomeToUnit` / `appendMigrationFacts` / `migrateRetiredIfFalseAbort` / `FalseTerminalMigration`; p0 gate `parent-join-correction-fact` relabelled "retired false-abort migration" → "legacy false-abort compensation replay" (`p0-recovery-join.mjs`, commit 9c5486bb0) | delegation |
| LEGACY-010 | `WorkActivated` production writer (`appendLegacyMigrationWorkActivatedCompat`) | none | `long-stroke.toml` waitFact WorkActivated removed; `long-stroke-oracles.mjs` `assertLaterSuccessfulFinality` no longer counts WorkActivated; `PLANNED_WAIT_FACTS.workActivated` preset removed | no | DELETE | complete; compat function + call in `materializeInitialAgentOwnerLife` deleted 2026-08-17; `WorkActivated` fact case (Facts.fs) + decode (Projection.fs) remain as inert decode-only permanent legacy; ratchet test `work-activated-writer-ratchet.test.mjs` now asserts writer absent | mission-manager |
| LEGACY-016 | `JoinPublished` compatibility single-result join chain (`OrchestratorHost.JoinPublished` + `joinPublishedString` + `Orchestrator.JoinPublished` + `VerdictMailbox.TryJoin`) | none | `rg JoinPublished src` → only canonical `JoinPublishedAvailable`/`JoinPublishedBatch` remain; zero bare `JoinPublished()` calls; `TryJoin` → 0; `joinPublishedString` → 0; `requirements/**/Surface.fs` `JoinPublished` → 0; stub test `host.test.mjs` `HOST_JoinPublished_renders_a_string` removed | no | DELETE | complete; `Host.fs` `joinPublishedString` + `member JoinPublished()`, `Runtime.fs` `member JoinPublished()`, `Job.fs` `member TryJoin()`, and stub test deleted; PROOF.md CHGINT-011 + HOW.md + `ToolRuntimeScope.fs` comment updated | change |

Struck rows (verified deleted 2026-08-17): LEGACY-001 (`ManagerActivation`),
LEGACY-002 (`RunCompletion.AgentId`), LEGACY-003 (`renderCompletedBatch`),
LEGACY-008 (`js-boundary-baseline.json`), LEGACY-009 (`domain.mjs` test facade),
LEGACY-011 (`ITimerHandle` alias), LEGACY-012 (`RuntimeSnapshot` type),
LEGACY-013 (`Journal.Frontier` type), LEGACY-014 (`IJournalWriter.FilePath`),
LEGACY-015 (`SessionRecoveryWorkflow.Ports` alias).

The ledger is not a permanent architecture document. Delete it after all
bounded survivors have named creditors and exit conditions, and after all
unowned rows are removed.
