# sleep-based-synchronization — Lesson

## Opinion

### Definition

Sleep-based synchronization confuses **time passing** with **a prerequisite becoming true**.

The code means "wait until X is ready," but it implements "wait 500 ms and hope X is ready by then." That substitution is the entire defect.

A fixed delay never proves readiness, completion, visibility, ownership transfer, lock release, process startup, replication, or event delivery. It only changes the probability that those things may have happened before the next line runs.

This is why sleep-based tests and production flows have the same two bad modes:

- on fast machines they waste time after the cause already happened;
- on slow or contended machines they advance before the cause happened.

The delay is simultaneously too long and too short.

### Governing Principle

The distinction is precise:

> **If the next step is allowed because the clock expired, ask whether what you really needed was a fact.**

If success still depends on an event/state and the clock only says "give up after N seconds," the clock is policy, not synchronization.

### Trigger When

Fire this rule when correctness depends on `sleep`, fixed delay, arbitrary timer, or "give it a moment" before observing another asynchronous effect. Common forms:

- `sleep(500); assert file exists` after starting a writer;
- "wait two seconds for the server to start" instead of observing readiness;
- delaying before reading eventual state because "replication usually settles by then";
- test teardown sleeping so child processes "have time to exit";
- retry loops whose only success condition is surviving a delay rather than observing a real state;
- UI or agent workflows that insert pauses to avoid races instead of waiting on ownership/completion signals.

### Do Not Trigger When

Do not fire for every use of sleep. Rate limiting, protocol backoff, jitter, scheduled cadence, animation/human pacing, deliberate fault injection, or time-domain product behavior may legitimately depend on elapsed time. A timeout can also bound a causal wait without becoming the success signal.

### Distinguish From

Nearby rules:

- `timeout-inflated-to-pass` — an existing uncertainty budget is enlarged to hide failure;
- `time-dependent-test` — a test depends on real wall-clock/calendar behavior more generally;
- `blocking-event-loop` — waiting blocks shared execution capacity;
- `repeat-until-pass` — inconsistent evidence is sampled until green.

### Decision Procedure

Use this rule specifically when elapsed duration is impersonating causality.

The decisive repair starts by naming the hoped-for fact. Not "wait 500 ms," but:

- process emitted ready;
- file appeared with expected generation;
- callback completed;
- session reached idle;
- lock/lease released;
- replication observed version V;
- child terminated;
- event with identity X committed.

Then wait on **that** fact. If no event exists, polling can be legitimate provided it polls an authoritative observable condition and has a timeout that fails rather than treating timeout expiry as success.

### Examples

`sleep(500); assert file exists` after starting a writer. "Wait two seconds for the server to start" instead of observing readiness. Test teardown sleeping so child processes "have time to exit."

## Advice

### What To Do Now

Replace the duration with the fact.

First write down what the sleep is pretending to establish. If the answer is readiness, completion, visibility, shutdown, propagation, lock release, or ownership transfer, expose an observation that can actually prove that condition.

Preferred mechanisms, in descending honesty:

1. await the operation's own completion/result;
2. subscribe to the event that establishes the condition;
3. await a readiness/termination primitive owned by the subsystem;
4. poll an authoritative state/version with a bounded timeout when no event can be exposed.

In every case, timeout has one job: **bound how long uncertainty may persist**. It does not turn uncertainty into success when the clock expires.

### Why This Matters

For process startup, wait for a real readiness signal, health endpoint with the right semantics, bound port + protocol handshake, or emitted ready event — not "the process has existed for two seconds."

For process shutdown, await exit/termination and resource release — not "we sent SIGTERM and slept a little."

For storage/replication, wait for a generation/version/commit identity that proves the required fact is visible — not "eventual consistency probably settled."

For tests, prefer deterministic fakes/controllable schedulers when the timing source itself is not what is under test. A test that needs 30 seconds of wall-clock patience to prove a local state transition is usually testing the scheduler as much as the product.

### Repair Strategy

For polling, verify two sides:

- the condition becoming true wakes progress promptly;
- timeout expiry produces an explicit failure/unknown outcome, never a counterfeit success.

For event-based waits, test missed-event races: establish whether subscription happens before the event can fire, or whether current state is checked in a way that closes the gap. Replacing sleep with a callback while introducing "subscribe-after-complete" is not an improvement.

### Decision Branches

Artificially delay the producer well beyond the old sleep: the consumer must not advance early. Then make the producer complete immediately: the consumer should proceed immediately rather than paying the old fixed latency.

### Common Wrong Fixes

Common fake repairs:

- increase 500 ms to 5 s;
- replace one long sleep with ten short sleeps but never inspect a causal state;
- busy-loop until the same wall-clock duration expires;
- keep the sleep "for stability" after adding a readiness signal;
- add retry after the sleep, so flakiness becomes less frequent but the missing synchronization remains;
- use a timeout callback that marks the operation successful because "nothing bad happened yet."

### Verification

Verification should attack scheduler assumptions. Artificially delay the producer well beyond the old sleep: the consumer must not advance early. Then make the producer complete immediately: the consumer should proceed immediately rather than paying the old fixed latency.

### Done When

You are done when the code can explain every wait as:

> I cannot proceed until **this observable fact** becomes true; the clock only limits how long I am willing to remain uncertain.

That sentence is synchronization. "We wait because this is usually enough time" is probability wearing a causal costume.

### Nudge

> Do not wait for time when you mean to wait for cause.
