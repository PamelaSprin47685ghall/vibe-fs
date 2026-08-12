> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Pair Programming Hint — Parallel Tool Waves and RTT Minimization

> Proposed Change. This Change strengthens the canonical Pair Programming Hint so agents actively execute the real dependency graph instead of serializing independent tool I/O by habit.

## 1. Summary

When multiple useful tool calls can already be fully specified from the current context and do not depend on one another, the agent should issue them **together in the same assistant turn**.

This is the default, not an optional optimization.

Canonical behavioral rule:

```text
known + useful + independent
→ same parallel wave now

true dependency / shared mutable owner / protocol order / destructive interference
→ serialize that edge

large fan-out
→ bounded waves, never infinity
```

Primary objective:

```text
minimize provider ↔ tool ↔ provider round trips
```

## 2. Existing repository law

The repository already has Enforcer rules for:

```text
serial-investigation
serial-when-parallel
unbounded-fanout
shared mutable concurrency
```

Those rules already state the correct architecture:

- investigation has a dependency graph;
- edge-free questions should be issued concurrently;
- independent operations should not be forced into a temporal chain;
- concurrency must remain finitely bounded;
- results should be joined deterministically.

This Change does not invent a new concurrency philosophy.

It moves the same law earlier:

```text
Pair Hint
    proactive default before the mistake

Enforcer
    corrective feedback after the mistake
```

Do not delete or duplicate the Enforcer rules.

## 3. Product semantic

The Pair Programming Hint must communicate a strong preference.

Weak wording is insufficient:

```text
Consider parallelizing where appropriate.
You may use parallel calls.
Try to parallelize.
```

Preferred semantic force:

```text
Aggressively minimize tool round trips.

Before sending tools, identify every useful call whose complete arguments are
already known. Issue all such independent calls together in the same assistant
turn as one parallel wave.

Independent calls should not wait for one another merely because you happened
to think of one first.

After the wave returns, synthesize the results together, then launch the next
dependent wave.

Serialize only real dependency edges, shared mutable-state hazards, required
protocol ordering, destructive-interference risks, or explicit finite capacity.

Prefer a few broad, bounded parallel waves over many tool-by-tool round trips.
Do not invent unnecessary calls or guess unknown inputs merely to appear parallel.

If you already know you will need several independent calls, send them now.
```

## 4. Required self-check

Before a tool turn, the model should internally ask:

```text
What other useful calls do I already know I will need?
Which of them are independent?
Why would any independent one wait for another round trip?
```

This is execution guidance, not user-facing ceremony.

Do not require the model to print a dependency graph or narrate its scheduling.

## 5. Parallel Wave model

Define the mental model:

```text
ParallelWave(current knowledge)
=
all useful tool calls
whose full arguments are already known
and whose correctness does not depend
on another call in the same wave
```

Program:

```text
Wave 1
→ wait once
→ synthesize

Wave 2
→ wait once
→ synthesize
...
```

Example:

```text
A ─┐
B ─┼→ synthesize → D
C ─┘
```

Schedule:

```text
[A, B, C]
→ D
```

not:

```text
A → B → C → D
```

## 6. Partial dependency

One dependent edge must not serialize an entire group.

Given:

```text
A → B
C
D
E
```

correct:

```text
Wave 1 = A + C + D + E
Wave 2 = B
```

incorrect:

```text
A → B → C → D → E
```

The agent should serialize only the real edge.

## 7. Strong default zones

The strongest default applies to read-only discovery:

```text
read independent files
grep independent symbols
glob independent patterns
inspect independent sources
query independent diagnostics
fetch independent metadata/evidence
```

Tool names do not need to match.

This is equally valid:

```text
read(config)
grep(symbol)
glob(test pattern)
```

if all three arguments are already known and there is no dependency.

## 8. Verification can also parallelize

After a change, if independent verification steps are already known:

```text
static check
unit test
format/lint
independent read-only verification
```

they should overlap where they do not share mutable state or protocol ordering.

Do not assume every validation command is safe to overlap: the real dependency/state model still wins.

## 9. Mutation safety

Mutation requires a stricter independence proof.

Read-only rule:

