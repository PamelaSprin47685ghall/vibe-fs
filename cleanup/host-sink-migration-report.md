# Host TodoTable Compatibility Sink & Runtime Migration Audit

Audit date: 2026-08-17
Scope: `Surface.CompatibilityTodoRow` / `obligationsToCompatibilityRows` /
`HostCodec.replaceCompatibilityArgs` sink, and every runtime migration that rewrites
or refuses historical bad data on the Host boundary (`JoinDrain` false-abort,
`WorkActivated` compat writer, `FactCodec` decode-only refusals).
No code edits — report only.

---

## 1. Method

1. Read the normative creditor/exit clauses:
   `requirements/obligation-ledger/WHAT.md` OBLIGATION-LEDGER-015 (Host V1 sink),
   `requirements/effect-accounting/WHAT.md` EFFECT-ACCOUNTING-007 (false abort),
   `requirements/durable-events/HOW.md` §7 (FactCodec decode-only ingress),
   `requirements/finality/HOW.md` + `tests/work-activated-writer-ratchet.test.mjs`
   (WorkActivated LEGACY-010).
2. Read every implementation site (see §4 inventory).
3. Ran a fresh census over `.git/wanxiang/events/` (48 journals) for each bad-data
   marker, distinguishing **fact envelope lines** from **blob content** inside
   `JsTransactionPrepared` mutations (the latter are file text, not facts).

---

## 2. Census evidence (2026-08-17, 48 journals)

Previous census: 26 journals (2026-08-16, cited in legacy-ledger LEGACY-007 and
EFFECT-ACCOUNTING-007). Current: **48 journals**.

| Probe | Fact-line hits | Notes |
|---|---|---|
| `status:"aborted"` completion blob | 0 | no LegacyFalseAbort blob bodies |
| `finality:"aborted"` completion blob | 0 | clean-break v2 never writes aborted |
| `HandleFalseCompletionRejected` fact | 0 | unretired reject path never fired |
| `HandleFalseTerminalReported` fact | 0 | retired migration never fired |
| `ParentJoinCorrectionRequested` fact | 0 | replacement handle never minted |
| pre-0.5.0 markers (`FailuresOnCurrentSide` … `DurableEffectAccepted`) | 0 | `containsLegacyFallbackFields` would refuse |
| `ScoreVectorRef` in fact lines | 0 | all 38 `BlogObservationCommitted` carry `TipRuleId` |
| `BlogEntryCommitted` (legacy tag) | 0 | tip-v2 clean break complete |
| `PairProgrammingGuidelineAppended` | 0 | HOST-013 anchored clean break complete |
| `HandleCompleted` missing `CompletionRef`/`CompletionDigest` | 0 of 142 | EXEC-009 clean break complete |
| `WorkActivated` fact | 1 | the long-stroke e2e journal only |

**Conclusion**: every runtime migration's bad-data set is observably empty in the
local census. The only live compat writer (`WorkActivated`) fires exactly once, in
the e2e scenario it exists to serve.

---

## 3. Per-migration Creditor / Boundary / Exit table

