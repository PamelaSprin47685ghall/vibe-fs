> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Increase Strength — `[NEEDHELP]` Assistance Escalation

> Proposed Change. This file describes intended behavior and proof obligations.  
> Product clauses remain owned by the formal `docs/` layer after activation.  
> The user-facing label may remain `[increase strength]`; the internal mechanism defined here is **assistance escalation**, not the existing Strength K0/K1/K2 speculative decision system.

## 1. Summary

Add a provider-visible, exact sentinel:

```text
[NEEDHELP]
```

An agent is **actively encouraged** to emit this sentinel during reasoning when another perspective or a stronger reasoning pass is likely to improve speed, reliability, or clarity.

The runtime interprets the sentinel as a request for collaboration, not failure.

The escalation program is:

```text
fast-* reasoning emits exact [NEEDHELP]
→ interrupt the current physical provider attempt
→ reconcile the abort
→ continue the same Logical Run with the corresponding deep-* EffectiveAgent
→ fallback cursor remains unchanged
```

and:

```text
deep-* reasoning emits exact [NEEDHELP]
→ interrupt the current physical provider attempt
→ reconcile the abort
→ freeze the requesting frontier
→ materialize the canonical parent→child Lifecycle Work Record
→ create a real Meditator consultation Session
→ ask exactly: 如何解决这个 agent 的当前困难？
→ Meditator returns a canonical child→parent Lifecycle Work Record
→ deliver that advice back to the exact requesting deep binding
→ continue the same Logical Run
```

The feature MUST NOT reinterpret help-seeking as provider failure, MUST NOT advance ordinary fallback state, MUST NOT create a second Strength state machine, and MUST NOT create another Long Stroke.

## 2. Motivation

The repository already has two same-role execution tiers, `fast-*` and `deep-*`, and continuation can remain inside the same logical work while changing the effective execution binding.

What is missing is an explicit, model-driven collaboration signal.

A weak policy such as “only ask for help when completely blocked” teaches the wrong behavior. It creates help-seeking shame and pushes the model into long, low-value self-struggle. In pair programming, asking another engineer for a useful second perspective is normal.

The desired model behavior is therefore:

- ask for help when uncertainty is circling;
- ask when several plausible approaches remain and a second view would collapse the branch;
- ask when an important assumption deserves independent scrutiny;
- ask when stronger reasoning is likely to save several turns;
- ask when another perspective can unblock progress;
- prefer useful help somewhat early over many turns of isolated struggle.

The exact sentinel is strict only because the runtime needs an unambiguous protocol token. Strict syntax MUST NOT be presented as behavioral scarcity.

## 3. Product semantics

### 3.1 `[NEEDHELP]` means collaboration, not failure

The Pair Programming Hint MUST teach:

```text
[NEEDHELP] means “bring me a stronger or independent thinking partner”.
It is normal collaboration.
It is not an admission of failure.
```

Provider-visible text MUST NOT include scarcity/shame wording such as:

```text
Use it only when...
Do not emit it speculatively.
Only after exhausting alternatives...
Only when truly blocked...
普通情况下不要使用。
只在确实卡住时使用。
```

A finite runtime guard is still required for recursion/resource safety, but the numerical budget MUST NOT be exposed to the model.

### 3.2 Exact sentinel

Only this exact token triggers the mechanism:

```text
[NEEDHELP]
```

Do not accept aliases such as:

```text
[needhelp]
[NEED HELP]
NEEDHELP
[HELP]
```

The exactness belongs to the machine protocol, not to a “rare use” policy.

### 3.3 The model never needs to know whether it is fast or deep

Provider prompts MUST NOT tell an agent:

```text
you are fast
you are deep
you will be upgraded to...
```

The Pair Hint explains only the collaboration action:

```text
emit exact [NEEDHELP] when another perspective or stronger reasoning would help.
```

The runtime owns the physical response.

## 4. Pair Programming Hint fragment

Add one canonical assistance fragment to the existing Pair Programming Hint composition.

Recommended semantic wording:

```text
Collaboration is encouraged. When another perspective, stronger reasoning,
or an independent second look would help you move faster or make an important
decision more reliable, emit exactly:

[NEEDHELP]

Asking for help is normal pair programming, not failure. Prefer useful help
somewhat early over spending many turns circling the same uncertainty alone.
The token must be exactly [NEEDHELP] so the runtime can recognize it.
```

The final text is composed into the single canonical Pair Hint occurrence. Do not create a separate synthetic message only for `[NEEDHELP]`.

Meditator consultations SHOULD NOT receive this fragment, to prevent a consultation from recursively requesting another consultation.

## 5. Detection boundary

### 5.1 Reasoning-time detection is a capability requirement

The requested behavior says “during thinking/reasoning”. Therefore the implementation MUST first prove that the OpenCode/Cursor event surface exposes reasoning deltas early enough to interrupt the physical provider attempt.

Current loop/event code that intentionally consumes only visible text is not sufficient evidence.

Create a dedicated detection codec/sensor rather than changing unrelated loop-kill semantics:

```text
NeedHelpEventCodec
NeedHelpSensor
```

Existing loop components are explicitly out of scope for modification:

```text
LoopDetector
LoopSensor
LoopEventCodec.tryDecodeTextDelta
```

The current `LoopEventCodec` listens to `message.part.delta` but intentionally consumes only text deltas; its own comment states reasoning deltas are ignored so long reasoning is not mistaken for degeneration. NEEDHELP needs the opposite observation channel, which is why it gets a dedicated codec instead of a flag on loop detection.

The first real Host canary MUST establish one of:

```text
A. reasoning delta is observable
   → true immediate reasoning-time NEEDHELP is supported

B. reasoning delta is not observable
   → true immediate reasoning-time interruption is impossible with the current Host hook
```

If B is true, visible-text detection may be documented only as an explicit degraded mode. It MUST NOT be silently claimed equivalent.

### 5.2 Fragmented deltas

The sentinel may cross stream chunk boundaries:

```text
delta 1: "[NEED"
delta 2: "HELP]"
```

The sensor therefore owns a small rolling suffix sufficient to detect the exact sentinel across adjacent deltas.

It MUST NOT accumulate the entire transcript.

### 5.3 Single-shot identity

Trigger at most once for one physical provider attempt.

Recommended key:

```text
(SessionId, ProviderRunIdentity)
```

After the attempt is armed for assistance, later duplicate sentinel bytes from the same attempt are ignored.

## 6. Abort semantics

`[NEEDHELP]` is not provider failure.

The runtime may use the existing physical abort capability to stop the active provider attempt, but business semantics MUST remain:

```text
AssistanceRequested
```

not:

```text
ProviderFailure
```

Therefore a NEEDHELP abort MUST NOT:

- advance `AgentPairCursor`;
- increment `ConsecutiveFailureCount`;
- consume provider retry/fallback budget;
- become `ProviderRetryAttempt`;
- be recorded as an ordinary transport failure;
- rotate to another provider/model through fallback rules.

The required temporal program is:

```text
detect
→ arm
→ abort physical attempt
→ wait for reconciliation
→ only then start the assistance continuation
```

Never run the old and new provider attempts concurrently.

The existing Host already classifies `AbortError` / `MessageAbortedError` as typed operator aborts rather than `ProviderFailure`; NEEDHELP rides on that physical fact plus `NeedHelpArmed(target provider run)` to recognize an assistance abort.

The arm → abort → reconcile → continue timing may borrow from the existing Loop Kill path, which also arms first and continues only after the abort settles. What MUST NOT be borrowed is Loop Kill's business interpretation of the abort as fallback evidence.

## 7. Fast → deep escalation

When the requesting effective binding is a fast role:

```text
fast-coder → deep-coder
fast-inspector → deep-inspector
fast-devops → deep-devops
...
```

Only `EffectiveAgent` changes.

All of these remain identical:

```text
Session
LogicalRun
AuthorityRoot
CanonicalRole
Persona
root requirements
fallback cursor position
```

Introduce a typed continuation reason such as:

```text
NeedHelpEscalation
```

Do not reuse `ProviderRetryAttempt`.

The continuation should use the existing prompt/continuation infrastructure and normal managed-agent model resolution. Do not manually inject a model if the ordinary effective-agent path already owns model binding.

