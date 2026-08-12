# race-first-wins-semantics — Enforcer

Race-first-wins semantics appear when the scheduler, network, or runtime accidentally gets to answer a business question.

The signature defect is this:

> Keep the logical inputs the same. Change only which concurrent result arrives first. The business answer changes.

If no domain rule says arrival order is meaningful, the system has outsourced semantics to latency.

This often hides behind performance code that “takes the first successful result,” `Promise.race`, `Task.WhenAny`, competing callbacks, redundant requests, speculative execution, or parallel workers. Those mechanisms are not inherently wrong. They become wrong when **physical completion order substitutes for a declared selection rule**.

Examples of accidental semantics:

- two replicas return different versions and the first response becomes truth;
- two candidate fixes race and the first completed patch is published without comparing correctness;
- concurrent discoveries compete to initialize shared state, and the first caller silently defines the canonical value;
- a fallback path and primary path race; whichever finishes first wins even though one result is semantically preferred;
- an asynchronous cache fill races a fresher source, and “first visible” becomes “authoritative.”

Do not fire when timing is genuinely part of the protocol. Leader election, lease acquisition, auction close, explicit first-writer-wins registers, lowest-latency replica selection, or hedged reads can all use time/order legitimately **if the protocol defines identity, freshness, quorum, cancellation, and tie behavior**. In that case timing is not an accident; it is one of the inputs.

Also do not confuse this with “concurrency exists.” Parallel work can finish in any order while a deterministic join later applies the real rule. Concurrency is fine. Undeclared scheduler sovereignty is not.

Nearby rules:

- `lost-update` — stale write erases an accepted update;
- `shared-mutable-concurrency` — several actors share mutation authority;
- `serial-when-parallel` — independent work is needlessly serialized;
- `flaky-test-tolerated` — unstable verdicts have been normalized.

Use this rule when the sharpest statement is: **arrival order is choosing an outcome that should be chosen by domain facts.**

A decisive test is to force permutations. Hold result A, let B arrive first, record the outcome. Then reverse the schedule with identical logical inputs. If the business result changes, ask whether the specification can defend that change without referring to runtime timing. If it cannot, the race has become policy by accident.

The repair is one of two things:

1. remove arrival order from the decision and apply a deterministic merge/selection law over the required information; or
2. admit that first-wins is truly the protocol and make that law explicit, including stable identity, freshness, cancellation of losers, and conflict/tie behavior.

Do not paper over the race with tiny sleeps, task priorities, “primary usually finishes first,” or retries. Those merely tune the probability distribution of an undeclared policy.

> The scheduler may decide **when** facts arrive. It should not decide **what those facts mean** unless the domain explicitly gave it that authority.
