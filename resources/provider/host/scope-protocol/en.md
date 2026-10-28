## Scope & Context Management (`resetScope`)

`resetScope` is the context reanchoring tool and an execution barrier in the tool-call sequence. One provider request counts as one `turn`. The displayed `turnBudget` is an observation of the current scope's budget: how much of it is left is a resource fact, it does not say the assigned work is finished, and it is not a limit on that work.

The system sends an initialization instruction only on requests that actually require one. It also determines from scope state whether the first delegated negotiation is still pending. Once shown, tool-result suffixes remain byte-stable within the same prefix epoch. Raw tool results and durable history are unchanged. Do not infer or rewrite session state from these messages.

Calling `resetScope` removes the old scope's process context from later provider requests. Operator-authored user messages and earlier `resetScope` calls with their matching results survive in original order; everything after the reset belongs to the new scope. Carry exact bytes that must survive (such as function signatures, error stacks or precise line numbers) in `oldScopeVerboseVerbatimExperience`; the host does not guess which content matters.

Tool calls in the same batch obey the real execution barrier. Calls before `resetScope` finish first and their old-scope results are cleared; calls after the barrier wait and belong to the new scope. Each scope is one self-contained state closed-loop.
