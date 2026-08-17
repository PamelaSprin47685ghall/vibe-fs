# Compatibility Ledger — temporary cleanup workbench

Policy: Name the creditor. Name the exit. Or delete the debt.

`UNKNOWN` never means keep. Every survivor is either deleted after first-party
migration or bounded to a named external/durable/deployment creditor.

| ID | Surface | Current creditor | Evidence | Writer | Verdict | Exit condition | Owner |
|---|---|---|---|---|---|---|---|
| LEGACY-005 | FactCodec historical decoders | historical durable journal bytes | per-migration inventory required | decode only | BOUNDED-COMPAT | retention horizon/sample inventory proves no old bytes | persistence |
| LEGACY-006 | Host V1 TodoTable sink | OpenCode Host V1 contract | current Host V1 projection contract | no canonical writer | BOUNDED-COMPAT | Host V1 TodoTable removed from supported host contract | host-boundary |
| LEGACY-007 | `false abort` runtime migration | historical deployment journals with retired false-abort tombstones (zero real samples in 48-journal census 2026-08-17) | no production writer of aborted finality (`encodeOutcome` has no aborted branch; architecture gate `codec-encode-finality-aborted` enforces); 48-journal census: 0 aborted blobs, 0 migration facts ever fired → bad-data set observably empty → migrate collapsed to fail-closed refuse | no (decode-only detect + fail-closed refuse) | DECODE-ONLY REFUSE | complete; migrate → refuse cutover executed 2026-08-17; `decodeBody` → `LegacyFalseAbort` detect permanent; retired path returns Error (fail-closed); deleted `migrateRetiredFalseAbort` / `tryMigrateRetiredFalseAbort` / `migrateOutcomeToUnit` / `appendMigrationFacts` / `migrateRetiredIfFalseAbort` / `FalseTerminalMigration` | delegation |
| LEGACY-010 | `WorkActivated` production writer (`appendLegacyMigrationWorkActivatedCompat`) | e2e long-stroke scenario (`waitFact WorkActivated eq 1` + `assertJoinWakePath countFactCase >= 1`) | `long-stroke.toml:184`, `long-stroke-oracles.mjs:385` | `appendLegacyMigrationWorkActivatedCompat` in `Workflow.fs` (private, called only from `materializeInitialAgentOwnerLife`) | BOUNDED-COMPAT | long-stroke scenario updated to not require `WorkActivated` → delete compat function + call; `acceptActivation` / `applyAcceptedActivation` / wire Activation detection already deleted (no creditor) | mission-manager |
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
