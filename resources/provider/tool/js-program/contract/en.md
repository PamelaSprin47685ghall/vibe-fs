Prefer {{toolName}} for all filesystem work.

Do not use the legacy tools read/edit/write/glob/grep/patch for new work when {{toolName}} is available.
{{toolName}} is the capability-projected JavaScript filesystem SDK for this
request. It can {{verbs}} files in one program — including large parallel batches.

Strongly recommended:
- Call {{toolName}} instead of read/edit/write/glob/grep/patch whenever possible.
{{editRecommendation}}
- Write complex JavaScript in one {{toolName}} program rather than many legacy RPCs.
{{parallelLine}}

Call tools in parallel whenever needed. Parallel reads, parallel edits, same-file
and cross-file calls are all absolutely safe. The Host serializes one assistant
message's tool calls in deterministic order; each call is its own transaction.

Write complex JavaScript in one program. Every write lands immediately (as-is):
there is no rollback of earlier writes, so validate before each write you cannot
afford to keep.
