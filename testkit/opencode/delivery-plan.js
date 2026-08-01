/**
 * delivery-plan.js — transport faults, declared separately from content.
 *
 * VERIFY-003. Content is a pure function of the request; a fault is not content.
 * A provider that returns 500 twice and then succeeds sent ONE conversation over
 * THREE physical deliveries, and the delivery count is genuinely countable — so
 * this is the one place in the forest where counting is legitimate.
 *
 * ── why they must be separate declarations ──────────────────────────────────
 *
 * The old form put the fault inside the content edge (`respond.type = "error"`),
 * which forced two wrong things:
 *
 *   the failing attempt and the succeeding one became DIFFERENT edges, so one
 *   conversation step was written twice with two ids (`round1-failure`,
 *   `round1-retry`) and the author had to keep their `match` predicates in step
 *
 *   `consumeExpectation` had to delete the seal cache entry for an error edge,
 *   because caching seal→error would trap every retry on the failure forever
 *
 * That second one is the deeper damage: it made the content cache non-idempotent
 * to express a transport fact. `design-script-forest.md` §14 lists it among the
 * seven patches that each landed on the wrong layer.
 *
 * Separating them means a retry re-selects the SAME content edge — which is what
 * `retryReselectsSameContent` in the gate asserts, and what makes "content is a
 * pure function" true rather than aspirational.
 */

// ── the plan ────────────────────────────────────────────────────────────────

/**
 * `attempts` lists which physical deliveries of one (lane, turn, step) fail.
 * `[1, 2]` means the first two are refused and the third is delivered.
 *
 * One-based because it counts deliveries, not array slots: "attempt 1" is a phrase
 * an operator reading a diagnostic already understands.
 */
export const FAULT_KINDS = ['provider-error', 'disconnect', 'context-overflow', 'never-end'];

/**
 * Which declared fault governs this ENTRY, or `null`.
 *
 * ── measured: keying by text never fired ────────────────────────────────────
 *
 * This compared `fault.turn === key.turn`, i.e. the DECLARED text against the REQUEST
 * text. A declaration is a prefix — that is the whole point of the lookup — so the two
 * are equal only when the author happened to write the utterance out in full. Every
 * fault in every real scenario was therefore inert.
 *
 * The gate cases did not catch it because their fixtures declare a turn and then send
 * exactly that string, making prefix and equality indistinguishable. Same failure mode
 * as `sessionIdOf` and `kindOf` in K9: a fixture built in the shape the code expects.
 *
 * `resolveEntry` has already decided WHICH declaration answers this request, so the
 * fault belongs to that entry's identity. Comparing ids removes the second matching
 * decision entirely — there is now exactly one place that says which entry a request is.
 */
export function faultFor(faults, entry) {
  if (entry === null || entry === undefined) return null;

  const matches = (faults ?? []).filter((fault) => fault.entryId === entry.id);

  if (matches.length === 0) return null;
  if (matches.length > 1) {
    throw new Error(
      `two faults declared for the same step '${entry.id}': ${matches.map((f) => f.kind).join(', ')}`,
    );
  }
  return matches[0];
}

/**
 * What to do with the Nth physical delivery of this key.
 *
 * `{ deliver: true }` or `{ fault }` — never both, and never a maybe. The caller
 * cannot treat "no plan" and "plan exhausted" differently, which is deliberate:
 * both mean the content edge is delivered as written.
 */
export function deliveryOutcome(fault, attempt) {
  if (fault === null || fault === undefined) return { deliver: true };
  return fault.attempts.includes(attempt) ? { fault } : { deliver: true };
}