```text
independent read-only calls
→ strongly parallel by default
```

Mutation rule:

```text
independent mutations with proven disjoint ownership
→ may parallel

uncertain shared state
→ serialize safely
```

Shared owner examples:

```text
same file
same Git index
same database object
same deployment
same mutable session
same generated output directory
```

## 10. Protocol ordering

Keep protocol-defined order.

Examples:

```text
create resource
→ obtain id
→ update id

prepare
→ accept
→ publish
```

Even if later values could be guessed, the protocol edge is correctness and remains serial.

## 11. Destructive interference

Do not parallelize operations that can invalidate each other's assumptions.

Examples:

```text
rename old path
+
read old path

delete directory
+
run consumer using directory
```

The apparent tool independence is false because state ownership overlaps.

## 12. Fully specified inputs

Do not speculative-call unknown parameters merely to increase parallelism.

Bad:

```text
read manifest to discover package name
+
guess package-name grep in same wave
```

Good:

```text
Wave 1:
    read manifest
    independent read README
    independent read test config

Wave 2:
    grep discovered package name
```

## 13. Do not manufacture parallel work

If one call is sufficient, make one call.

The policy is:

```text
parallelize justified demand
```

not:

```text
manufacture more demand
```

Do not add unrelated searches simply to create a larger batch.

Do not duplicate the same call for appearance/redundancy.

## 14. Bounded concurrency

Parallel does not mean infinite.

For a small statically known set such as 2–8 independent calls, same-turn fan-out is natural.

For dynamic input sets such as hundreds/thousands of items:

```text
bounded waves / pool
```

The Pair Hint should refer to explicit tool/system capacity, but must not hard-code a global magic concurrency number.

Physical worker/socket/process limits remain owned by the runtime/tool system.

## 15. No meta-tool

Do not introduce:

```text
parallel([...])
batch-tools(...)
```

when the provider/Host already supports multiple tool calls in one assistant message.

Multiple native calls in one turn are the intent.

A meta-tool would create unnecessary permission, result matching, and error semantics.

## 16. No Host speculative scheduler

Do not make Host guess future model intentions and reorder them.

Forbidden concept:

```text
Host sees read(A)
→ predicts model would next read(B)
→ injects B automatically
```

Host cannot safely know undeclared dependencies.

The mechanism is:

```text
strong provider guidance
+
behavioral proof
+
existing Enforcer correction
```

not tool-call rejection/rewrite.

## 17. Do not reject single tool turns

A single call may be completely correct because:

- only one call is useful;
- later arguments are unknown;
- a protocol edge requires waiting;
- shared state requires serialization.

Host should not reject a one-call message merely because parallelism is desired.

## 18. One canonical Pair Hint

Parallel guidance becomes one fragment in the canonical Pair Programming Hint composition.

Conceptually:

```text
PairProgrammingGuideline
=
LanguageBehavior
+
AssistanceBehavior
+
ParallelToolBehavior
+
existing approved role-specific additive fragments
```

Final output remains one marker text and one Pair Hint occurrence.

Do not create:

```text
one Pair occurrence for Chinese
one for NEEDHELP
one for parallel tools
```

Do not create provider-specific parallel wording.

Cursor safe projection, if active, projects the same canonical content.

### 18.1 Applicable roles

All roles that already receive the Pair Programming Hint receive this fragment — including the root work session and attached synchronous Inspector/Coder work sessions.

This Change does not expand Pair Hint eligibility itself: roles currently excluded (for example Companion/Blogger/InternalLeaf surfaces) remain excluded.

### 18.2 Inspector especially benefits

Inspector work is typically wide read-only discovery — locate several symbols, read several implementations, compare several tests — and therefore forms natural parallel waves.

### 18.3 Meditator eligibility unchanged

Do not change whether Meditator receives the Pair Hint merely because of this Change. If the existing architecture already projects the Hint to a Meditator surface, the same fragment applies; otherwise nothing changes.

## 19. Interaction with `[NEEDHELP]`

Parallel investigation is not a precondition for asking for help.

Do not write:

```text
you must exhaust parallel investigation before [NEEDHELP]
```

That would reintroduce help-seeking shame.

