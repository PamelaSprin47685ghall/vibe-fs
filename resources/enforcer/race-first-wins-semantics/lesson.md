# race-first-wins-semantics — Lesson

## Opinion

### Definition

Race-first-wins semantics appear when the scheduler, network, or runtime accidentally gets to answer a business question.

The signature defect is this:

> Keep the logical inputs the same. Change only which concurrent result arrives first. The business answer changes.

If no domain rule says arrival order is meaningful, the system has outsourced semantics to latency.

### Governing Principle

This often hides behind performance code that "takes the first successful result," `Promise.race`, `Task.WhenAny`, competing callbacks, redundant requests, speculative execution, or parallel workers. Those mechanisms are not inherently wrong. They become wrong when **physical completion order substitutes for a declared selection rule**.

### Trigger When

Examples of accidental semantics:

- two replicas return different versions and the first response becomes truth;
- two candidate fixes race and the first completed patch is published without comparing correctness;
- concurrent discoveries compete to initialize shared state, and the first caller silently defines the canonical value;
- a fallback path and primary path race; whichever finishes first wins even though one result is semantically preferred;
- an asynchronous cache fill races a fresher source, and "first visible" becomes "authoritative."

### Do Not Trigger When

Do not fire when timing is genuinely part of the protocol. Leader election, lease acquisition, auction close, explicit first-writer-wins registers, lowest-latency replica selection, or hedged reads can all use time/order legitimately **if the protocol defines identity, freshness, quorum, cancellation, and tie behavior**. In that case timing is not an accident; it is one of the inputs.

Also do not confuse this with "concurrency exists." Parallel work can finish in any order while a deterministic join later applies the real rule. Concurrency is fine. Undeclared scheduler sovereignty is not.

### Distinguish From

Nearby rules:

- `lost-update` — stale write erases an accepted update;
- `shared-mutable-concurrency` — several actors share mutation authority;
- `serial-when-parallel` — independent work is needlessly serialized;
- `flaky-test-tolerated` — unstable verdicts have been normalized.

### Decision Procedure

Use this rule when the sharpest statement is: **arrival order is choosing an outcome that should be chosen by domain facts.**

A decisive test is to force permutations. Hold result A, let B arrive first, record the outcome. Then reverse the schedule with identical logical inputs. If the business result changes, ask whether the specification can defend that change without referring to runtime timing. If it cannot, the race has become policy by accident.

The repair is one of two things:

1. remove arrival order from the decision and apply a deterministic merge/selection law over the required information; or
2. admit that first-wins is truly the protocol and make that law explicit, including stable identity, freshness, cancellation of losers, and conflict/tie behavior.

Do not paper over the race with tiny sleeps, task priorities, "primary usually finishes first," or retries. Those merely tune the probability distribution of an undeclared policy.

### Examples

Two replicas return different versions and the first response becomes truth. Two candidate fixes race and the first completed patch is published without comparing correctness. A fallback path and primary path race; whichever finishes first wins even though one result is semantically preferred.

## Advice

### What To Do Now

First decide whether timing belongs to the product rule at all.

If it does not, remove scheduler order from the outcome. Let concurrency gather information, then decide using stable domain facts: version, priority, quorum, freshness, score, explicit precedence, or a merge law that gives the same result under every permitted arrival permutation.

### Why This Matters

If first-wins really *is* the protocol, stop treating it as an implementation shortcut. Specify it like a protocol:

- what stable identity is competing;
- what makes a candidate eligible;
- whether stale candidates may win;
- whether success/failure races are equivalent;
- how losers are cancelled or ignored;
- what happens on ties / simultaneous observation;
- whether replay must reproduce the original winner;
- which durable fact records the winner so restart does not rerun the election by accident.

### Repair Strategy

The repair owner is the layer that owns the business choice. A scheduler API such as `race`, `WhenAny`, callback order, or completion queue should never become the hidden owner merely because it is convenient to call.

Prefer deterministic join when all relevant information is required. Parallelism can still buy latency: fetch in parallel, compute in parallel, inspect in parallel. The important point is that **completion order only affects when the decision can be made, not what decision is correct**.

Where a subset is sufficient, make sufficiency explicit. "First two of three matching quorum" is a rule. "Whichever two happen to return first" is only safe if quorum semantics prove all eligible subsets equivalent for the decision being made.

### Decision Branches

If the decision persists, test restart/replay. Once a winner becomes a durable fact, recovery must restore that fact rather than re-running a race whose timing cannot be reproduced.

### Common Wrong Fixes

Common fake repairs:

- add `sleep(10)` so the preferred branch tends to finish first;
- raise thread/task priority for the intended winner;
- launch the primary a few milliseconds earlier and rely on head start;
- retry when the "wrong" branch wins, effectively sampling schedules until a preferred answer appears;
- cancel losers before confirming the first result is semantically admissible;
- sort by completion timestamp after the fact and call that deterministic — the timestamp is still scheduler output unless the domain owns it;
- hide the race behind a facade whose result type no longer reveals how the winner was chosen.

### Verification

Verification should control schedule deliberately. For each set of logical inputs, enumerate meaningful arrival orders and assert either:

- every permutation yields the same outcome; or
- the documented first-wins law predicts the different outcomes exactly.

### Done When

A useful completion criterion is that a reviewer can answer "why did X win?" without saying "because its future resolved first" unless **that sentence itself is the documented domain law**.

> Performance may choose the fastest route to the facts. It must not smuggle latency in as a substitute for judgment.

### Nudge

> The scheduler may decide **when** facts arrive. It should not decide **what those facts mean** unless the domain explicitly gave it that authority.
