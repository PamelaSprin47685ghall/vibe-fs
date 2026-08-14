> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Cursor Pair Programming Hint Projection Experiment

> Proposed Change. This Change owns provider-specific encoding of the canonical Pair Programming Hint.  
> It does not own the Hint's product semantics.

## 1. Summary

The canonical Pair Programming Hint must remain one semantic payload.

Ordinary providers continue using the existing synthetic fake-tool pair.

Cursor must not receive that fake-tool wire when its strict validator rejects it. Instead, implement all three legal text-role projections:

```text
CursorAssistantOutput
CursorUserOutput
CursorSystemOutput
```

Run controlled real-Cursor experiments for all three and choose one production default from evidence.

Do not keep three semantic prompt copies.

## 2. Current problem

Current Pair Hint transport is a durable synthetic pair:

```text
assistant fake tool-call auto-injected
assistant fake tool-result auto-injected
    output = canonical Pair Hint text
```

The occurrence carries stable identity and transcript-gap anchors.

Cursor currently avoids adding new fake-tool pairs because strict provider validation rejects the wire.

That leaves two defects:

1. fresh Cursor work does not receive newly anchored Pair Hint semantics;
2. historical durable Pair occurrences may still replay as fake tools if a transcript moves from another provider projection to Cursor.

Therefore the fix is not “remove the Cursor skip”. The fix is:

```text
durable semantic occurrence
+
provider-specific renderer
```

The skip is currently frozen in tests as `skipAutoInjectedRequested("cursor") = true`, and existing tests also pin that historical pairs still replay for Cursor. The naive change `Some "cursor" -> false` would therefore immediately start emitting the fake-tool wire to Cursor. The dispatcher must instead keep creating/replaying the same anchored occurrences for Cursor while routing them to a text-role renderer.

## 3. Core architecture

Treat the durable Pair occurrence as:

```text
“the canonical Pair Programming Hint occurs at this semantic transcript location”
```

not as:

```text
“a fake tool call/result must exist forever”
```

Conceptually:

```fsharp
type PairProgrammingWireStrategy =
    | FakeToolPair
    | CursorAssistantOutput
    | CursorUserOutput
    | CursorSystemOutput
```

The selected strategy is transport/config state.

The Hint text remains one source.

## 4. Single semantic owner

The final canonical text continues to come from the existing Pair Programming Hint composition.

This Change MUST automatically project whatever the canonical Hint contains at the time, including separately owned fragments such as:

- simplified-Chinese reasoning guidance;
- `[NEEDHELP]` collaboration guidance;
- parallel tool-wave guidance;
- existing role-specific additive fragments such as Manager Magic Todo where already legal.

Do not add:

```text
CursorAssistantHintText
CursorUserHintText
CursorSystemHintText
```

Do not tune wording per mode.

The experiment variable is message role/envelope, not prompt quality.

## 5. Ordinary provider behavior

Non-Cursor provider behavior remains:

```text
Pair occurrence
→ FakeToolPair
```

No regression in:

- stable call ID;
- call/result anchors;
- idempotent replay;
- append-only same-provider prefix behavior;
- source identity;
- fake-tool result containing the marker text.

This Change is not a rewrite of the working non-Cursor path.

## 6. Cursor transport model

For Cursor:

```text
Pair occurrence
→ one synthetic text message
→ anchored at the semantic result location
```

Use the existing occurrence's `ResultGap` for the single Cursor text message because the actual Hint text in the old fake pair lives in the completed result half.

Keep `CallGap` in the durable occurrence because an ordinary provider still needs both anchors if the same occurrence is projected there later.

## 7. Three required encoders

### 7.1 Assistant Output

Provider-visible shape:

```text
role = assistant
text = markerText
```

Hypothesis only:

- likely lower authority distortion;
- may look like the model's own prior output;
- may weaken instruction salience or create self-continuation artifacts.

### 7.2 User Output

Provider-visible shape:

```text
role = user
text = markerText
```

Hypothesis only:

- Cursor may follow it strongly;
- highest risk of masquerading as a real HumanRoot/latest user turn.

### 7.3 System Output

Provider-visible shape:

```text
role = system
text = markerText
```

Hypothesis only:

- may produce strongest adherence;
- may violate message-layout rules;
- may disturb system-prefix/cache semantics;
- may be interpreted as Persona/system replacement.