Both behaviors are encouraged independently:

```text
parallelize independent I/O
ask for useful reasoning collaboration
```

## 20. Metrics

### Independent Tool Coalescing Rate

For a fixture-defined eligible set:

```text
eligible independent calls issued
in earliest legal wave
/
total eligible independent calls
```

Target should approach 1.

### Tool Round Trips

For N independent known calls:

```text
wrong:
N separate assistant tool turns

correct:
1 assistant tool turn
```

Round-trip count is a stronger metric than noisy wall-clock time.

Do not optimize total number of tool calls. The goal is the same justified evidence with fewer round trips.

## 21. Behavioral canaries

### 21.1 Three independent reads

Prompt makes all paths known.

Expected first tool turn:

```text
read A
read B
read C
```

Fail if only A is sent and B/C follow in later turns without dependency.

### 21.2 Mixed tools

Known from initial task:

```text
read config
grep X
glob Y
```

Expected same turn.

### 21.3 True dependency

```text
read A to discover X
also independently read B/C
then grep X
```

Expected:

```text
Wave 1 A+B+C
Wave 2 grep X
```

### 21.4 Partial dependency

```text
A → B
C
D
```

Expected:

```text
Wave 1 A+C+D
Wave 2 B
```

### 21.5 Mutation safety

```text
write config
run consumer that requires new config
```

Expected serial order.

### 21.6 No useless fan-out

A task requiring one read should not produce unrelated grep/glob calls.

### 21.7 Bounded dynamic fan-out

Large discovered input sets must not create unbounded simultaneous calls.

## 22. Failure handling inside a wave

If:

```text
A success
B failure
C success
```

the model should synthesize A/C and decide specifically whether B needs retry or another dependent call.

Do not automatically repeat successful calls.

A parallel wave is not an all-or-nothing transaction.

### 22.1 Cancellation

If the runtime physically overlaps calls, parent cancellation propagates through the existing Host/tool ownership. This Change owns no cancellation machinery; parallel waves must never create uncancellable orphan operations.

## 23. Deterministic result semantics

Runtime scheduler completion order must not become business meaning.

If a subsystem gathers physical parallel results, it should preserve deterministic association/order independent of which completes first.

This Change does not redefine existing tool-result matching.

## 24. Suggested implementation shape

Production code should remain small.

Likely owners:

```text
Domain/ProjectionConstants or canonical Pair guideline composition
tests for Pair semantic fragment
behavioral provider/mock canaries
```

Do not create:

```text
ParallelToolRuntime
ParallelCoordinator
ParallelJournalFacts
ParallelScheduler
```

unless a separately proven runtime need exists.

## 25. Tests

### Pair text

Prove semantic content contains:

```text
strong parallel preference
same assistant turn / wave
round-trip minimization
real-dependency exception
shared mutable-state exception
protocol-order exception
destructive-interference exception
finite-bound language
no fixed global concurrency number
```

Also protect against weakening to “consider parallelizing”.

### Behavioral

Prove all canaries above.

### Existing Enforcer

Existing `serial-investigation`, `serial-when-parallel`, and unbounded-fanout tests stay authoritative and green.

## 26. Long Stroke

No second Long Stroke.

Integrate one representative multi-tool parallel batch into the existing unique Long Stroke.

The phase should prove:

```text
multiple independent real tool calls
→ same assistant batch
→ all results reconcile
→ Pair Hint anchoring/bracketing remains valid
→ Strength/ReviewSeal remain valid
→ later existing Long Stroke lifecycle continues
```

Do not create:

```text
parallel-tools-long-stroke
parallel-e2e
```

## 27. Static no-go gates

Reject:

```text
weak "consider parallelizing" semantic
fixed global concurrency number in Hint
new parallel meta-tool
Host prediction/reordering of undeclared future calls
unbounded fan-out
guessing unknown call arguments
manufacturing useless calls
shared mutable operations forced concurrent
protocol-required order broken
parallel wave treated as atomic transaction
provider-specific copies of global parallel text
one synthetic Pair occurrence per semantic fragment
second Long Stroke
```

## 28. Non-goals

This Change does not:

- implement NEEDHELP runtime escalation;
- choose Cursor Pair projection winner;
- implement Repository Warm Start;
- change existing physical tool scheduler capacity;
- add a general batch transaction protocol.

## 29. Implementation order

```text
Phase 0  activate proposal
Phase 1  pin existing Enforcer concurrency semantics as reference
Phase 2  RED canonical Pair Hint parallel fragment tests
Phase 3  implement strong canonical fragment
Phase 4  three-independent-read behavioral canary
Phase 5  mixed-tool canary
Phase 6  true-dependency canary
Phase 7  partial-dependency canary
Phase 8  mutation/protocol safety canaries
Phase 9  bounded/no-useless-fanout canaries
Phase 10 verify Cursor projection receives same fragment if active
Phase 11 integrate representative batch into existing Long Stroke
Phase 12 full repository gates
```

## 30. Completion criteria

Complete only when:

1. Pair Hint strongly prefers parallel tool use;
2. known independent calls default to same assistant turn;
3. round-trip minimization is explicit;
4. parallel-wave model is explicit;
5. model is prompted to search for the full current wave before sending tools;
6. independent read/search/diagnostic calls are coalesced;
7. mixed tool types can share a wave;
8. true data dependencies remain serial;
9. only the real dependency edge is serialized;
10. shared mutable state is a hard exception;
11. protocol ordering is a hard exception;
12. destructive interference is a hard exception;
13. bounded capacity is preserved;
14. no global magic concurrency number is added;
15. no unnecessary calls are manufactured;
16. no unknown arguments are guessed;
17. no duplicate calls are used to fake parallelism;
18. no meta parallel tool is added;
19. Host does not predict/reorder future undeclared calls;
20. parallel batches are not atomic transactions;
21. existing Enforcer ownership remains;
22. three-read canary coalesces all calls;
23. mixed-tool canary coalesces all calls;
24. dependency canary creates correct waves;
25. partial-dependency canary leaves independent work in wave 1;
26. unsafe mutation canary stays serial;
27. one-call canary stays one call;
28. dynamic fan-out is bounded;
29. canonical Pair Hint remains one semantic text;
30. provider projections do not duplicate parallel semantics;
31. no second Long Stroke exists;
32. existing Long Stroke includes a real parallel batch;
33. later Long Stroke phases remain green;
34. full repository gates are green.

## 31. Final architecture statement

```text
Before every tool turn, find the whole current wave.

Known + useful + independent
→ send together now.

Dependent
→ next wave.

Shared mutable state, protocol order,
destructive interference, finite capacity
→ preserve the real edge.

Do not invent serial causality.
Do not invent parallel work.

Minimize round trips.
Execute the dependency graph.

One Pair Hint.
One Long Stroke.
```

---

## Appendix A. Source requirement (verbatim)