| ID | Migration surface | Creditor (who needs it) | Boundary (where it lives) | Writer dead? | Finite bad-data set? | Exit condition | Current verdict |
|---|---|---|---|---|---|---|---|
| **M1** | Host V1 TodoTable sink: `CompatibilityTodoRow`, `obligationsToCompatibilityRows`, `HostCodec.replaceCompatibilityArgs`, `MagicTodoHostSurface.projectCompatibilityRows`, MagicTodoMembrane before-hook projection | OpenCode Host V1 TodoTable contract (the built-in executor still consumes `{todos:[{content,status,priority}]}`) | `Mission/Obligation/Todo/Surface.fs` + `OpenCode/HostCodec.fs` + `OpenCode/MagicTodoHostSurface.fs` + `MagicTodoMembrane.fs` before-hook; one-way canonical → V1, non-enumerable `todos` | N/A — not a bad-data migration; it is a **live projection** written every checkpoint | N/A — no stored bad data; optimistic UI state only, never round-trips into canonical | Host V1 TodoTable removed from supported host contract → delete `CompatibilityTodoRow` / `obligationsToCompatibilityRows` / `replaceCompatibilityArgs` / V1 canaries | BOUNDED-COMPAT (live sink, not decode-only) |
| **M2** | false-abort retired-handle replacement: `migrateRetiredFalseAbort`, `tryMigrateRetiredFalseAbort`, `migrateOutcomeToUnit`, `appendMigrationFacts`, `reconcileFalseAborts`, `FalseTerminalMigration.replacementHandle`; plus `HostForkRestart.migrateRetiredIfFalseAbort` entry | Historical deployment journals with retired false-abort tombstones (zero real samples in 48-journal census) | `Execution/Delegation/Handle/JoinDrain.fs` + `Fork/ChildRecovery.fs` (`FalseTerminalMigration`) + `Fork/Host/Restart.fs`; decode-only detect (`decodeBody` → `LegacyFalseAbort`) is permanent; the **migrate** branch is the bounded-compat part | **Yes** — `encodeOutcome` has no aborted branch; gate `codec-encode-finality-aborted` enforces; `AgentJoinItem` has no aborted case | **Yes** — bad data = historical `SendFailure`/aborted blob on a *retired* handle; writer dead ⇒ finite and non-growing | census/instrumentation proves zero observable retired false-abort tombstones across all deployments → delete `migrateRetiredFalseAbort` / `tryMigrateRetiredFalseAbort` / `migrateOutcomeToUnit` / `appendMigrationFacts` / `reconcileFalseAborts` retire-side branch, **keep** `decodeBody` detect → refuse | BOUNDED-COMPAT (decode + one-shot idempotent migrate) |
| **M3** | `WorkActivated` compat writer: `appendLegacyMigrationWorkActivatedCompat` in `Workflow.fs`, called only from `materializeInitialAgentOwnerLife` | e2e long-stroke scenario: `long-stroke.toml:184` (`waitFact WorkActivated eq 1`) + `long-stroke-oracles.mjs:385` (`countFactCase WorkActivated >= 1`) | `Mission/Manager/Life/Workflow.fs` (private function + single call site); the `WorkActivated` **decode** in `Projection.fs` is inert-legacy and permanent | Canonical writer dead (`acceptActivation`/`applyAcceptedActivation` deleted, ratcheted by `work-activated-writer-ratchet.test.mjs`); **compat writer is alive** — fires on every AgentOwnerRoot migration Life | **No** — not bad-data repair; it is a live writer serving a test oracle. The "finite set" is the set of e2e runs, not historical data | long-stroke scenario updated to not require `WorkActivated` → delete compat function + call; decode stays | BOUNDED-COMPAT (live compat writer, test creditor) |
| **M4a** | `containsLegacyFallbackFields` → `pre050MigrationMessage` | operators who may still hold pre-0.5.0 runtime journals | `Persistence/Journal/FactCodec.fs` + `Envelope.fs` ingress; decode-only **refuse** (no migration) | Yes — current writer emits canonical shapes only | Yes — pre-0.5.0 journals only; 0 in census | retention horizon + external workspace census proves no pre-0.5.0 bytes → delete detection + diagnostic tests | DECODE-ONLY REFUSE (already) |
| **M4b** | `containsLegacyScoreVectorEntry` → `tipV2CleanBreakMessage` | historical tip-v1 observation/entry bytes | `FactCodec.fs` + `BlogSurface.fs`; decode-only refuse | Yes | Yes — 0 in census (all 38 `BlogObservationCommitted` carry `TipRuleId`) | all supported workspaces complete tip-v2 clean break + no old bytes → delete | DECODE-ONLY REFUSE (already) |
| **M4c** | `containsLegacyUnanchoredGuideline` → `legacyGuidelineCleanBreakMessage` | historical unanchored `PairProgrammingGuidelineAppended` bytes | `FactCodec.fs`; decode-only refuse | Yes | Yes — 0 in census | HOST-013 retention horizon + census no old bytes → delete | DECODE-ONLY REFUSE (already) |
| **M4d** | `containsHandleCompletedMissingCompletionFields` → explicit refusal | historical `HandleCompleted` lacking `CompletionRef`/`CompletionDigest` | `FactCodec.fs`; decode-only refuse | Yes | Yes — 0 of 142 in census | EXEC-009 retention horizon + census no old bytes → delete | DECODE-ONLY REFUSE (already) |