All are hypotheses. None is the winner until measured.

### 7.4 Priors registered before the experiment

The original discussion recorded these priors explicitly so the experiment can confirm or refute them:

- **Assistant Output is the first candidate**: it does not tamper with “who is issuing instructions” semantics.
- **System Output may produce the strongest instruction adherence**, but a system message injected mid-transcript may collide with Cursor's message-layout or prefix-cache rules.
- **User Output is the riskiest**: much of the control flow treats user as the authority boundary, so even with late placement the synthetic message must be provably recognized, stripped, and replayed as synthetic on every subsequent transform — it must never become a fake HumanRoot.

Priors are not evidence. The winner is decided by the registered gates in §23.

## 8. Authority firewall

The provider-visible role is transport only.

### UserOutput MUST NOT become HumanRoot

A synthetic Cursor user message may never:

- become AuthorityRoot;
- become a real HumanRoot;
- change last-real-user semantics;
- enter root requirement capture;
- replace current physical user message;
- affect review scope;
- become external ingress;
- mutate interaction state.

Provider role `user` is not Domain user authority.

### SystemOutput MUST NOT replace system identity

A synthetic Cursor system message may never:

- rewrite canonical role system prompt;
- mutate Session Persona;
- become AuthorityRoot;
- change Role Law ownership;
- be persisted as a new canonical root system instruction.

It is a provider projection only.

### AssistantOutput MUST NOT become semantic work

A synthetic Cursor assistant message may never be interpreted as:

- a real model completion;
- provider evidence;
- terminal result;
- Chronicle work;
- XTrace semantic work;
- a real Assistant Output for completion/recovery.

All three are transport-only Pair Hint projection.

## 9. Stable identity

Cursor projection needs deterministic synthetic identity derived from the same durable Pair occurrence.

Do not use:

```text
random UUID
clock
current process order
marker text digest as sole identity
```

Recommended conceptual identity:

```text
PairOccurrenceId + CursorRole
```

Repeated transformation with the same strategy must produce byte-identical output.

## 10. Synthetic detection remains identity-based

Never detect Pair Hint messages by comparing text.

The user may quote the exact Hint.

Synthetic identity must remain based on internal source/known durable occurrence metadata.

Do not add:

```text
if message.text == PairProgrammingGuidelineText then synthetic
```

## 11. Historical replay/provider transition

This is a hard requirement.

Scenario:

```text
ordinary provider
→ durable Pair occurrence O1
→ old view renders FakeToolPair

later same canonical transcript is sent through Cursor
→ O1 remains the same occurrence
→ Cursor renders O1 as selected Cursor text projection
→ no historical fake tool reaches Cursor
```

Do not “migrate” by creating a second Cursor occurrence.

Reverse transition must also work:

```text
Cursor view
→ later ordinary provider view
→ same O1 renders FakeToolPair from CallGap/ResultGap
```

The semantic occurrence is provider-independent.

## 12. Prefix/idempotence scope

Within a fixed provider projection family and fixed Cursor role strategy:

```text
same occurrence history
same semantic transcript
same strategy
→ repeated transform byte-identical
```

A deliberate provider-family transition may change physical bytes:

```text
FakeToolPair bytes ≠ CursorText bytes
```

That is not prefix corruption; it is a different provider projection.

The durable semantic occurrence identity must remain stable.

## 13. Experiment mode lifetime

Do not change Cursor role strategy mid-session during comparison.

Use:

```text
fresh session + fixed strategy
```

for each arm.

Do not create:

```text
turn 1 Assistant
turn 2 User
turn 3 System
```

inside one session.

## 14. Sanitization and ReviewSeal

Preferred internal shape may carry synthetic metadata, but Cursor strict validation is authoritative.

The existing pipeline boundary should remain:

```text
PairProgrammingThoughtTransform
→ HostMessageProjection.sanitizeMessages
→ ReviewSeal
```

If Cursor rejects internal metadata fields, the sanitizer must remove/adapt them before provider wire generation.

ReviewSeal must seal the exact bytes Cursor will receive after sanitization.

## 15. Transform ordering

Do not move the Pair transform relative to existing Strength/XTrace/Enforcer ordering.

The canonical semantic projection must remain frozen before Pair marker injection as it is today.

This Change changes only the Pair occurrence renderer.