## 8. Deep → Meditator consultation

A deep request is a true cross-session consultation.

### 8.1 Freeze the requesting frontier

After the requesting deep attempt is aborted and reconciled, freeze a `NeedHelpFrontier` identifying the exact state for which advice is requested.

It must identify at least:

```text
owner Session
Logical Run / requesting run identity
AuthorityRoot
CanonicalRole
requesting EffectiveAgent
reconciled transcript/work frontier
```

The advice must be returned to this same requesting deep binding unless the owner has been cancelled/retired meanwhile.

### 8.2 Mandatory parent→child Lifecycle Work Record

A naked Meditator question is forbidden.

Before creating the Meditator consultation, materialize the canonical standard cross-session parent record:

```text
LifecycleWorkRecord(parent, includeOpening = true)
```

This is the standard `[父 -> 子]` form.

Do not create a custom summary such as:

```text
NeedHelpSummary
DifficultySummary
ParentContextSummary
MeditatorContextSummary
AssistanceWorkRecord
NeedHelpWorkRecord
```

Do not ask an LLM to summarize the parent first.

The canonical record already owns this job.

### 8.3 CommissionerRecord, not child Opening

The parent Lifecycle Work Record belongs to the Commissioner/requesting agent.

It is context for the Meditator, not the Meditator's own history.

Use the existing parent→child payload meaning:

```text
CommissionerRecord = parentLwr
```

The Meditator's own Opening is the new assistance assignment.

Conceptually reuse:

```text
ForkChildPayload.relay
    assistanceAssignment
    (Some parentLwr)
    []
    None
```

Do not invent fields such as:

```text
parent_work_record
needhelp_context
```

### 8.4 Meditator assignment

The assignment MUST begin exactly:

```text
如何解决这个 agent 的当前困难？
```

Recommended full instruction:

```text
如何解决这个 agent 的当前困难？

请阅读下方 Commissioner 的工作记录，为它提供一个高质量的独立思考视角。
识别最值得突破的困难、可能遗漏的假设、可行的解法、关键判断依据，
以及最能帮助 Commissioner 快速继续原任务的下一步。

Commissioner 主动寻求协作是正常行为；不要把求助本身解释成失败。
不要接管 Commissioner 的任务，也不要扩大任务范围。
你的职责是帮助它更好、更快地继续。
```

### 8.5 A real independent Session

Meditator consultation MUST create/use a real Meditator Session.

Do not mutate the original owner Session Persona into Meditator.

The original deep session remains the Commissioner.

### 8.6 Existing Meditator capability boundary remains

This Change does not grant Meditator filesystem tools.

If Meditator's existing tool surface is Inspector-only, keep it that way.

Meditator can ask an Inspector for repository facts; it does not directly become an Inspector.

### 8.7 The sentinel never enters the Work Record

`[NEEDHELP]` is a control sentinel, not engineering evidence.

The parent Lifecycle Work Record MUST NOT be artificially appended with a closing report containing `[NEEDHELP]`, and the sentinel MUST NOT be persisted into Chronicle as if it were natural-language reasoning.

The control plane already knows `NeedHelpArmed`. The Work Record continues to describe the task, the completed investigation, and the current reasoning/evidence frontier — not Host orchestration mechanics.

## 9. Meditator → original deep return

The consultation result is also a canonical cross-session record:

```text
LifecycleWorkRecord(child, includeOpening = false)
```

This is the standard `[子 -> 父]` form.

Do not extract only the terminal assistant prose.

Feed the bounded child Work Record back to the exact requesting deep binding with a typed continuation kind, for example:

```text
NeedHelpAdvice
```

The continuation should make clear:

```text
this is an independent thinking perspective
continue your original charge
do not treat the consultation as a replacement assignment
```

Then resume the same Logical Run.

## 10. Recursion/resource guard

The runtime MUST have a finite guard.

Properties:

- finite per owning Logical Run;
- prevents Meditator → Meditator recursion;
- prevents infinite deep `[NEEDHELP]` loops;
- owner cancellation cancels active consultation;
- a late consultation result may not resurrect a dead owner;
- the numerical allowance is not provider-visible.