---

## 4. Implementation inventory (verified sites)

**M1 — Host V1 TodoTable sink**
- `src/Wanxiangshu/Mission/Obligation/Todo/Surface.fs:90` — `CompatibilityTodoRow` type
- `Surface.fs:98` — `obligationsToCompatibilityRows` (canonical → V1 row projection)
- `src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/HostCodec.fs:191` —
  `replaceCompatibilityArgs` (non-enumerable `todos` on `output.args`)
- `OpenCode/MagicTodoHostSurface.fs:44` — `projectCompatibilityRows` (JS surface re-projection)
- `MagicTodoMembrane.fs:879` — before-hook calls `obligationsToCompatibilityRows` then
  `replaceCompatibilityArgs` (the single live projection site)
- Normative: OBLIGATION-LEDGER-015 (canonical single truth vs Host compat sink;
  Exit = Host V1 TodoTable leaves supported host contract).

**M2 — false-abort runtime migration**
- `src/Wanxiangshu/Execution/Delegation/Handle/JoinDrain.fs:202` — `migrateRetiredFalseAbort`
- `JoinDrain.fs:428` — `tryMigrateRetiredFalseAbort` (public; called by Restart)
- `JoinDrain.fs` — `migrateOutcomeToUnit`, `appendMigrationFacts`
  (`HandleFalseTerminalReported` + `HandleLinked` replacement + `ParentJoinCorrectionRequested`),
  `reconcileFalseAborts`, `rejectUnretiredFalseAbort` (the already-refuse path)
- `Fork/ChildRecovery.fs:173` — `FalseTerminalMigration.replacementHandle` / `replacementAgentId`
  (deterministic `recovery:<agent>:<digest>`)
- `Fork/Host/Restart.fs:146` — `migrateRetiredIfFalseAbort` → `tryMigrateRetiredFalseAbort`
  (second entry: restart recovery of retired handles)
- `Handle/CompletionCodec.fs:199` — `decodeBody` → `LegacyFalseAbort` (permanent detect)
- Gate: `scripts/checks/p0-recovery-join.mjs:53` `codec-encode-finality-aborted` (encode never writes aborted)
- Normative: EFFECT-ACCOUNTING-007 (aborted ≠ terminal; Exit = zero observable bad data →
  delete migrate, keep detect → refuse).

**M3 — WorkActivated compat writer**
- `src/Wanxiangshu/Mission/Manager/Life/Workflow.fs:133` — `appendLegacyMigrationWorkActivatedCompat`
- `Workflow.fs:179` — single call site inside `materializeInitialAgentOwnerLife`
- `Mission/Manager/Life/Projection.fs` — `WorkActivated` inert-legacy decode (permanent)
- Ratchet: `requirements/finality/tests/work-activated-writer-ratchet.test.mjs`
  (only this compat function may write `WorkActivated`)
- Creditor sites: `verification-system/tests/e2e/scenarios/long-stroke.toml:184`,
  `verification-system/tests/e2e/scenarios/support/long-stroke-oracles.mjs:385`
- Normative: OBLIGATION-LEDGER-017 + finality HOW (LEGACY-010).

**M4 — FactCodec decode-only refusals**
- `src/Wanxiangshu/Persistence/Journal/FactCodec.fs:71/78/97/105` — the four `containsLegacy*` detectors
- `FactCodec.fs:155` — `deserializeFact` refusal chain
- `Persistence/Journal/Envelope.fs:93` — `deserialize` refusal chain
- `Enforcer/BlogSurface.fs:231` + `Persistence/Journal/FactCodecSurface.fs:188` — JS surface re-exports
- Normative: durable-events HOW §7 (decode-only bounded compat; never upgrade to migrator/shim).

---

## 5. Per-migration analysis (version / writer / finite / repair-or-refuse)