## 16. Eligibility

Existing Pair Programming Hint eligibility remains unchanged.

Do not use Cursor work as a reason to expand the Pair Hint to roles/sessions currently excluded.

Magic Todo remains a Manager-only additive semantic fragment and is not copied into a Cursor-specific string.

## 17. Emergency fuse

Keep the existing emergency capability to suppress auto-injected Pair Hint work.

The existing fuse is the environment variable `WANXIANGSHU_SKIP_AUTO_INJECTED=1` — an independent emergency switch that suppresses new Pair Hint occurrences for the turn.

Do not conflate:

```text
emergency disable
```

with:

```text
Cursor safe projection
```

Production Cursor should normally receive the canonical Hint through the selected safe strategy.

## 18. Real-Cursor experiment — Gate 1: validator acceptance

Run all three implementations against the real Cursor/OpenCode provider path.

A strategy is eliminated immediately if strict validation fails for representative shapes.

Cover at least:

```text
empty/fresh transcript
trailing real user
assistant-completion tail
one real tool call/result
parallel tool batch
tool result then continuation
multi-turn session
historical anchored Pair occurrence
ordinary-provider → Cursor transition
repeated transform
```

`ValidatorPass = 100%` is mandatory for promotion.

## 19. Gate 2: protocol/authority integrity

For every surviving strategy prove:

```text
tool calls still execute
tool results continue correctly
no last-user confusion
no duplicate Pair message
no synthetic role becomes Domain authority
continuations/fallback remain valid
provider transition remains valid
```

Protocol integrity and AuthorityRoot integrity are mandatory 100% gates.

## 20. Gate 3: Hint adherence

Only structurally safe survivors compete on semantic effectiveness.

Measure whatever is in the canonical Hint at experiment time.

At minimum, current Chinese-reasoning guidance is a stable observable.

If Proposal 1 is active, also observe:

- `[NEEDHELP]` exact-token behavior;
- help is used without “only when desperate” behavior.

If Proposal 3 is active, also observe:

- independent tool calls are coalesced into same-turn parallel waves.

Do not create Cursor-only behavioral wording.

## 21. Experiment matrix

Run matched tasks across all surviving strategies:

1. plain reasoning/no tool;
2. one read/search;
3. multiple independent parallel reads/searches;
4. mixed independent tool types;
5. tool result followed by reasoning;
6. multi-turn session;
7. continuation/fallback;
8. historical provider transition;
9. `[NEEDHELP]` fast→deep path if active;
10. `[NEEDHELP]` deep→Meditator path if active;
11. representative attached work sessions where Pair Hint is already eligible.

Use the same:

```text
Cursor model
repository state
task
system/role prompt
tool surface
canonical Pair Hint
```

Only role projection changes.

Use fresh sessions and repeated runs, not a single anecdotal transcript.

## 22. Metrics

Hard gates:

```text
ValidatorPass = 100%
ProtocolIntegrity = 100%
AuthorityIntegrity = 100%
IdempotentReplay = 100%
```

Quality metrics:

```text
Pair Hint adherence
Chinese-reasoning retention
Independent Tool Coalescing Rate
NEEDHELP collaboration behavior (if active)
tool-turn success
conversation distortion
```

Secondary metrics:

```text
prefix/cache behavior
token cost
RTT
```

Do not choose a winner solely from an LLM judge score.

Prefer mechanically observable metrics where possible.

## 23. Winner rule

Use lexicographic selection:

```text
1 transport validity
2 protocol safety
3 authority safety
4 Hint adherence
5 replay/prefix stability
6 cost/cache/RTT
```

A lower layer cannot compensate for failure above it.

Among safe survivors, choose the highest instruction adherence + tool-turn stability.

Tie-break with lower authority distortion, simpler wire, cache/prefix behavior, and RTT.

Do not encode the prior hypothesis as the outcome.

## 24. Production after selection

After evidence selects the winner:

```text
provider = cursor
→ fixed production strategy = WINNER
```

The two losing pure encoders may remain narrowly available for tests/diagnostics/re-canary, but they should not become a normal end-user product setting.

Do not expose a permanent everyday three-mode knob unless separately approved.

Rollback should be able to change the Cursor projection strategy without changing canonical Hint semantics.

## 25. If a mode is rejected

A strict validator rejection is a valid experiment result.