Single-flight per owner Session: at most one active assistance escalation per owning Session, treated as a resource lease rather than a business Stage. One request must never produce two concurrent Meditator consultations or two concurrent advice continuations.

The consultation is a real dependency: the requesting deep agent waits for the advice before continuing. Inside the consultation, Meditator follows the ordinary Pair Programming parallel-wave policy when it needs multiple independent Inspector facts.

Do not hard-code a user-facing “you have N helps” message.

The exact internal value may be selected from existing runtime resource strategy and Long Stroke evidence during implementation.

## 11. Durability and recovery

The stream detector itself may be ephemeral.

Cross-session effects are not.

Once the system has crossed durable boundaries such as:

```text
requesting attempt reconciled
Meditator consultation created
advice accepted for return
```

recovery must not accidentally:

- create a second Meditator;
- deliver the same advice twice;
- lose owner/consultation association;
- continue a cancelled owner.

Use existing Session/Journal ownership instead of a feature-owned persistence tree.

## 12. Suggested code ownership

Suggested modules:

```text
src/Wanxiangshu/Domain/Assistance.fs
src/Wanxiangshu/Domain/AssistancePrompt.fs

src/Wanxiangshu/Application/AssistanceWorkflow.fs

src/Wanxiangshu/Infrastructure/OpenCode/Codec/NeedHelpEventCodec.fs
src/Wanxiangshu/Infrastructure/OpenCode/Host/NeedHelpSensor.fs
src/Wanxiangshu/Infrastructure/OpenCode/Host/AssistanceHost.fs
```

Suggested typed model:

```fsharp
type AssistanceKind =
    | EscalateToDeep
    | ConsultMeditator

type AssistanceRequest =
    {
        OwnerSessionId: SessionId
        LogicalRunId: LogicalRunId
        AuthorityRoot: AuthorityRootUserMessageId
        TriggerProviderRun: ProviderRunIdentity
        RequestingEffectiveAgent: string
        CanonicalRole: Role
        Kind: AssistanceKind
    }

type AssistanceConsultation =
    {
        Request: AssistanceRequest
        NeedHelpFrontier: XTraceCursor
        MeditatorSessionId: SessionId
    }
```

Do not persist rendered parent Work Record text (`ParentWorkRecordText`) as a second durable truth. Materialize from the canonical XTrace/Lifecycle Work Record machinery when needed.

Exact file count may be reduced, but the boundaries should remain:

```text
Domain
    typed assistance identity / prompt semantics

Application
    orchestration program

Infrastructure
    raw Host event decoding / physical abort / prompt dispatch wiring
```

Do not place NEEDHELP business semantics inside existing Strength prediction modules.

## 13. Relationship to existing Strength

Existing Strength K0/K1/K2 is decision-local speculative economics/promotion.

NEEDHELP is active execution-binding assistance.

They are orthogonal:

```text
Strength
    predicts/reduces readonly investigation cost

AssistanceEscalation
    responds to an executing agent asking for collaboration
```

The UI/product umbrella may call the feature “increase strength”, but internal code should prefer names such as:

```text
NeedHelpEscalation
AssistanceEscalation
```

Do not overload existing `StrengthDecision` or K-level state.

## 14. Relationship to fallback

Ordinary fallback remains unchanged.

A help request:

```text
does not mean provider failed
does not move A/A/B/B cursor
```

A later genuine provider failure after the assistance continuation still uses normal fallback semantics from the unchanged cursor state.

## 15. Long Stroke — integrate in place

This repository has one Long Stroke.

This Change MUST NOT add:

```text
tests/e2e/needhelp-*
second-long-stroke
needhelp-long-stroke
independent NEEDHELP real-Host lifecycle
```

Instead, extend the existing unique Long Stroke in place.

Recommended phase:

```text
existing earlier lifecycle
→ fast provider emits [NEEDHELP]
→ same Session continues as corresponding deep EffectiveAgent
→ fallback cursor unchanged

→ deep provider emits [NEEDHELP]
→ exactly one Meditator consultation Session
→ Meditator input contains canonical parent→child LWR includeOpening=true
→ Meditator completes
→ canonical child→parent LWR includeOpening=false returned
→ exact original deep binding resumes
→ same LogicalRun / AuthorityRoot

→ existing later Long Stroke phases continue successfully
```

