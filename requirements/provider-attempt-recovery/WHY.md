# provider-attempt-recovery — WHY

A failed provider request should not erase the task or create an endless stream of attempts. A budget owned by the Logical Run bounds automatic recovery without charging an unrelated later task. Exact failure identity prevents concurrent observers and late callbacks from spending the same budget twice.

Changing the executor is different from changing who acts and under whose authority. Durable participant identity preserves responsibility, language and context across retries. Provider health and failure count answer different questions: trying LWR on the original target tests whether context caused the failure before excluding that provider.

Host transport activity, unusable response content and confirmed provider failure carry different evidence. Typed policy permission prevents cleanup, ambiguous acceptance or diagnostic prose from creating a new request. RequestKind likewise keeps maintenance and speculative success from pretending that the owner's business task recovered.

A committed failure explains why recovery is needed; it does not prove the Host stopped sending or that a new process may resume a callback. The exact terminal observation prevents overlapping physical attempts. Conversely, acceptance without execution creates a real obligation: either resume that accepted material with the required capability or report its terminal failure.

Recovery may need material that a linked producer is already making. Waiting on that producer's committed progress ties continuation to evidence, rather than timing guesses. Without an open producer there is no corresponding obligation to wait for hypothetical future material.

## DEPENDS ON

- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`
