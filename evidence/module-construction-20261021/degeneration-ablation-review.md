# MF06 degeneration / ablation receipt

Decision: do not receive either module during this release. This is a deliberate scope decision after comparing production consumers, not completed library integration.

Fixed upstream: `202a41ab39460980ba053e55a9b3780bbaf411ca`.

- `src/degeneration/Degeneration.fs`: SHA256 `6005cd01b5ff08c870e01cfdd2120cfa6fa3701263f4083fae4ea23dde068c3f`.
- `src/ablation/Ablation.fs`: SHA256 `9e396cff4282aedfc07619d13e6576619e5e473c4479a252dc3c12c9974a1088`.
- Main observed generation: 101. Source and library remained untouched; no compile or physical-host test ran.

## Degeneration

Main `Execution/Session/LoopDetector.fs` and fixed upstream use the same update order and formula: increment step; use 1 for a new token or `1 - lambda^(step - previous)` for a repeated token; update the token's last-seen step; update count with `lambda * count + replacement`. Thresholds are both strict, so boundaries are Normal. Both retain one dictionary entry per seen vocabulary token.

The current production input is repository-owned `o200k_base.encode`; envelope and lineage remain owned by the checked-in repository artifact. LoopSensor stores the current detector per session and replaces it under its existing gate. Exact run filtering, atomic reset while arming, anomaly identity, interrupt ownership, abort-cause reconciliation and continuation drain remain entirely in LoopSensor. The library implements none of those lifecycle responsibilities.

Receiving the library would remove the local token recurrence but add envelope/plain-object construction, an opaque detector message interface, token-array passage, state-label mapping and evaluation DTO decoding. It also validates/copies the whole encoded token array before folding and freezes the evaluation. That validation is valuable at a general JS library boundary, but no invalid production token/envelope trigger was found in this consumer. The upstream mutable per-token accumulator may allocate less than main's record fold; no measurement here establishes a useful total latency/CPU improvement once tokenization and the new validation/copy pass are counted. Do not claim a performance gain or regression without measurement.

Remaining P3 proof-input edge: actual main Surface `classify(NaN)` answers Normal, whereas upstream explicitly rejects nonfinite input. No production path to a NaN count was found with the tracked finite envelope and valid tokenizer ids. No proposal to widen this release for that proof-only input.

## Ablation

Fixed upstream's `validateNodes`, `findCycle`, `validateModes` match main Manifest's slice/main-node checks, DFS over declared nodes and the two edge-mode laws. Error text and first-violation ordering match by source inspection. Library input snapshots/accessor rejection are additional general JS boundary validation; main receives a Thoth-decoded typed manifest and typed mode DU.

Real consumers must retain the main registry and IO contracts: profiles/env overrides, manifest decode (including station and borrowed_surface), fingerprints, tool/fact maps, default modes, installed registry and borrowed capability projection. `ToolRegistry`, `StaticTools`, `AgentJournal`, `ManagedAgentConfig`, and `FissionHostSurface` all consume those gates. Receiving only the three pure predicates requires typed Node/Edge/Map conversions and violation-to-AblationLoadError mapping without improving the currently observed gate behavior.

### P2: invalid configuration fallback opens mapped gates

Actual public Surface reproduction:

1. Start without an installed registry; set `WANXIANGSHU_ABLATION_PROFILE` to `audit-nonexistent-profile` in the isolated probe process.
2. `load()` returns `{ok:false,kind:'UnknownProfile'}`; `installed()` remains null.
3. `registry()` returns the `denied()` empty registry with nodeCount 0.
4. `AblationRegistry.modeFor` defaults an absent node to Active. Consequently `allowsTool('deliver')`, `allowsToolSchema('deliver')`, `allowsFact('AgentFact.Context')`, `allowsPrimaryAgent('manager')`, and `fissionVisible()` all return true.

This is a genuine composition defect in the existing fallback/default policy, not a new integration regression. Production gates call the same registry fallback, so it is more than an arbitrary malformed proof DTO. It contradicts `Resolution.denied`'s stated fail-closed intent. The fixed upstream pure module does not own registry resolution or default modes, so receiving it cannot fix this issue. A future atomic fix should first specify rejected-load behavior for every real gate, preserve normal unconfigured production defaults and unmapped tags, and add invalid-profile/invalid-mode/DAG-rejection tests against those gates. Do not blindly change every missing-mode lookup to Ablated: valid production defaults and unowned-tag semantics need separate consideration.

### P3: malformed manifests accepted by both rule implementations

- Duplicate slice IDs are accepted; the real main Surface reports nodeCount 3 while the modes object has 2 keys.
- A cycle consisting solely of undeclared edge endpoints is ignored. Main accepts a one-node manifest plus ghost1 -> ghost2 -> ghost1 borrow edges.

These were executed against actual main Surface. Fixed upstream shares the same `Map.ofList` and declared-node-only DFS logic by direct source inspection; it was not compiled or executed for this receipt. Actual repository resources have 52 unique node IDs and 991 edges with every endpoint declared, so these are configuration-validation debts and not current graph failures. Integration does not resolve them.

## Probe evidence and limits

`probe.mjs`: 7 finite actual-main-Surface observation groups. Node v22.13.0 and Bun 1.3.14 each exit 0, with the problematic acceptance/fail-open behavior asserted as observations rather than misreported as repaired behavior. See `receipt-node.json`, `receipt-bun.json`, `node.log`, `bun.log`. No requirement runner, Host interrupt/continuation lifecycle or full release result is claimed.
