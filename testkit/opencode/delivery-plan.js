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
export const FAULT_KINDS = ['provider-error', 'disconnect', 'context-overflow'];

/** Which declared fault governs this key, or `null`. */
export function faultFor(faults, key) {
  const matches = (faults ?? []).filter(
    (fault) =>
      (fault.lane === undefined || fault.lane === key.lane) &&
      fault.turn === key.turn &&
      fault.step === key.step,
  );

  if (matches.length === 0) return null;
  if (matches.length > 1) {
    throw new Error(
      `two faults declared for the same (lane, turn, step): ${matches.map((f) => f.kind).join(', ')}`,
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

// ── the delivery counter ────────────────────────────────────────────────────
//
// State, and named as such. It counts physical deliveries per key, which is the
// only quantity in the forest that a request cannot report about itself: the
// request looks identical on attempt 1 and attempt 3.

/** Key text for the counter map. `\u001f` cannot occur in a lane or turn. */
const counterKey = (key) => `${key.lane ?? ''}\u001f${key.turn ?? ''}\u001f${key.step}`;

export const emptyDeliveries = () => new Map();

/**
 * Record one physical delivery and return its one-based attempt number.
 *
 * Called on every arrival, fault or not. Counting only the successes would make
 * `attempts = [1, 2]` unreachable — the second delivery would still be attempt 1.
 */
export function recordDelivery(deliveries, key) {
  const text = counterKey(key);
  const attempt = (deliveries.get(text) ?? 0) + 1;
  deliveries.set(text, attempt);
  return attempt;
}

/** How many deliveries this key has seen, without recording one. */
export const deliveriesOf = (deliveries, key) => deliveries.get(counterKey(key)) ?? 0;

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