### M1 — Host V1 TodoTable sink
- **Which version bad data?** None. This is not a data migration; it is a live,
  per-checkpoint projection of canonical obligations into the Host's legacy
  `{todos:[{content,status,priority}]}` sink shape. The sink is optimistic UI
  state that never round-trips into canonical truth (TODO-007 / OBLIGATION-LEDGER-015).
- **Writer already dead?** N/A. The canonical obligation writer is alive and
  correct; the sink is a read-only projection of it. There is no "bad writer" to kill.
- **Finite set?** N/A — no stored bad data.
- **Offline repair or refuse?** Neither. It is a live compatibility projection
  whose legitimacy is bounded by the Host V1 contract, not by a data horizon.
- **Recommendation**: KEEP as BOUNDED-COMPAT until the Host V1 TodoTable leaves
  the supported host contract, then DELETE the sink + canaries. Do not convert to
  decode-only (there is nothing to decode) and do not DELETE now (the Host still
  consumes the V1 shape).

### M2 — false-abort retired-handle replacement
- **Which version bad data?** Historical completion blobs carrying
  `status:"aborted"` or `finality:"aborted"`, produced before the clean-break
  cutover when `encodeOutcome` had an aborted branch. The damaging combination is
  an aborted blob landing on a handle that was *retired* on that false terminal —
  the EXEC-009 tombstone is permanent, so simple rejection is a fold no-op.
- **Writer already dead?** **Yes.** `encodeOutcome` has no aborted branch
  (`CompletionCodec.fs:44`); `AgentJoinItem` has no aborted case; the
  `codec-encode-finality-aborted` gate enforces both. No production path can
  produce a new aborted finality blob.
- **Finite set?** **Yes.** Writer dead ⇒ the bad-data set is closed: historical
  `SendFailure`/aborted-blob-on-retired-handle combinations only. 48-journal
  census: 0 aborted blobs, 0 migration facts ever fired.
- **Offline repair or refuse?**
  - The **unretired** path already refuses: `rejectUnretiredFalseAbort` appends
    `HandleFalseCompletionRejected` → fold reverts to Active → no join item. This
    is already decode-only refuse and should stay.
  - The **retired** path migrates: it mints a deterministic replacement handle
    (`recovery:<agent>:<digest>`) and appends correction facts. This is the only
    non-refuse branch. Because the bad-data set is observably empty and the writer
    is dead, the retired path **can become refuse** (detect `LegacyFalseAbort` on
    a retired handle → fail-closed / refuse, do not mint a replacement). The
    replacement exists solely to reopen a join window for a child whose handle was
    retired on a false abort — but no such child exists in any observed journal.