Do not weaken Cursor validation to force all three modes through.

Do not disguise one role as another and call it “SystemOutput”.

If all three legal text-role projections fail, the Change remains blocked and requires a separately approved transport design.

Do not silently fall back to fake tool.

## 26. Suggested implementation shape

Possible ownership:

```text
Domain/
    PairProgrammingGuidelineProjection.fs
        CursorHintRole
        provider projection strategy

Infrastructure/OpenCode/Host/
    PairProgrammingThoughtTransform.fs
        occurrence/anchor coordination

    PairProgrammingGuidelineRenderer.fs
        renderFakeToolPair
        renderCursorAssistant
        renderCursorUser
        renderCursorSystem

    HostMessageProjection.fs
        provider strict sanitization
```

Keep renderers pure where possible.

Do not create three durable ledgers.

## 27. Tests

### Pure rendering

```text
all three modes receive identical markerText bytes
roles differ as intended
stable synthetic ID
single Cursor message anchored to ResultGap
repeated render deterministic
```

### Authority

```text
UserOutput not HumanRoot
SystemOutput not Persona/Role Prompt
AssistantOutput not real model completion/evidence
```

### Replay

```text
ordinary occurrence → Cursor safe replay
Cursor occurrence view → ordinary FakeToolPair view
no duplicate occurrence on same placement
```

### Tool turns

```text
one tool
parallel real tool batch
tool result continuation
multi-turn
fallback/continuation
```

### Strict validator

Fixture-level schema checks plus real Cursor canaries.

## 28. Long Stroke

This Change does not create a Cursor-specific Long Stroke.

Three-mode comparison is a targeted real-Cursor canary/proof experiment.

After the winner is selected, integrate one representative winning Cursor projection phase into the repository's existing unique Long Stroke.

The unique Long Stroke must then continue through its existing later lifecycle.

Do not add:

```text
cursor-pair-hint-long-stroke
second-long-stroke
parallel real-Host lifecycle duplicate
```

## 29. Static no-go gates

Reject:

```text
three copies of Pair Hint text
Cursor still skips semantic Pair occurrence
Cursor receives auto-injected fake tool
UserOutput becomes HumanRoot
SystemOutput mutates canonical system prompt
AssistantOutput becomes real completion/evidence
history fake tool replays to Cursor
provider transition creates duplicate occurrence
text-equality synthetic detection
unanchored append every transform
random synthetic IDs
winner picked only by intuition
permanent normal-user three-mode product setting
second Long Stroke
```

## 30. Non-goals

This Change does not:

- define `[NEEDHELP]` semantics;
- define parallel tool-wave semantics;
- implement Repository Warm Start;
- change ordinary non-Cursor FakeToolPair behavior;
- restructure AuthorityRoot/XTrace;
- broaden Pair Hint eligibility.

## 31. Implementation order

```text
Phase 0  activate proposal
Phase 1  separate durable occurrence semantics from fake-tool renderer
Phase 2  keep ordinary FakeToolPair behavior green
Phase 3  implement pure Assistant encoder
Phase 4  implement pure User encoder + authority tests
Phase 5  implement pure System encoder + Persona tests
Phase 6  provider-specific dispatcher
Phase 7  historical ordinary→Cursor and Cursor→ordinary replay
Phase 8  strict sanitizer/ReviewSeal proof
Phase 9  real Cursor matched three-mode canaries
Phase 10 select winner by registered gates
Phase 11 fix production Cursor default
Phase 12 integrate winner into existing unique Long Stroke
Phase 13 full repository gates
```

## 32. Completion criteria

Complete only when:

1. one canonical Hint semantic owner remains;
2. non-Cursor still uses FakeToolPair;
3. Cursor receives Pair Hint without fake tool;
4. Assistant encoder exists;
5. User encoder exists;
6. System encoder exists;
7. all three use byte-identical content;
8. all three project the same durable occurrence;
9. Cursor single message uses canonical result placement;
10. historical ordinary occurrences are safe on Cursor;
11. reverse Cursor→ordinary replay works;
12. same strategy replay is byte-stable;
13. UserOutput never becomes HumanRoot;
14. SystemOutput never replaces Persona/system authority;
15. AssistantOutput never becomes semantic completion/evidence;
16. every mode has real strict-validator evidence;
17. all safe modes have matched repeated effectiveness evidence;
18. winner follows the documented gate order;
19. production Cursor fixes one winner;
20. losing modes are not normal product settings;
21. sanitizer precedes ReviewSeal;
22. existing transform order remains valid;
23. no second durable Pair ledger exists;
24. no second Long Stroke exists;
25. winner regression is integrated into the single existing Long Stroke;
26. later Long Stroke phases remain green;
27. full repository gates are green.

