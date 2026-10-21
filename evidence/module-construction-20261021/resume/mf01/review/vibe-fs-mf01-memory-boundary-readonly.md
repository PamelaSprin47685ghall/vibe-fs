# MF-01 memory boundary: read-only decision

This note does not establish a memory limit and does not change any runtime flag, source, test policy or production configuration. The current isolated guest work addresses execution ownership, RPC, suspension and deadlines. It cannot close repository-programming WHAT[006]'s memory obligation by implication.

The exact Node 22.13.0 documentation says worker `resourceLimits` constrain the JavaScript engine rather than external data such as ArrayBuffers; a global out-of-memory condition may still abort the process. Therefore this thread option alone is not a complete process-memory containment argument. [Node 22.13.0 Worker options](https://nodejs.org/download/release/v22.13.0/docs/api/worker_threads.html#new-workerfilename-options).

Bun's current runtime documentation describes `--smol` as more frequent garbage collection with a performance tradeoff; its Worker documentation describes the small heap configuration. These documents do not supply this candidate with an experimentally verified hard cap. They describe the current documented runtime, while the tested binary here remains Bun 1.3.14; the note does not infer a new-version guarantee for that installed binary. [Bun runtime](https://bun.sh/docs/runtime#bun-run---smol), [Bun Workers](https://bun.sh/docs/runtime/workers#memory-usage-with-smol).

The inference is limited: neither the present independent-process prototype nor the two settings above justify claiming the existing full memory requirement passed. This round has not verified a process-wide OS-enforced cap on either supported platform. Sampling RSS, observing stable soak memory, or successfully killing one bounded loop does not establish that cap. No memory-exhaustion experiment was performed for this note.

Before production reception, separately identify the actual enforced memory domain, supported runtime/platform, external allocations, startup overhead, limit-trigger receipt and retained Host resource obligations. Keep the existing memory TODO open until those facts are verified. Do not replace this with a sampled-RSS heuristic or silently drop a currently supported runtime.
