# Compatibility Ledger — temporary cleanup workbench

Policy: Name the creditor. Name the exit. Or delete the debt.

`UNKNOWN` never means keep. Every survivor is either deleted after first-party
migration or bounded to a named external/durable/deployment creditor.

| ID | Surface | Current creditor | Evidence | Writer | Verdict | Exit condition | Owner |
|---|---|---|---|---|---|---|---|
| LEGACY-001 | `ManagerActivation` vocabulary | none | repository search: zero production callers | no | DELETE | immediate; implementation/docs removed | mission-manager |
| LEGACY-002 | `RunCompletion.AgentId` field | none | repository search: field absent; canonical Map/AgentName callers | no | DELETE | immediate; no type/codec/fixture occurrence | execution |
| LEGACY-003 | `JoinResultRenderer.renderCompletedBatch` | none | repository search: zero callers after canonical `renderJoinItemBatch` migration | no | DELETE | immediate; renderer/support/tests removed | delegation |
| LEGACY-005 | FactCodec historical decoders | historical durable journal bytes | per-migration inventory required | decode only | BOUNDED-COMPAT | retention horizon/sample inventory proves no old bytes | persistence |
| LEGACY-006 | Host V1 TodoTable sink | OpenCode Host V1 contract | current Host V1 projection contract | no canonical writer | BOUNDED-COMPAT | Host V1 TodoTable removed from supported host contract | host-boundary |
| LEGACY-007 | `false abort` runtime migration | historical deployment journals with retired false-abort tombstones (zero real samples in 26-journal census 2026-08-16) | no production writer of aborted finality (`encodeOutcome` has no aborted branch; architecture gate `codec-encode-finality-aborted` enforces); retired handles cannot be simply rejected (`rejectFalseCompletion` returns `HandleIsRetired` → fold no-op; EXEC-009 tombstone is permanent); replacement migration is the only way to reopen the join window for a child whose handle was already retired on a false abort | no (decode-only + one-shot idempotent migration) | BOUNDED-COMPAT | census/instrumentation proves zero observable retired false-abort tombstones across all deployments → delete `migrateRetiredFalseAbort`/`tryMigrateRetiredFalseAbort`/`migrateOutcomeToUnit`/`appendMigrationFacts` + `Restart.fs` `migrateRetiredIfFalseAbort`/`migrateFromBlob`/`migrateIfLegacyAbort` + `reconcileFalseAborts` retired branch, keep detect → refuse | execution |
| LEGACY-008 | `js-boundary-baseline.json` | none | scanner debt reached absolute zero; baseline file deleted | no | DELETE | complete; ledger disappeared after zero debt | js-surface |
| LEGACY-009 | `verification-system/tests/support/domain.mjs` | none as a test contract | repository search: zero semantic-zone imports; all callers use production owner surfaces | no | DELETE | facade and family adapters deleted after zero-consumer proof | semantic-owners |
| LEGACY-010 | `WorkActivated` production writer (`appendLegacyMigrationWorkActivatedCompat`) | e2e long-stroke scenario (`waitFact WorkActivated eq 1` + `assertJoinWakePath countFactCase >= 1`) | `long-stroke.toml:184`, `long-stroke-oracles.mjs:385` | `appendLegacyMigrationWorkActivatedCompat` in `Workflow.fs` (private, called only from `materializeInitialAgentOwnerLife`) | BOUNDED-COMPAT | long-stroke scenario updated to not require `WorkActivated` → delete compat function + call; `acceptActivation` / `applyAcceptedActivation` / wire Activation detection already deleted (no creditor) | mission-manager |
| LEGACY-011 | `Process.PtyTiming.ITimerHandle` alias | none | repository search: zero source/requirements consumers; all callers use `IDeadlineHandle` | no | DELETE | immediate; alias and transition comment removed | process |
| LEGACY-012 | `Composition.Durable.RuntimeSnapshot` type | none | repository search: zero production consumers; only `guide-contract.test.mjs` pinned it as emitted surface; `Frontier` field was sole consumer of `Persistence.Journal.Frontier` | no | DELETE | complete; type and guide-contract pin removed | persistence |
| LEGACY-013 | `Persistence.Journal.Frontier` type (`Map<RuntimeId, int64>`) | none | sole consumer was `RuntimeSnapshot.Frontier` (LEGACY-012); comment self-described as "legacy NDJSON boot field retained for RuntimeSnapshot shape" | no | DELETE | complete; type and legacy comment removed | persistence |
| LEGACY-014 | `IJournalWriter.FilePath` member | none | implemented as `""` in `EventStoreJournalWriter`; repository search: zero reads of `writer.FilePath` / `journal.FilePath` in production or tests | no | DELETE | complete; interface member, implementation, and interface-impl line removed | persistence |
| LEGACY-015 | `SessionRecoveryWorkflow.Ports` alias | none | repository search: zero callers; comment referenced `AttachFamilyRecoveryPorts` which does not exist in source; all call sites use `SessionRecoveryPorts` | no | DELETE | complete; alias and stale comment removed | recovery |

The ledger is not a permanent architecture document. Delete it after all
bounded survivors have named creditors and exit conditions, and after all
unowned rows are removed.