## 33. Final architecture statement

```text
Pair Programming Hint is semantic.
Fake tool is one transport.

Ordinary providers
→ one anchored occurrence
→ fake tool pair.

Cursor
→ the same anchored occurrence
→ one validated text-role projection.

Assistant, User, System are all implemented.
Evidence chooses one production default.

Provider-visible user is not HumanRoot.
Provider-visible system is not Persona.
Provider-visible assistant is not semantic work.

Historical Pair occurrences stay semantic across provider transitions.

One Hint.
One durable occurrence.
Provider-specific projection.
One Long Stroke.
```

---

## Appendix A. Source requirement (verbatim)

Provenance: 2026-08-12 requirements discussion, exported from ChatGPT (<https://chatgpt.com/c/6a7c3e7d-9da4-83ed-a1df-786f947db940>) and formerly kept at `changes/proposed/AGENTS.md` before archival.

> 1. 目前 Cursor 模型没法伪造工具（严格校验），但是指令遵循挺好，因此对 Cursor 模型可以把 Pair Programming Hint 伪造成 Assistant Output / User Output / System Output （三个选项都实现，然后实测哪个方式最好）

## Appendix B. Discussion record — occurrence ≠ wire

Key analysis facts behind this Change:

- HOST-013's durable value is the gap anchor, stable call id, replay, and same-placement idempotence — the fake-tool shape is incidental.
- The durable occurrence should be read as a general anchored guideline fact (discussion name: `ProviderGuidelineAnchored` / single-gap fact), not as `CallGap + ResultGap` pretending to remain a tool pair under Cursor.
- Historical replay trap: the current code skips *new* pairs for Cursor yet still replays historical durable pairs, and existing tests pin that replay behavior. A fix that only covers fresh Cursor sessions leaves provider transition broken.
- One experiment variable: all three arms consume byte-identical canonical `markerText`; only the role/envelope differs. Otherwise the experiment measures “which prompt is stronger” instead of “which role projects better”.
- Sanitizer precedes ReviewSeal; the sealed bytes are the exact bytes Cursor receives.
- The winner is decided by evidence, then formal specs keep only the winner; the three-mode comparison itself lives in Change history/proof, not in permanent product semantics.

## Appendix C. Failure classification taxonomy

Every real-Cursor experiment run classifies failure as one of:

```text
ValidatorReject
RoleUnsupported
MessageShapeReject
ToolProtocolCorruption
InstructionIgnored
AuthorityDistortion
ConversationDistortion
PrefixInstability
ProviderFailure
UnrelatedTaskFailure
```

Two scoring rules:

- provider 500 / network timeout MUST NOT be counted against an encoder's instruction-following;
- a strict-validator reject is not ordinary LLM quality noise — it is a Gate 1 elimination.

## Appendix D. Detailed test IDs (C-PH series)

### Canonical content

```text
C-PH-001 three encoders receive identical MarkerText
C-PH-002 no encoder owns a second literal Pair Hint
C-PH-003 modifying canonical markerText changes all three outputs
C-PH-004 role is the only semantic experiment variable
```

### AssistantOutput

```text
C-PH-010 role = assistant
C-PH-011 part = text
C-PH-012 text byte-exact MarkerText
C-PH-013 stable id
C-PH-014 placed at ResultGap
C-PH-015 repeated render byte-identical
```

### UserOutput

```text
C-PH-020 role = user
C-PH-021 same text bytes
C-PH-022 stable placement
C-PH-023 does not create Domain HumanRoot
C-PH-024 does not change AuthorityRoot
C-PH-025 does not become OpeningMaterial
```

### SystemOutput

```text
C-PH-030 role = system
C-PH-031 same text bytes
C-PH-032 does not modify SessionPersona
C-PH-033 does not modify Role Prompt
C-PH-034 does not rebuild Prompt Composition
C-PH-035 stable placement
```

### Historical replay

```text
ordinary provider
→ occurrence O1 created
→ FakeToolPair rendered

same transcript later projected for Cursor
→ O1 remains the same durable occurrence
→ no fake tool sent to Cursor
→ selected Cursor text output rendered at O1.ResultGap
```

and:

```text
Cursor repeated transform
→ no second occurrence created for the same placement
```

### Reverse transition

```text
Cursor → ordinary provider
→ canonical occurrence unchanged
→ ordinary provider re-projects a legal FakeToolPair from CallGap/ResultGap
```

Cursor having once used a single-message projection must never permanently corrupt ordinary-provider replay.

## Appendix E. Coordinator commit sequence

The existing transform commit sequence stays the single durable writer and remains fail-closed:

```text
read durable
strip known synthetic
decide placement
construct candidate
render
validate
append durable
return
```

Only `render` becomes provider-specific. Do not create a separate `CursorCoordinator`.

## Appendix F. Specification impact

Expected formal-layer touch points after activation:

```text
docs/what/host.md
    Pair Hint semantic occurrence vs provider projection separation

docs/shape/host.md
    anchored occurrence SSOT
    provider-specific renderer ownership

docs/how/host.md
    ordinary FakeToolPair
    Cursor winning projection
    provider transition replay
    sanitize → ReviewSeal

docs/proof/host.md
    Assistant/User/System controlled comparison
    Cursor validator evidence
    winner rationale
    historical replay canaries

docs/what/prompt.md
docs/shape/prompt.md
    provider-visible role ≠ Prompt authority

docs/proof/prompt.md
    UserOutput cannot become HumanRoot
    SystemOutput cannot replace Persona
```

This Change file records the three-mode experiment and the winner decision; the formal specification ultimately keeps only the winning behavior.

## Appendix G. Cross-proposal sequencing

The discussion's recommended construction order across the four sibling proposals:

```text
1. Pair Hint semantic + Cursor three encoders + strict-validator canary   (this Change)
2. reasoning-delta NEEDHELP sensor + fast→deep continuation
3. deep→Meditator→deep consultation
4. Repository Warm Start                                                  (independent, parallelizable)
5. one combined real-Cursor pass of the unique Long Stroke:
   tool-heavy work, NEEDHELP fast→deep, deep→Meditator,
   warm-start — then decide the default Cursor encoder (this Change)
```

---

# Final outcome

## Outcome

Canonical Pair Programming Hint 保持单一语义源；Cursor 不再接收 auto-injected fake tool wire。三种 text-role 投影（Assistant / User / System）均已实现并 byte-identical 正文；controlled canary 与 strict-validator 证据后，生产固定 **Assistant Output**（`tryInject` → `assistant` role）。历史 durable occurrence 在 ordinary→Cursor→ordinary 间可逆 replay。

## Final specification

正式语义：`docs/proof/host.md` HOST-013、PROMPT-018 — Cursor 新 placement 仍 append 同一 durable occurrence；provider wire 为 `ResultGap` 处一条 stable-id assistant text；UserOutput 不得成为 HumanRoot；SystemOutput 不得替换 Persona；AssistantOutput 不得成为 semantic work/completion evidence。

## Implementation result

- `PairProgrammingThoughtTransform.fs`：durable `PairProgrammingGuidelineAnchored` occurrence 与 provider-specific renderer 分离；`tryInjectCore cursorRole` 支持三 encoder 对比；生产 `tryInject` 固定 `assistant`。
- `GuidelineProjection.fs` / journal fold 保留 CallGap+ResultGap 双 anchor，供非 Cursor FakeToolPair replay。
- `WANXIANGSHU_SKIP_AUTO_INJECTED=1` 紧急 fuse 与 Cursor safe projection 正交保留。

## Verification

- C-PH 系列 pure/authority/replay 测试全绿。
- Real-Cursor strict-validator canary：三 mode 对照后 production 选定 Assistant。
- 唯一 Long Stroke 含 winning Cursor projection 阶段且后续 adversity spine 继续全绿。
- `npm run check` 全量门禁通过（unit 2385/2385；integration；harness 275/275）。

## References

- `docs/proof/host.md` Cursor Pair Hint row
- `src/Wanxiangshu/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.fs`
- `src/Wanxiangshu/Journal/GuidelineProjection.fs`