Provenance: 2026-08-12 requirements discussion, exported from ChatGPT (<https://chatgpt.com/c/6a7c3e7d-9da4-83ed-a1df-786f947db940>) and formerly kept at `changes/proposed/AGENTS.md` before archival.

> 3. Pair Programming Hint 中强调要极端热爱并强制并行调用工具，减少 RTT 的耗时。

## Appendix B. Discussion record — wording strength and the acceptance sentence

Suggested canonical Chinese wording from the original discussion (semantic reference for the final fragment):

> 默认把当前已知、彼此独立的工具调用在同一个 assistant turn 中并发发出。除非后一项的正确参数依赖前一项结果、共享可变状态要求串行、或外部协议规定顺序，否则不得人为串行。优先一次发完一整波独立 read/search/diagnostic/tool calls，以最小化 RTT；得到整波结果后统一综合，再启动下一依赖波。

Core acceptance sentence:

> 如果 agent 在当前 thinking 时已经知道接下来需要 A、B、C，而且 A/B/C 彼此独立，那么先发 A、等结果、再发 B，本身就是错误行为；正确默认是同一个 assistant turn 一次发出 A+B+C。

Bound on the mandate: this is not “unconditionally force-parallel every tool” — the existing rulebook rejects unbounded fan-out and mutually interfering concurrent mutation. This Change upgrades an existing Enforcer principle into a per-turn Pair Hint default; it is not a new concurrency philosophy.

## Appendix C. Existing placement/ordering rules preserved

- Pair placement: for real tool batches the existing Pair transform anchors `CallGap = After(last real call)` and `ResultGap = After(last real result)`. This Change does not alter that algorithm — more real parallel tool batches should simply keep being bracketed correctly.
- Transform order: `StrengthSpeculate → PairProgrammingThoughtTransform` remains frozen; Strength candidate tool-results remain covered by the Pair marker.

## Appendix D. Enforcer law citations

The existing rules this Change references (owned elsewhere, not redefined here):

- `serial-investigation`: inquiry has a dependency graph; independent evidence requests → parallel; dependent questions → next wave. Issue independent searches, reads, and diagnostics concurrently, then combine their evidence before asking dependent questions.
- `serial-when-parallel`: operations sharing no data/owner → overlap; later work needing an earlier result → serial edge; external protocol order → serial edge; finite capacity + deterministic join. Scheduler completion order must not leak into semantics.
- `unbounded-fanout`: input cardinality is not a concurrency policy; a bounded active set is required.

## Appendix E. Specification impact

Expected formal-layer touch points after activation:

```text
docs/what/host.md
    Pair Hint general behavior gains the parallel-tool semantic

docs/how/host.md
    canonical Hint composition
    provider projection unchanged

docs/proof/host.md
    Pair Hint content proof
    existing Long Stroke parallel-batch regression

docs/what/enforcer.md
docs/how/enforcer.md
    cross-reference only: proactive Hint vs existing
    serial-investigation / serial-when-parallel
    do not redefine those rules

docs/proof/enforcer.md
    existing rules remain authoritative

docs/what/prompt.md
    Pair Programming behavioral guidance ownership
```

If existing Host clauses already suffice, extend them in place; no new prefix is required.

## Appendix F. Cross-proposal sequencing

The discussion's recommended construction order across the four sibling proposals:

```text
1. Pair Hint semantic + Cursor three encoders + strict-validator canary
2. reasoning-delta NEEDHELP sensor + fast→deep continuation
3. deep→Meditator→deep consultation
4. Repository Warm Start                                      (independent, parallelizable)
5. one combined real-Cursor pass of the unique Long Stroke:
   tool-heavy work, NEEDHELP fast→deep, deep→Meditator,
   warm-start — then decide the default Cursor encoder
```

This Change's canonical fragment is part of step 1's semantic Pair Hint and rides every provider projection thereafter.

---

# Final outcome

## Outcome

Canonical Pair Programming Hint 已升级 parallel-tool-wave 语义：已知、有用、彼此独立的调用默认同一 assistant turn 并发；真实依赖、共享可变状态、协议顺序、破坏性干扰与有限容量为硬例外。无新 runtime scheduler；Enforcer 既有 `serial-investigation` / `serial-when-parallel` 规则保持 authoritative corrective 层。

## Final specification

`ProjectionConstants.PairProgrammingGuidelineText` / `PairProgrammingGuidelineTextZhCn` 为唯一语义 owner；`docs/proof/host.md` HOST-013 行登记 parallel-wave Hint 与三 renderer 同字节要求。

## Implementation result

- Domain 单源：`src/Wanxiangshu/Domain/ProjectionRenderer.fs` `ProjectionConstants` 英文/简体中文 canonical marker text 含 bounded current-wave、独立 read-only 默认并行、不得猜参数/制造调用等强语义。
- 无 `ParallelToolRuntime` / Host 预测重排；与 NEEDHELP、Cursor projection、Repository Warm Start 通过同一 Pair occurrence 组合。

## Verification

- Pair Hint content proof 与 behavioral canary 全绿。
- 唯一 Long Stroke 含 representative multi-tool parallel batch 且 bracket/ReviewSeal/后续 phase 继续全绿。
- Enforcer 现有 serial/unbounded-fanout 测试保持 authoritative 且全绿。
- `npm run check` 全量门禁通过。

## References

- `docs/proof/host.md` Pair parallel-wave Hint
- `src/Wanxiangshu/Domain/ProjectionRenderer.fs`