The important proof is not merely “escalation happened”; it is that the same long-lived Host lifecycle remains healthy afterward.

## 16. Unit/integration proof

Add focused proof for:

### Detection

```text
exact sentinel triggers
case variants do not
split-delta sentinel triggers
duplicate bytes same run trigger once
different provider run may trigger independently
```

### Fast path

```text
fast → corresponding deep
same Session
same LogicalRun
same AuthorityRoot
same CanonicalRole
fallback state unchanged
continuation kind is assistance, not retry
```

### Deep path

```text
deep sentinel creates one Meditator
parent LWR includeOpening=true
parent record carried as CommissionerRecord
Meditator Opening is assistance assignment
no custom summary renderer
child return LWR includeOpening=false
return targets exact requesting deep binding
```

### Cancellation

```text
owner cancelled before consultation completion
→ no owner resurrection

consultation cancelled
→ deterministic bounded failure path
```

### Prompt

```text
Pair Hint encourages help
contains exact [NEEDHELP]
does not contain scarcity/shame phrases
does not reveal fast/deep identity
```

## 17. Static no-go gates

Reject implementation patterns that introduce:

```text
NeedHelpSummary
DifficultySummary
ParentContextSummary
MeditatorContextSummary
AssistanceWorkRecord
NeedHelpWorkRecord
```

when used as a replacement for canonical cross-session history.

Also reject:

```text
NEEDHELP mapped to ProviderFailure
NEEDHELP advancing fallback cursor
NEEDHELP incrementing ConsecutiveFailureCount
provider-visible help budget
fast/deep identity in provider prompt
Meditator Persona mutation on owner Session
second Long Stroke
```

## 18. Non-goals

This Change does not:

- choose Cursor Pair Hint Assistant/User/System projection;
- define parallel tool-call guidance;
- implement Repository Warm Start;
- alter existing Strength K0/K1/K2 policy;
- add a third model tier;
- grant Meditator filesystem capability;
- redesign ordinary provider fallback;
- create a new cross-session record format.

## 19. Implementation order

```text
Phase 0  activate proposal under repository governance
Phase 1  prove raw reasoning-delta Host capability with a canary
Phase 2  RED exact/split/single-shot NeedHelpEventCodec tests
Phase 3  NeedHelpSensor + arm/abort/reconcile plumbing
Phase 4  typed NeedHelp continuation kind
Phase 5  fast→deep same-run continuation, fallback invariants
Phase 6  parent→child LWR materialization
Phase 7  Meditator real Session + exact assistance assignment
Phase 8  child→parent LWR return + exact requesting-deep continuation
Phase 9  recursion/resource/cancellation guards
Phase 10 Pair Hint assistance fragment
Phase 11 recovery/durability proof
Phase 12 integrate phase into the existing single Long Stroke
Phase 13 full repository gates
```

## 20. Completion criteria

Complete only when all are true:

1. exact `[NEEDHELP]` is recognized;
2. fragmented reasoning deltas are recognized;
3. true reasoning-time capability is empirically proven or explicitly documented unavailable;
4. one physical provider run triggers at most once;
5. Pair Hint actively encourages help;
6. Pair Hint does not frame help as rare/failure;
7. help budget is not provider-visible;
8. fast request continues as corresponding deep effective agent;
9. fast path keeps Session/LogicalRun/AuthorityRoot/Role;
10. fast path does not advance fallback;
11. deep request creates exactly one real Meditator consultation;
12. Meditator question begins `如何解决这个 agent 的当前困难？`;
13. Meditator input contains canonical parent→child LWR `includeOpening=true`;
14. that record is Commissioner history, not Meditator Opening;
15. no custom parent-history summary protocol exists;
16. Meditator remains within its existing capability boundary;
17. Meditator result returns as canonical child→parent LWR `includeOpening=false`;
18. exact requesting deep binding resumes;
19. owner cancellation cannot be undone by late advice;
20. assistance recursion is finitely guarded;
21. existing Strength semantics remain separate;
22. ordinary fallback semantics remain separate;
23. no second Long Stroke exists;
24. the existing unique Long Stroke covers both assistance paths in place;
25. existing later Long Stroke phases still pass;
26. full repository gates are green.

