/**
 * gate-delivery-cases.mjs — transport faults stay orthogonal to content.
 *
 * VERIFY-003. The property under test is not "faults work" but "declaring a fault
 * changes nothing about which content edge is selected". That is what makes content
 * a pure function of the request rather than a function of the request AND how many
 * times it has failed so far.
 *
 * `design-script-forest.md` §14 lists the old form among the seven patches that each
 * landed on the wrong layer: expressing a transport fact (retry) inside a content
 * edge (`respond.type = "error"`) forced the seal cache to become non-idempotent,
 * because caching seal→error would have trapped every retry on the failure forever.
 */

import { assertEq, assertTrue } from './lib.mjs';
import {
  deliveriesOf,
  deliveryOutcome,
  emptyDeliveries,
  faultFor,
  recordDelivery,
  faultBody,
  validateFault,
} from '../../e2e/support/delivery-plan.js';
import { resolveEntry } from '../../e2e/support/runtime-key.js';

const SESSION = 'ses_real_1';
const BINDINGS = new Map([['coder', SESSION]]);

const user = (text) => ({ role: 'user', content: text });
const request = (text) => ({ messages: [user(text)] });

const TURN = 'Round 1 fallback attempt.';
const OTHER_TURN = 'Round 2 fallback attempt.';

// Two declarations, and `round1x` deliberately shares a prefix with `round1`. That overlap
// is what the "no spreading" cases below need: under text keying a fault on the shorter
// declaration silently covered the longer one.
const ENTRIES = [
  { id: 'round1', lane: 'coder', turn: TURN, step: 0, respond: { text: 'done' } },
  { id: 'round1x', lane: 'coder', turn: `${TURN} Extended.`, step: 0, respond: { text: 'extended' } },
  { id: 'round2', lane: 'coder', turn: OTHER_TURN, step: 0, respond: { text: 'done 2' } },
  { id: 'round1step1', lane: 'coder', turn: TURN, step: 1, respond: { text: 'after' } },
];

const entryOf = (id) => ENTRIES.find((entry) => entry.id === id);

/**
 * A COMPILED fault, as `faultFor` consumes it: it names the entry it governs.
 *
 * Deliberately a different shape from `sourceFault` below. The author writes turn and step
 * NAMES; the compiler resolves them to one entry. Sharing a fixture between the two layers
 * is what let the old cases assert `faultFor` against author-shaped input, which is a shape
 * the runtime never sees.
 */
const fault = (overrides = {}) => ({
  entryId: 'round1',
  lane: 'coder',
  attempts: [1, 2],
  kind: 'provider-error',
  status: 500,
  ...overrides,
});

/** A SOURCE fault, as an author writes it and `validateFault` checks it. */
const sourceFault = (overrides = {}) => ({
  turn: 'round1',
  step: 0,
  attempts: [1, 2],
  kind: 'provider-error',
  status: 500,
  ...overrides,
});

/**
 * One arrival, in the order the provider path uses: resolve WHICH declaration answers
 * this request, then count that declaration's delivery, then ask the plan.
 *
 * The old helper keyed the counter and the fault lookup on `runtimeKeyOf(body)` — the
 * REQUEST text. That is what made every real fault inert: a declaration is a prefix, so
 * the declared text equals the request text only when the author wrote the utterance out
 * in full, which every fixture here happened to do.
 */
const arrive = (deliveries, faults, body) => {
  const resolved = resolveEntry(body, ENTRIES, BINDINGS, { sessionId: SESSION });
  const entry = resolved.matched;
  const attempt = entry === undefined ? 0 : recordDelivery(deliveries, entry);
  return {
    key: resolved.key,
    entry: resolved,
    attempt,
    outcome: deliveryOutcome(faultFor(faults, entry), attempt),
  };
};