- **Recommendation**: **→ decode-only REFUSE.** Collapse the retired branch from
  migrate to refuse (fail-closed with an actionable "legacy false-abort tombstone
  on retired handle; archive or remove the affected journal" message), matching
  the M4 pattern. Keep `decodeBody` → `LegacyFalseAbort` detection permanently
  (EFFECT-ACCOUNTING-007 forbids aborted finality forever). Delete
  `migrateRetiredFalseAbort` / `tryMigrateRetiredFalseAbort` / `migrateOutcomeToUnit`
  / `appendMigrationFacts` / the retire-side branch of `reconcileFalseAborts` /
  `HostForkRestart.migrateRetiredIfFalseAbort` once the refuse cutover is made.
  The 48-journal census (zero fired) is the evidence that no real child is lost.

### M3 — WorkActivated compat writer
- **Which version bad data?** None. This is a **live writer**, not bad-data
  repair. It appends an inert legacy `WorkActivated` fact during AgentOwnerRoot
  migration-Life materialization so the e2e long-stroke scenario replays
  identically. The fact never decides work, compression, or finality
  (GLORY-014/016..021; OBLIGATION-LEDGER-017).
- **Writer already dead?** The *canonical* writer is dead
  (`acceptActivation`/`applyAcceptedActivation` deleted, ratcheted). The *compat*
  writer is **alive** — it is the sole remaining `WorkActivated` writer, called
  from exactly one place.
- **Finite set?** No — it fires on every AgentOwnerRoot migration Life, not on a
  closed set of historical bytes. The "finite set" is the set of e2e long-stroke
  runs, which is a test artifact, not data.
- **Offline repair or refuse?** Neither — it is test-creditor debt, not data debt.
- **Recommendation**: **DELETE** the compat writer + its call site once the
  long-stroke oracle is updated to not `waitFact WorkActivated eq 1` /
  `countFactCase WorkActivated >= 1`. The `WorkActivated` **decode** in
  `Projection.fs` stays (inert legacy fact, permanent). This is a one-shot test
  edit, not a migration cutover. Until that oracle edit lands, KEEP as
  BOUNDED-COMPAT with the existing ratchet test guarding it.

### M4a–M4d — FactCodec decode-only refusals
- **Which version bad data?** pre-0.5.0 journals (M4a), tip-v1 observation bytes
  (M4b), unanchored guideline bytes (M4c), `HandleCompleted` missing completion
  fields (M4d).
- **Writer already dead?** **Yes** for all four — current writer produces
  canonical shapes only.
- **Finite set?** **Yes** for all four — historical journals only; 0 in 48-journal
  census (fact-line probes, blob-content false positives excluded).
- **Offline repair or refuse?** **Already refuse.** Each detector returns an
  actionable archive-or-remove message; none migrate. These are the reference
  pattern for what M2 should become.
- **Recommendation**: **KEEP as decode-only REFUSE** until the retention horizon
  / external-workspace census proves no old bytes anywhere, then DELETE the
  detectors + diagnostic tests. Do not upgrade to migrators (durable-events HOW §7
  forbids it).

---

## 6. Recommendations summary — decode-only vs DELETE

| Migration | Recommendation | Action |
|---|---|---|
| **M1** Host V1 TodoTable sink | **KEEP (live compat)** | BOUNDED-COMPAT until Host V1 contract dropped; then DELETE sink + canaries. Not a migration — do not convert to decode-only. |
| **M2** false-abort retired replacement | **→ decode-only REFUSE** | Collapse retire-side migrate → refuse (fail-closed + actionable message); keep `decodeBody` detect permanently; delete `migrateRetiredFalseAbort` / `tryMigrateRetiredFalseAbort` / `migrateOutcomeToUnit` / `appendMigrationFacts` / retire branch of `reconcileFalseAborts` / `migrateRetiredIfFalseAbort`. 48-journal census = zero fired ⇒ safe. |
| **M3** WorkActivated compat writer | **DELETE (after test edit)** | Update long-stroke oracle to not require `WorkActivated`; then delete `appendLegacyMigrationWorkActivatedCompat` + call. Keep inert decode. |
| **M4a** pre-0.5.0 fields | **KEEP decode-only REFUSE** | Already refuse; delete after retention-horizon census. |
| **M4b** tip-v1 ScoreVectorRef | **KEEP decode-only REFUSE** | Already refuse; 0 in census; delete after horizon. |
| **M4c** unanchored guideline | **KEEP decode-only REFUSE** | Already refuse; 0 in census; delete after horizon. |
| **M4d** HandleCompleted missing fields | **KEEP decode-only REFUSE** | Already refuse; 0 of 142 in census; delete after horizon. |

**Net**: one migration (M2) should be converted from migrate to decode-only refuse;
one compat writer (M3) should be deleted after its test creditor is updated; the
four FactCodec refusals (M4) are already decode-only and await only retention-horizon
deletion; the Host V1 sink (M1) is a live projection, not a migration, and stays
until the Host contract changes.

---

## 7. Ledger cross-reference

| Report ID | legacy-ledger row | Normative clause |
|---|---|---|
| M1 | LEGACY-006 | OBLIGATION-LEDGER-015 |
| M2 | LEGACY-007 | EFFECT-ACCOUNTING-007 |
| M3 | LEGACY-010 | OBLIGATION-LEDGER-017 / finality HOW |
| M4a–M4d | LEGACY-005 | durable-events HOW §7 |

The 48-journal census (2026-08-17) strengthens every "zero real samples" exit
claim: M2's migration facts and M4's refusal targets remain at zero across a
near-doubling of the journal population since the 26-journal census.