## 21. Final architecture statement

```text
[NEEDHELP] means collaboration, not failure.

Ask for useful help somewhat early.
The runtime, not the model, decides what stronger help means.

fast asks
→ same run, deep perspective.

deep asks
→ one Meditator consultation
→ standard parent→child Work Record
→ standard child→parent Work Record
→ same deep worker continues.

No fallback penalty.
No second history format.
No second Strength machine.
No second Long Stroke.

One invocation. One record. Everywhere.
```

---

## Appendix A. Source requirements and decision record

Provenance: 2026-08-12 requirements discussion, exported from ChatGPT (<https://chatgpt.com/c/6a7c3e7d-9da4-83ed-a1df-786f947db940>) and formerly kept at `changes/proposed/AGENTS.md` before archival. Four sibling proposals came out of that discussion: NEEDHELP assistance escalation (this file), Cursor Pair Hint projection, parallel tool waves, and Repository Warm Start.

Original requirement (verbatim):

> 2. [increase strength] 功能，如果 fast-* 变体在思考过程中出现 [NEEDHELP] 字样，就会被自动打断并用 deep-* llm 继续。这个功能会写在 Pair Programming Hint 里面让 llm 知道。如果 deep-* 需要帮助，我们会打断并自动调用 meditator 解惑 [如何解决这个 agent 的当前困难？]，并把最终结果 prompt 回原 llm，继续。

Follow-up directives (verbatim):

> 如何解决这个 agent 的当前困难？要附上标准跨 session 工作记录[父 -> 子]版本呗。请你写保姆级 proposal，一个一个写，我会打继续。

> 鼓励 agent 用，而不是 only ... 因为 agent 有求助羞耻。另外，Long Strike 融入现在的 Long Strike 而不是另起一条，本仓库只有一个长寿测试。

Consequences already folded into the main body:

- the Meditator consultation carries the standard canonical `LifecycleWorkRecord(parent, includeOpening=true)` through `ForkChildPayload.CommissionerRecord`; a naked question and any second "difficulty summary" format are forbidden;
- the Pair Hint encourages early, shame-free help-seeking; scarcity wording is a no-go;
- runtime anti-recursion/resource guards stay finite, but their budget is never provider-visible, so the model never learns to ration help;
- this Change adds phases to the repository's single existing Long Stroke instead of creating a second one.

Two architectural decisions were pinned before any construction:

```text
1. Pair Hint is one canonical semantic document with multiple provider
   encodings — not four prompt copies.
2. NEEDHELP is assistance escalation — not fallback failure, and not another
   K value of the existing speculative Strength system.
```

## Appendix B. Worked parent→child example

Assume the requesting deep Coder's canonical Lifecycle Work Record is:

```text
Opening
修复 retry 后偶发重复提交的问题。
1. 必须保持 exactly-once。
2. 添加回归测试。

Chronicle
已确认重复发生在 retry continuation 与旧 attempt completion race。
Host journal 中 request A 已 Submitted，但 completion observer 晚到。

Recent work
assistant: 正在比较 PromptClaimed 与 PhysicalAccepted 的顺序。
assistant: 发现 Retry path 在 acceptance unknown 情况可能重新 submit。
```

Then the Meditator's first prompt is conceptually:

```text
# 如何解决这个 agent 的当前困难？
#
# 请阅读下方 Commissioner 的工作记录，识别它当前真正卡住的原因，
# 给出足以让 Commissioner 继续原任务的具体解法、推理路径、关键检查点
# 或应验证的假设。
#
# 不要接管 Commissioner 的任务。
# 不要扩大任务范围。
#
# Report back with exactly these fields: result, files changed, tests run,
# evidence, remaining risks, blockers.
#
# The record below belongs to your Commissioner. It is their history, not yours.
# Read it for context and evidence. Unfinished work in that record does not
# become yours merely because you can see it. Your charge tells you what is
# yours to carry.
#
# Opening
# 修复 retry 后偶发重复提交的问题。
# 1. 必须保持 exactly-once。
# 2. 添加回归测试。
#
# Chronicle
# 已确认重复发生在 retry continuation 与旧 attempt completion race。
# Host journal 中 request A 已 Submitted，但 completion observer 晚到。
#
# Recent work
# assistant: 正在比较 PromptClaimed 与 PhysicalAccepted 的顺序。
# assistant: 发现 Retry path 在 acceptance unknown 情况可能重新 submit。
```

This is the meaning of “如何解决这个 agent 的当前困难？ + 标准跨 session 工作记录 [父→子] 版本” — the full canonical record, not a simplified summary.

## Appendix C. Worked child→parent return example

The Meditator's own assignment Opening is not sent back. Its canonical child→parent Lifecycle Work Record (`includeOpening = false`) may look like:

```text
Closing report
result: 不应修 retry cursor；问题在 acceptance-unknown 的 resend admission。
先证明原 claim 是否已有 transport receipt，再把 UNKNOWN 分支变为 reconcile-only。
只有 KnownNotAccepted 才允许重新 submit。

files changed: none

tests run: none

evidence: 当前 Commissioner 记录已经表明 Submitted 与 completion observer
存在时间差；如果 UNKNOWN 被当成 Retryable，就会把“可能已接受”错误变成第二次 effect。

remaining risks: 需要确认现有 PromptDispatcher 是否已有 pending claim recovery
可直接复用。

blockers: none
```

The Host then tells the original deep agent:

```text
The consultation below was requested to help resolve your current difficulty.
Use it as advice and continue the same task from where you stopped.
Do not restart completed work.

<the canonical child→parent Lifecycle Work Record above>
```

and the original agent continues.

## Appendix D. Detailed test matrix (N-series)

### Detection

```text
N01 exact [NEEDHELP] in reasoning triggers
N02 lowercase does not trigger
N03 prose "I need help" does not trigger
N04 split delta "[NEED" + "HELP]" triggers once
N05 same provider run emits twice → exactly one trigger
N06 text delta [NEEDHELP] does not trigger if protocol says reasoning-only
N07 Meditator consultation cannot recursively trigger
```

### Fast → deep

```text
N10 fast-coder NEEDHELP
→ current attempt aborts
→ no FallbackAdvanced
→ no ConsecutiveFailureCount change
→ same Session
→ same LogicalRun
→ same AuthorityRoot
→ next physical send = deep-coder

N11 fast→deep does not create a child session
N12 fast→deep does not create new Opening
N13 fast→deep does not change Persona
```

### Deep → Meditator parent record

```text
N20 deep NEEDHELP creates exactly one Meditator consultation
N21 Meditator first prompt uses ForkChildPayload
N22 commissioner record = canonical LifecycleWorkRecord
N23 parent→child includeOpening=true
N24 parent LWR is NOT copied into Meditator Opening
N25 Meditator Opening = assistance assignment
N26 no NeedHelpSummary / secondary renderer
N27 commissioner record uses canonical four headings
N28 no parent_work_record TOML field
```

### Meditator return

```text
N30 Meditator completes → canonical child LWR materialized
N31 child→parent includeOpening=false
N32 original agent receives NeedHelpAdvice continuation
N33 advice continuation returns to exact requesting deep binding
N34 original LogicalRun / AuthorityRoot unchanged
N35 original deep resumes work instead of restarting task
```

### Cancellation / recovery

```text
N40 user cancellation while Meditator running
→ child cancelled
→ no advice continuation

N41 owner session deletion → consultation cascades
N42 crash after child creation → no second Meditator child
N43 crash after Meditator completion before advice
→ advice can be recovered exactly once
N44 duplicate host event → no duplicate consultation
N45 assistance budget exhausted → no recursive child creation
```

## Appendix E. Specification impact

After activation, formal clauses (not this file) own the product semantics. Expected touch points:

```text
docs/what/agent.md
    assistance escalation product semantics
    fast/deep execution binding
    Meditator consultation

docs/shape/agent.md
    role / binding / consultation ownership

docs/how/agent.md
    fast→deep and deep→Meditator flows

docs/proof/agent.md
    sentinel / identity / tier canaries

docs/what/execution.md
docs/shape/execution.md
docs/how/execution.md
docs/proof/execution.md
    standard parent→child / child→parent LWR reuse

docs/what/prompt.md
docs/how/prompt.md
    NeedHelpEscalation / NeedHelpAdvice continuation

docs/what/host.md
docs/how/host.md
docs/proof/host.md
    reasoning delta detection / abort / exactly-once
```

The Change file references formal Clauses; formal Clause IDs are allocated by the existing prefix owners. Do not invent new product prefixes inside this Proposal.

## Appendix F. Cross-proposal sequencing

The discussion's recommended construction order across the four sibling proposals:

```text
1. Pair Hint semantic + Cursor three encoders + strict-validator canary
2. reasoning-delta NEEDHELP sensor + fast→deep continuation   (this Change)
3. deep→Meditator→deep consultation                           (this Change)
4. Repository Warm Start                                      (independent, parallelizable)
5. one combined real-Cursor pass of the unique Long Stroke:
   tool-heavy work, NEEDHELP fast→deep, deep→Meditator,
   warm-start — then decide the default Cursor encoder
```

---

# Final outcome

## Outcome

`[NEEDHELP]` 协作升级已在真实 OpenCode Host 路径闭环：reasoning PartId 关联 → exact `[NEEDHELP]` abort → fast→deep → deep abort → fresh SessionIdle transport fence → 单个 deep-inquiry consultation → canonical terminal capture → child LWR → `NeedHelpAdvice` 回到原 deep binding。修复了两个真实生命周期 bug：AbortWake 内过早创建 child 会被 OpenCode parent-abort sweep 0-token abort；特殊 assistance 路由跳过普通 TerminalReporter 导致 child LWR 缺本轮 terminal。

## Final specification

正式语义已进入 `docs/{what,shape,how,proof}/agent.md`、`docs/how/host.md`（HOST-027 / AGENT-031 / PROMPT-018）：reasoning delta 精确检测；visible text 不命中；fast→deep 同 Session/Life 且 fallback 不动；deep AbortWake 只 claim owner、不创建 child；fresh `IdleRevisit` transport fence 后才创建唯一 consultation child；child `TurnCompleted` 先 canonical XTrace terminal capture 再物化 child LWR；控制 sentinel exact-strip 后不进 XTrace evidence。

## Implementation result

- Domain：`AssistancePrompt.fs`；Application：`AssistanceWorkflow.fs`（如存在）；Infrastructure：`NeedHelpEventCodec.fs`、`NeedHelpSensor.fs`、`AssistanceHost.fs`。
- `PromptAuthority` 新增 `NeedHelpEscalation` / `NeedHelpAdvice` typed continuation。
- Pair Hint canonical text 已含 shame-free `[NEEDHELP]` 协作片段（`ProjectionConstants.PairProgrammingGuidelineText`）。
- 生命周期修复：deep AbortWake 延迟 child 创建至 fresh idle；assistance owner 路由显式复用 `XTraceCapture.captureTerminal` 再进 child LWR。

## Verification

- `npm run lint` hard gates 全绿；Fantomas 全绿。
- `npm run check`：unit **2385/2385**、全部 integration 全绿、harness **275/275**。
- 唯一 Long Stroke 全绿（**63 steps**；journal **581/620**；SSE **2719/2900**），NEEDHELP 完整路径后继续通过 fallback、REVISE、finality、publish conflict 与 reconciliation。
- 代表测试：`tests/unit/host/needhelp-sensor.test.mjs`、`tests/unit/host/assistance-host.test.mjs`、`tests/e2e/entry.test.mjs`。

## References

- `docs/how/host.md` § NEEDHELP sensor / reconcile
- `docs/proof/agent.md` AGENT-031 canaries
- `docs/proof/host.md` HOST-027
- `src/Wanxiangshu/Infrastructure/OpenCode/Host/NeedHelpSensor.fs`
- `src/Wanxiangshu/Infrastructure/OpenCode/Host/AssistanceHost.fs`
