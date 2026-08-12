# retry-not-idempotent — Main

Make logical identity survive transport repetition.

Allocate an idempotency identity before the first side effect and reuse it on every retry of the same intention. The receiver — or the closest boundary that truly owns the side effect — must use that identity to collapse duplicate attempts into one effect.

The invariant is:

> **Many physical attempts for one logical intention produce at most one business effect.**

That usually requires more than a client-side retry wrapper. If the remote system can receive both attempts, deduplication must exist where the effect is committed or at a protocol layer that can prevent duplicate commitment.

Good patterns include:

- provider-supported idempotency keys persisted before first attempt;
- natural business keys with create-if-absent semantics;
- command IDs recorded atomically with the committed effect;
- inbox/dedup tables where `command_id` and effect commit share one transaction boundary;
- replay returning the original committed outcome or an explicit already-applied result.

When the provider cannot support idempotency and the first attempt has uncertain outcome, the honest repair may be **do not retry automatically**. Surface `UnknownOutcome`, reconcile by reading authoritative state, and require a new explicit intention before creating another effect.

Do not invent a new request ID on every attempt. That tells the receiver each retry is a new business action — exactly the opposite of what retry means.

Common fake repairs:

- “duplicates are rare”;
- retry only once;
- dedupe by a short time window or payload similarity;
- search logs after the fact and remove duplicate records while an external charge/email/publication already escaped;
- assign the idempotency key inside the retry loop;
- rely on process-local memory for dedupe when restart can replay the command;
- record the dedupe key after performing the effect, leaving a crash window between effect and identity record;
- treat HTTP method names as proof (`PUT` can still be non-idempotent if the server gives it additive semantics).

Verification must include acknowledgement loss, because that is the case naive retry logic is designed to mishandle:

1. deliver request K;
2. commit the side effect;
3. suppress/drop the response;
4. retry K;
5. assert exactly one logical effect exists and the second attempt resolves consistently.

Also test concurrent duplicate delivery of K, restart between effect and replay, and a truly new request K2 with identical payload. K2 must remain capable of creating a second effect; dedupe must collapse identity, not merely similar content.

If a dedupe record and effect are stored separately, inject failure between them. If that produces either duplicate effect or permanent false suppression, the atomicity boundary is wrong.

You are done when retry policy can be aggressive within its transport budget without altering business multiplicity. The system may see one attempt or ten; the domain still sees one intention.

> Idempotency is not “running twice usually looks okay.” It is a protocol-level proof that repetition preserves business meaning.
