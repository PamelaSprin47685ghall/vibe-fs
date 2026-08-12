# sleep-based-synchronization — Main

Replace the duration with the fact.

First write down what the sleep is pretending to establish. If the answer is readiness, completion, visibility, shutdown, propagation, lock release, or ownership transfer, expose an observation that can actually prove that condition.

Preferred mechanisms, in descending honesty:

1. await the operation's own completion/result;
2. subscribe to the event that establishes the condition;
3. await a readiness/termination primitive owned by the subsystem;
4. poll an authoritative state/version with a bounded timeout when no event can be exposed.

In every case, timeout has one job: **bound how long uncertainty may persist**. It does not turn uncertainty into success when the clock expires.

For process startup, wait for a real readiness signal, health endpoint with the right semantics, bound port + protocol handshake, or emitted ready event — not “the process has existed for two seconds.”

For process shutdown, await exit/termination and resource release — not “we sent SIGTERM and slept a little.”

For storage/replication, wait for a generation/version/commit identity that proves the required fact is visible — not “eventual consistency probably settled.”

For tests, prefer deterministic fakes/controllable schedulers when the timing source itself is not what is under test. A test that needs 30 seconds of wall-clock patience to prove a local state transition is usually testing the scheduler as much as the product.

Common fake repairs:

- increase 500 ms to 5 s;
- replace one long sleep with ten short sleeps but never inspect a causal state;
- busy-loop until the same wall-clock duration expires;
- keep the sleep “for stability” after adding a readiness signal;
- add retry after the sleep, so flakiness becomes less frequent but the missing synchronization remains;
- use a timeout callback that marks the operation successful because “nothing bad happened yet.”

Verification should attack scheduler assumptions. Artificially delay the producer well beyond the old sleep: the consumer must not advance early. Then make the producer complete immediately: the consumer should proceed immediately rather than paying the old fixed latency.

For polling, verify two sides:

- the condition becoming true wakes progress promptly;
- timeout expiry produces an explicit failure/unknown outcome, never a counterfeit success.

For event-based waits, test missed-event races: establish whether subscription happens before the event can fire, or whether current state is checked in a way that closes the gap. Replacing sleep with a callback while introducing “subscribe-after-complete” is not an improvement.

You are done when the code can explain every wait as:

> I cannot proceed until **this observable fact** becomes true; the clock only limits how long I am willing to remain uncertain.

That sentence is synchronization. “We wait because this is usually enough time” is probability wearing a causal costume.
