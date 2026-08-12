# sleep-based-synchronization — Enforcer

Sleep-based synchronization confuses **time passing** with **a prerequisite becoming true**.

The code means “wait until X is ready,” but it implements “wait 500 ms and hope X is ready by then.” That substitution is the entire defect.

A fixed delay never proves readiness, completion, visibility, ownership transfer, lock release, process startup, replication, or event delivery. It only changes the probability that those things may have happened before the next line runs.

This is why sleep-based tests and production flows have the same two bad modes:

- on fast machines they waste time after the cause already happened;
- on slow or contended machines they advance before the cause happened.

The delay is simultaneously too long and too short.

Fire this rule when correctness depends on `sleep`, fixed delay, arbitrary timer, or “give it a moment” before observing another asynchronous effect. Common forms:

- `sleep(500); assert file exists` after starting a writer;
- “wait two seconds for the server to start” instead of observing readiness;
- delaying before reading eventual state because “replication usually settles by then”;
- test teardown sleeping so child processes “have time to exit”;
- retry loops whose only success condition is surviving a delay rather than observing a real state;
- UI or agent workflows that insert pauses to avoid races instead of waiting on ownership/completion signals.

Do not fire for every use of sleep. Rate limiting, protocol backoff, jitter, scheduled cadence, animation/human pacing, deliberate fault injection, or time-domain product behavior may legitimately depend on elapsed time. A timeout can also bound a causal wait without becoming the success signal.

The distinction is precise:

> **If the next step is allowed because the clock expired, ask whether what you really needed was a fact.**

If success still depends on an event/state and the clock only says “give up after N seconds,” the clock is policy, not synchronization.

Nearby rules:

- `timeout-inflated-to-pass` — an existing uncertainty budget is enlarged to hide failure;
- `time-dependent-test` — a test depends on real wall-clock/calendar behavior more generally;
- `blocking-event-loop` — waiting blocks shared execution capacity;
- `repeat-until-pass` — inconsistent evidence is sampled until green.

Use this rule specifically when elapsed duration is impersonating causality.

The decisive repair starts by naming the hoped-for fact. Not “wait 500 ms,” but:

- process emitted ready;
- file appeared with expected generation;
- callback completed;
- session reached idle;
- lock/lease released;
- replication observed version V;
- child terminated;
- event with identity X committed.

Then wait on **that** fact. If no event exists, polling can be legitimate provided it polls an authoritative observable condition and has a timeout that fails rather than treating timeout expiry as success.

> Do not wait for time when you mean to wait for cause.
