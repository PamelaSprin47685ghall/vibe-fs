# race-first-wins-semantics — Main

First decide whether timing belongs to the product rule at all.

If it does not, remove scheduler order from the outcome. Let concurrency gather information, then decide using stable domain facts: version, priority, quorum, freshness, score, explicit precedence, or a merge law that gives the same result under every permitted arrival permutation.

If first-wins really *is* the protocol, stop treating it as an implementation shortcut. Specify it like a protocol:

- what stable identity is competing;
- what makes a candidate eligible;
- whether stale candidates may win;
- whether success/failure races are equivalent;
- how losers are cancelled or ignored;
- what happens on ties / simultaneous observation;
- whether replay must reproduce the original winner;
- which durable fact records the winner so restart does not rerun the election by accident.

The repair owner is the layer that owns the business choice. A scheduler API such as `race`, `WhenAny`, callback order, or completion queue should never become the hidden owner merely because it is convenient to call.

Prefer deterministic join when all relevant information is required. Parallelism can still buy latency: fetch in parallel, compute in parallel, inspect in parallel. The important point is that **completion order only affects when the decision can be made, not what decision is correct**.

Where a subset is sufficient, make sufficiency explicit. “First two of three matching quorum” is a rule. “Whichever two happen to return first” is only safe if quorum semantics prove all eligible subsets equivalent for the decision being made.

Common fake repairs:

- add `sleep(10)` so the preferred branch tends to finish first;
- raise thread/task priority for the intended winner;
- launch the primary a few milliseconds earlier and rely on head start;
- retry when the “wrong” branch wins, effectively sampling schedules until a preferred answer appears;
- cancel losers before confirming the first result is semantically admissible;
- sort by completion timestamp after the fact and call that deterministic — the timestamp is still scheduler output unless the domain owns it;
- hide the race behind a facade whose result type no longer reveals how the winner was chosen.

Verification should control schedule deliberately. For each set of logical inputs, enumerate meaningful arrival orders and assert either:

- every permutation yields the same outcome; or
- the documented first-wins law predicts the different outcomes exactly.

Then test restart/replay if the decision persists. Once a winner becomes a durable fact, recovery must restore that fact rather than re-running a race whose timing cannot be reproduced.

A useful completion criterion is that a reviewer can answer “why did X win?” without saying “because its future resolved first” unless **that sentence itself is the documented domain law**.

> Performance may choose the fastest route to the facts. It must not smuggle latency in as a substitute for judgment.