export const deliveryCases = [
  // ── the orthogonality claim ───────────────────────────────────────────────

  {
    name: 'VERIFY-003 a retry re-selects the same content edge',
    fn: () => {
      // The whole reason faults are declared separately. Under the old form the
      // failing attempt and the succeeding one were two edges with two ids, and the
      // author had to keep their predicates in step by hand.
      const deliveries = emptyDeliveries();
      const faults = [fault()];

      const selected = [];
      for (let i = 0; i < 3; i += 1) {
        const arrival = arrive(deliveries, faults, request(TURN));
        selected.push(arrival.entry.matched?.id);
      }

      assertEq(selected.join(','), 'round1,round1,round1', 'content selection must not depend on attempt');
    },
  },

  {
    name: 'VERIFY-003 the declared attempts fault and the next one delivers',
    fn: () => {
      const deliveries = emptyDeliveries();
      const faults = [fault({ attempts: [1, 2] })];

      const outcomes = [];
      for (let i = 0; i < 4; i += 1) {
        outcomes.push(arrive(deliveries, faults, request(TURN)).outcome.deliver === true ? 'deliver' : 'fault');
      }

      assertEq(outcomes.join(','), 'fault,fault,deliver,deliver');
    },
  },

  {
    name: 'VERIFY-003 no declared fault means every delivery goes through',
    fn: () => {
      const deliveries = emptyDeliveries();

      for (let i = 0; i < 3; i += 1) {
        assertTrue(arrive(deliveries, [], request(TURN)).outcome.deliver === true, 'unfaulted keys always deliver');
      }
    },
  },

  {
    name: 'VERIFY-003 a fault at a later attempt lets the earlier ones through',
    fn: () => {
      // Not every fault is a leading one. A provider that works twice and then
      // fails is what a mid-conversation outage looks like.
      const deliveries = emptyDeliveries();
      const faults = [fault({ attempts: [3] })];

      const outcomes = [];
      for (let i = 0; i < 4; i += 1) {
        outcomes.push(arrive(deliveries, faults, request(TURN)).outcome.deliver === true ? 'deliver' : 'fault');
      }

      assertEq(outcomes.join(','), 'deliver,deliver,fault,deliver');
    },
  },

  // ── the counter counts physical deliveries ───────────────────────────────

  {
    name: 'VERIFY-003 every arrival is counted, faulted ones included',
    fn: () => {
      // Counting only the successes would make `attempts = [1, 2]` unreachable: the
      // second arrival would still be attempt 1 and would fault forever.
      const deliveries = emptyDeliveries();
      const faults = [fault({ attempts: [1, 2] })];

      const attempts = [];
      for (let i = 0; i < 3; i += 1) attempts.push(arrive(deliveries, faults, request(TURN)).attempt);

      assertEq(attempts.join(','), '1,2,3');
    },
  },

  {
    name: 'VERIFY-003 the counter is per key, not global',
    fn: () => {
      // Two different declarations each start at attempt 1. A global counter would make the
      // second turn's first delivery look like a retry of the first turn.
      const deliveries = emptyDeliveries();

      assertEq(arrive(deliveries, [], request(TURN)).attempt, 1);
      assertEq(arrive(deliveries, [], request(OTHER_TURN)).attempt, 1, 'a different turn starts fresh');
      assertEq(arrive(deliveries, [], request(TURN)).attempt, 2);
      assertEq(arrive(deliveries, [], request(OTHER_TURN)).attempt, 2);
    },
  },

  {
    name: 'VERIFY-003 counting is the only state, and it is observable',
    fn: () => {
      // `deliveriesOf` reads without recording. A diagnostic that had to record in
      // order to report would change the thing it was reporting.
      const deliveries = emptyDeliveries();
      const entry = entryOf('round1');

      assertEq(deliveriesOf(deliveries, entry), 0);
      recordDelivery(deliveries, entry);
      assertEq(deliveriesOf(deliveries, entry), 1);
      assertEq(deliveriesOf(deliveries, entry), 1, 'reading must not count');
    },
  },

  // ── fault selection is keyed, not scored ────────────────────────────────

  {
    name: 'VERIFY-003 a fault governs exactly the step it names',
    fn: () => {
      const faults = [fault()];

      assertTrue(faultFor(faults, entryOf('round1')) !== null, 'the named step');
      assertTrue(faultFor(faults, entryOf('round1step1')) === null, 'another step is another point');
      assertTrue(faultFor(faults, entryOf('round2')) === null, 'another turn');
      assertTrue(faultFor(faults, undefined) === null, 'an unresolved request has no fault');
    },
  },

  {
    name: 'VERIFY-003 a fault cannot spread to a declaration that shares its prefix',
    fn: () => {
      // Content uses longest-prefix so a scenario can declare a short distinctive fragment.
      // A fault must not spread that way — one declaration would silently cover every later
      // turn starting with the same words.
      //
      // This used to be enforced by comparing text with `===`, which is where it went wrong:
      // the fault compared its DECLARED text against the REQUEST text, and since a
      // declaration is a prefix the two matched only when the author wrote the utterance out
      // in full. Real faults were inert; this case passed because its fixtures did exactly
      // that.
      //
      // Naming the entry makes the property structural rather than asserted: `resolveEntry`
      // has already picked one declaration, and the fault either names it or does not.
      const faults = [fault({ entryId: 'round1' })];

      assertTrue(faultFor(faults, entryOf('round1')) !== null);
      assertTrue(
        faultFor(faults, entryOf('round1x')) === null,
        'the longer declaration is a different step, so the fault does not reach it',
      );

      // And end to end: a request matching the longer declaration is delivered, not faulted.
      const deliveries = emptyDeliveries();
      const extended = arrive(deliveries, faults, request(`${TURN} Extended.`));

      assertEq(extended.entry.matched.id, 'round1x');
      assertEq(extended.outcome.deliver, true);
    },
  },

  {
    name: 'VERIFY-003 two faults for one key is an error, not a precedence question',
    fn: () => {
      // Same shape as `ambiguousTurn`: two declarations for one point mean the
      // scenario does not say what the transport does. Picking one would answer a
      // question the author never answered.
      const faults = [fault({ kind: 'provider-error' }), fault({ kind: 'disconnect' })];

      let threw = null;
      try {
        faultFor(faults, entryOf('round1'));
      } catch (error) {
        threw = error.message;
      }

      assertTrue(threw !== null, 'a duplicate fault declaration must throw');
      assertTrue(threw.includes('provider-error') && threw.includes('disconnect'), 'the message names both');
    },
  },

  {
    name: 'VERIFY-003 a lane-less fault applies to any lane',
    fn: () => {
      const faults = [{ turn: TURN, step: 0, attempts: [1], kind: 'provider-error' }];

      assertTrue(faultFor(faults, { lane: 'coder', turn: TURN, step: 0 }) !== null);
      assertTrue(faultFor(faults, { lane: 'anything', turn: TURN, step: 0 }) !== null);
    },
  },

  // ── load-time validation ─────────────────────────────────────────────────

  {
    name: 'VERIFY-003 an empty attempts list is rejected at load time',
    fn: () => {
      // A fault that never fires is a step the author believes is covered and is
      // not. Silently accepting it is how a scenario stops testing what it claims.
      const problems = validateFault(sourceFault({ attempts: [] }));

      assertEq(problems.length, 1);
      assertTrue(problems[0].includes('never fires'), problems[0]);
    },
  },

  {
    name: 'VERIFY-003 attempt numbers are one-based and distinct',
    fn: () => {
      assertTrue(validateFault(sourceFault({ attempts: [0, 1] })).some((p) => p.includes('one-based')), 'zero rejected');
      assertTrue(validateFault(sourceFault({ attempts: [-1] })).some((p) => p.includes('one-based')), 'negative rejected');
      assertTrue(validateFault(sourceFault({ attempts: [1, 1] })).some((p) => p.includes('distinct')), 'duplicate rejected');
      assertEq(validateFault(sourceFault({ attempts: [1, 3] })).length, 0, 'gaps are legitimate');
    },
  },

  {
    name: 'VERIFY-003 only the declared fault kinds exist',
    fn: () => {
      // A typo'd kind must not become a silently inert declaration. Fault kinds are
      // transport facts, independent from the content selected for the request.
      assertEq(validateFault(sourceFault()).length, 0);
      assertEq(validateFault(sourceFault({ kind: 'disconnect' })).length, 0);
      assertEq(validateFault(sourceFault({ kind: 'context-overflow' })).length, 0);
      assertEq(validateFault(sourceFault({ kind: 'never-end' })).length, 0);

      const problems = validateFault(sourceFault({ kind: 'provider_error' }));
      assertEq(problems.length, 1);
      assertTrue(problems[0].includes('unknown fault kind'), problems[0]);
    },
  },

  {
    name: 'VERIFY-003 a fault must name a turn and a step',
    fn: () => {
      // Without both it is not a point in a conversation, and it would apply to
      // whatever happened to match — the "spreads by accident" failure again.
      assertTrue(validateFault(sourceFault({ turn: '' })).some((p) => p.includes('name the turn')));
      assertTrue(validateFault(sourceFault({ turn: undefined })).some((p) => p.includes('name the turn')));
      assertTrue(validateFault(sourceFault({ step: -1 })).some((p) => p.includes('non-negative')));
      assertTrue(validateFault(sourceFault({ step: 1.5 })).some((p) => p.includes('non-negative')));
    },
  },

  {
    name: 'VERIFY-003 no fault kind can express a content change',
    fn: () => {
      // The boundary this file exists to hold. A transport fault says the delivery
      // failed; it never says the model said something different. A kind like
      // `alternate-response` would put content back into the fault layer.
      const contentish = ['text', 'tool-call', 'title', 'alternate-response', 'empty-assistant'];

      for (const kind of contentish) {
        assertTrue(
          validateFault(sourceFault({ kind })).some((p) => p.includes('unknown fault kind')),
          `'${kind}' is content and must not be a fault kind`,
        );
      }
    },
  },
  // ── rendering a fault the Host will actually classify ─────────────────────

  {
    name: 'FALLBACK-009 a retryable fault renders a body the Host retries',
    fn: () => {
      // `../opencode/packages/opencode/src/session/message-v2.ts:706` hands the body to
      // `ProviderError.parseStreamError`, which (`provider/error.ts:102`) returns undefined
      // unless the TOP-LEVEL `type` is "error", then switches on `body.error.code`.
      // `server_error` is the branch that yields `isRetryable: true`.
      const body = faultBody({ kind: 'provider-error', status: 500, retryable: true });

      assertEq(body.type, 'error', 'a body without top-level type is not parsed at all');
      assertEq(body.error.code, 'server_error');
    },
  },

  {
    name: 'FALLBACK-009 a non-retryable fault renders a body the Host gives up on',
    fn: () => {
      // `invalid_prompt` yields `isRetryable: false`, so the Host stops and the plugin must
      // continue the Logical Run itself (`src/Wanxiangshu/Application/Reconciliation/TurnCompletionProgram.fs:92`).
      // That is the mechanism `fallback-aabb-trace` depends on to observe four attempts.
      const body = faultBody({ kind: 'provider-error', status: 400, retryable: false });

      assertEq(body.type, 'error');
      assertEq(body.error.code, 'invalid_prompt');
    },
  },

  {
    name: 'FALLBACK-009 the retired isRetryable body field was never read',
    fn: () => {
      // Measured in K9. Every JSON fault wrote its intent as `body.error.isRetryable`:
      //
      //   { "error": { "message": "aabb-fail-1", "type": "invalid_request_error",
      //                "isRetryable": false } }
      //
      // No top-level `type`, no `code` — so `parseStreamError` bailed and the decision fell
      // through to the AI SDK's own `e.isRetryable`, derived from the HTTP status. The field
      // was decoration that happened to agree with the status in both scenarios using it. A
      // scenario declaring 500 with `isRetryable: false` would have behaved as retryable
      // while reading as terminal.
      //
      // The rendered body carries no such field: the declaration is the only source.
      const body = faultBody({ kind: 'provider-error', status: 400, retryable: false });

      assertTrue(body.error.isRetryable === undefined, 'no second source of truth');
      assertTrue(body.error.code !== undefined, 'the code is what the Host actually reads');
    },
  },

  {
    name: 'CTX-005 a context-overflow fault is a different outcome, not a retryable error',
    fn: () => {
      // `parseStreamError`'s `context_length_exceeded` branch raises `ContextOverflowError`
      // rather than an API error, so it never reaches the retry decision at all. CTX-006's
      // recovery slot is what responds to it — which is why it cannot be spelled as a
      // provider-error with a status.
      const body = faultBody({ kind: 'context-overflow' });

      assertEq(body.error.code, 'context_length_exceeded');
    },
  },
];
