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

import { assertEq, assertTrue } from './gate-lib.mjs';
import {
  deliveriesOf,
  deliveryOutcome,
  emptyDeliveries,
  faultFor,
  recordDelivery,
  validateFault,
} from '../delivery-plan.js';
import { resolveEntry, runtimeKeyOf } from '../runtime-key.js';

const SESSION = 'ses_real_1';
const BINDINGS = new Map([['fast-coder', SESSION]]);

const user = (text) => ({ role: 'user', content: text });
const request = (text, sessionID = SESSION) => ({ sessionID, messages: [user(text)] });

const TURN = 'Round 1 fallback attempt.';
const ENTRIES = [{ id: 'round1', lane: 'fast-coder', turn: TURN, step: 0, respond: { text: 'done' } }];

const fault = (overrides = {}) => ({
  lane: 'fast-coder',
  turn: TURN,
  step: 0,
  attempts: [1, 2],
  kind: 'provider-error',
  status: 500,
  ...overrides,
});

/** One arrival: count the delivery, then ask the plan what to do with it. */
const arrive = (deliveries, faults, body) => {
  const key = runtimeKeyOf(body, BINDINGS);
  const attempt = recordDelivery(deliveries, key);
  return { key, attempt, outcome: deliveryOutcome(faultFor(faults, key), attempt), entry: resolveEntry(body, ENTRIES, BINDINGS) };
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
      // Two different turns each start at attempt 1. A global counter would make the
      // second turn's first delivery look like a retry of the first turn.
      const deliveries = emptyDeliveries();
      const other = 'Round 2 fallback attempt.';

      assertEq(arrive(deliveries, [], request(TURN)).attempt, 1);
      assertEq(arrive(deliveries, [], request(other)).attempt, 1, 'a different turn starts fresh');
      assertEq(arrive(deliveries, [], request(TURN)).attempt, 2);
      assertEq(arrive(deliveries, [], request(other)).attempt, 2);
    },
  },

  {
    name: 'VERIFY-003 counting is the only state, and it is observable',
    fn: () => {
      // `deliveriesOf` reads without recording. A diagnostic that had to record in
      // order to report would change the thing it was reporting.
      const deliveries = emptyDeliveries();
      const key = runtimeKeyOf(request(TURN), BINDINGS);

      assertEq(deliveriesOf(deliveries, key), 0);
      recordDelivery(deliveries, key);
      assertEq(deliveriesOf(deliveries, key), 1);
      assertEq(deliveriesOf(deliveries, key), 1, 'reading must not count');
    },
  },

  // ── fault selection is keyed, not scored ────────────────────────────────

  {
    name: 'VERIFY-003 a fault applies only to its own lane, turn and step',
    fn: () => {
      const faults = [fault()];

      const at = (key) => faultFor(faults, key);

      assertTrue(at({ lane: 'fast-coder', turn: TURN, step: 0 }) !== null, 'exact key matches');
      assertTrue(at({ lane: 'fast-coder', turn: TURN, step: 1 }) === null, 'another step is another point');
      assertTrue(at({ lane: 'other', turn: TURN, step: 0 }) === null, 'another lane is another conversation');
      assertTrue(at({ lane: 'fast-coder', turn: 'Something else', step: 0 }) === null, 'another turn');
    },
  },

  {
    name: 'VERIFY-003 the turn is matched exactly, not by prefix',
    fn: () => {
      // Content uses longest-prefix so a scenario can declare a short distinctive
      // fragment. A fault must not: a prefix-matched fault would silently cover
      // every later turn that happens to start with the same words.
      const faults = [fault({ turn: 'Round 1' })];

      assertTrue(faultFor(faults, { lane: 'fast-coder', turn: 'Round 1', step: 0 }) !== null);
      assertTrue(
        faultFor(faults, { lane: 'fast-coder', turn: 'Round 1 fallback attempt.', step: 0 }) === null,
        'a fault must not spread by prefix',
      );
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
        faultFor(faults, { lane: 'fast-coder', turn: TURN, step: 0 });
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

      assertTrue(faultFor(faults, { lane: 'fast-coder', turn: TURN, step: 0 }) !== null);
      assertTrue(faultFor(faults, { lane: 'anything', turn: TURN, step: 0 }) !== null);
    },
  },

  // ── load-time validation ─────────────────────────────────────────────────

  {
    name: 'VERIFY-003 an empty attempts list is rejected at load time',
    fn: () => {
      // A fault that never fires is a step the author believes is covered and is
      // not. Silently accepting it is how a scenario stops testing what it claims.
      const problems = validateFault(fault({ attempts: [] }));

      assertEq(problems.length, 1);
      assertTrue(problems[0].includes('never fires'), problems[0]);
    },
  },

  {
    name: 'VERIFY-003 attempt numbers are one-based and distinct',
    fn: () => {
      assertTrue(validateFault(fault({ attempts: [0, 1] })).some((p) => p.includes('one-based')), 'zero rejected');
      assertTrue(validateFault(fault({ attempts: [-1] })).some((p) => p.includes('one-based')), 'negative rejected');
      assertTrue(validateFault(fault({ attempts: [1, 1] })).some((p) => p.includes('distinct')), 'duplicate rejected');
      assertEq(validateFault(fault({ attempts: [1, 3] })).length, 0, 'gaps are legitimate');
    },
  },

  {
    name: 'VERIFY-003 only the declared fault kinds exist',
    fn: () => {
      // A typo'd kind must not become a silently inert declaration. The three kinds
      // are transport facts: a provider error, a dropped stream, a refused request.
      assertEq(validateFault(fault()).length, 0);
      assertEq(validateFault(fault({ kind: 'disconnect' })).length, 0);
      assertEq(validateFault(fault({ kind: 'context-overflow' })).length, 0);

      const problems = validateFault(fault({ kind: 'provider_error' }));
      assertEq(problems.length, 1);
      assertTrue(problems[0].includes('unknown fault kind'), problems[0]);
    },
  },

  {
    name: 'VERIFY-003 a fault must name a turn and a step',
    fn: () => {
      // Without both it is not a point in a conversation, and it would apply to
      // whatever happened to match — the "spreads by accident" failure again.
      assertTrue(validateFault(fault({ turn: '' })).some((p) => p.includes('name the turn')));
      assertTrue(validateFault(fault({ turn: undefined })).some((p) => p.includes('name the turn')));
      assertTrue(validateFault(fault({ step: -1 })).some((p) => p.includes('non-negative')));
      assertTrue(validateFault(fault({ step: 1.5 })).some((p) => p.includes('non-negative')));
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
          validateFault(fault({ kind })).some((p) => p.includes('unknown fault kind')),
          `'${kind}' is content and must not be a fault kind`,
        );
      }
    },
  },
];