// ── rendering a fault the Host will actually classify ───────────────────────
//
// A fault declares `retryable`, and FALLBACK-009 turns on it: a retryable failure means
// the HOST drives the retries and the plugin sends no continuation; a non-retryable one
// means the Host gives up and `TurnCompletionProgram` must carry the Logical Run forward.
// Declaring it is only half the job — the response body has to make the Host agree.
//
// ── measured: the old fault bodies said nothing the Host reads ──────────────
//
// Every JSON fault wrote its intent as `body.error.isRetryable`:
//
//   { "error": { "message": "aabb-fail-1", "type": "invalid_request_error",
//                "isRetryable": false } }
//
// `../opencode/packages/opencode/src/session/message-v2.ts:706` calls
// `ProviderError.parseStreamError`, and that function (`provider/error.ts:102`) returns
// `undefined` unless the body's TOP-LEVEL `type` is `"error"`, then switches on
// `body.error.code`. The old bodies had no top-level `type` and no `code`, so
// `parseStreamError` bailed and the retry decision fell through to the AI SDK's own
// `e.isRetryable`, derived from the HTTP status.
//
// So `isRetryable` in those bodies was decoration: `fallback.json` got its Host-driven
// retries from the 500, and `fallback-aabb-trace.json` got its plugin continuations from
// the 400 — the field agreeing with the status by luck in both cases. A scenario that
// declared 500 + `isRetryable: false` would have silently behaved as retryable.
//
// Synthesising the body from the declaration removes the second source of truth.
const RETRYABLE_CODE = 'server_error';
const TERMINAL_CODE = 'invalid_prompt';

/**
 * The wire body for a declared fault, shaped so the Host's own parser reaches the
 * declared conclusion.
 *
 * `context-overflow` is its own code because HOST-006/CTX-005 make it a different
 * outcome entirely — the Host raises `ContextOverflowError` rather than an API error, and
 * the recovery slot (CTX-006) is what responds to it.
 */
export function faultBody(fault) {
  if (fault.kind === 'context-overflow') {
    return {
      type: 'error',
      error: {
        code: 'context_length_exceeded',
        message: "This model's maximum context length is 100000 tokens.",
        type: 'invalid_request_error',
      },
    };
  }

  return {
    type: 'error',
    error: {
      code: fault.retryable === true ? RETRYABLE_CODE : TERMINAL_CODE,
      message: `declared ${fault.kind} fault (retryable=${fault.retryable === true})`,
      type: 'invalid_request_error',
    },
  };
}

// ── the delivery counter ────────────────────────────────────────────────────
//
// State, and named as such. It counts physical deliveries per key, which is the
// only quantity in the forest that a request cannot report about itself: the
// request looks identical on attempt 1 and attempt 3.

/**
 * Key text for the counter map.
 *
 * Keyed by the resolved ENTRY, not by the request text, for the same reason `faultFor` is:
 * two deliveries of one declaration must land on one counter, and a request that differs
 * from the declaration by a suffix is still that declaration's delivery.
 */
const counterKey = (entry) => `${entry.lane ?? ''}\u001f${entry.id}`;

export const emptyDeliveries = () => new Map();

/**
 * Record one physical delivery and return its one-based attempt number.
 *
 * Called on every arrival, fault or not. Counting only the successes would make
 * `attempts = [1, 2]` unreachable — the second delivery would still be attempt 1.
 */
export function recordDelivery(deliveries, entry) {
  const text = counterKey(entry);
  const attempt = (deliveries.get(text) ?? 0) + 1;
  deliveries.set(text, attempt);
  return attempt;
}

/** How many deliveries this entry has seen, without recording one. */
export const deliveriesOf = (deliveries, entry) => deliveries.get(counterKey(entry)) ?? 0;

// ── load-time validation ────────────────────────────────────────────────────

/**
 * Reasons a fault declaration is rejected before a scenario runs.
 *
 * All five are author errors that would otherwise become silent behaviour: an
 * empty `attempts` list is a fault that never fires, and a fault at an attempt the
 * scenario never reaches is a step the author believes is covered and is not.
 */
export function validateFault(fault) {
  const problems = [];

  if (!FAULT_KINDS.includes(fault.kind)) {
    problems.push(`unknown fault kind '${fault.kind}'; expected one of ${FAULT_KINDS.join(', ')}`);
  }
  if (typeof fault.turn !== 'string' || fault.turn === '') {
    problems.push('fault must name the turn it applies to');
  }
  if (!Number.isInteger(fault.step) || fault.step < 0) {
    problems.push('fault step must be a non-negative integer');
  }
  if (!Array.isArray(fault.attempts) || fault.attempts.length === 0) {
    problems.push('fault must list at least one attempt; an empty list is a fault that never fires');
  } else {
    if (fault.attempts.some((attempt) => !Number.isInteger(attempt) || attempt < 1)) {
      problems.push('fault attempts are one-based integers');
    }
    if (new Set(fault.attempts).size !== fault.attempts.length) {
      problems.push('fault attempts must be distinct');
    }
  }

  return problems;
}
