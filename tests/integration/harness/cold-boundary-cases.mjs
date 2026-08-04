/**
 * gate-cold-boundary-cases.mjs — the seal has exactly two declared exceptions.
 *
 * ARCH-004 / VERIFY-003. The property is not "cold boundaries work" but "a break the
 * scenario did not declare is fatal, and a declaration that never fires is fatal too".
 *
 * Package K1 measured what the alternative costs. The deleted `epochCold` exemption
 * read "tools and the leading system message unchanged" and then admitted any body
 * rewrite — so it passed precisely the mutations it existed to catch, and the canaries
 * stayed green. `design-script-forest.md` §14 lists it among the four sniffed
 * exemptions that could green-light a wrong implementation.
 */

import { assertEq, assertTrue } from './lib.mjs';
import { boundaryFor, sealDecision, validateBoundary } from '../../e2e/support/cold-boundary.js';
import { wireOf } from '../../e2e/support/provider-wire.js';

const SYSTEM = { role: 'system', content: 'You are a coder.' };
const user = (text) => ({ role: 'user', content: text });
const assistant = (text) => ({ role: 'assistant', content: text });

const body = (model, messages, tools = ['write']) => ({
  model,
  tools: tools.map((name) => ({ type: 'function', function: { name } })),
  messages,
});

const FIRST = body('test-model', [SYSTEM, user('Round 1')]);
const APPENDED = body('test-model', [SYSTEM, user('Round 1'), assistant('r1'), user('Round 2')]);

/** FALLBACK-004: the model moves, the transcript does not. */
const SIDE_SWITCHED = body('test-model-b', [SYSTEM, user('Round 1'), assistant('r1'), user('Round 2')]);

/** A side switch that also rewrote history — what the old exemption would have passed. */
const SIDE_SWITCHED_AND_REWRITTEN = body('test-model-b', [SYSTEM, user('DIFFERENT'), user('Round 2')]);

/** COMPANION-009: the prefix is replaced by a companion-memory head. */
const EPOCH_REBASED = body('test-model', [SYSTEM, user('[companion memory]'), user('Round 2')]);

const decide = (previous, next, boundary = null) =>
  sealDecision({ previousWire: previous === null ? null : wireOf(previous), body: next, boundary });

/** A SOURCE boundary, as an author writes it and `validateBoundary` checks it. */
const at = (kind) => ({ kind, lane: 'fast-coder', turn: 'Round 2', step: 0 });

/**
 * A COMPILED boundary, as `boundaryFor` consumes it: it names the entry it governs.
 *
 * Deliberately a different shape from `at`. Sharing one fixture across both layers is what
 * let the lookup cases assert against author-shaped input the runtime never sees — and hid
 * that the lookup compared DECLARED text to REQUEST text, so every real boundary was inert.
 */
const compiledAt = (kind, entryId = 'round2') => ({ kind, lane: 'fast-coder', entryId });

const entry = (id) => ({ id, lane: 'fast-coder' });

export const coldBoundaryCases = [
  // ── the ordinary case ─────────────────────────────────────────────────────

  {
    name: 'ARCH-004 an append-only continuation keeps the seal',
    fn: () => {
      assertEq(decide(FIRST, APPENDED).held, true);
      assertEq(decide(FIRST, FIRST).held, true, 'an unchanged request keeps the seal');
    },
  },

  {
    name: 'ARCH-004 the first request of a session has nothing to break',
    fn: () => {
      assertEq(decide(null, EPOCH_REBASED).held, true);
    },
  },

  {
    name: 'ARCH-004 an undeclared break is fatal',
    fn: () => {
      // No sniffing. Every shape below is a real cache break, and none of them earns
      // an exemption from looking plausible.
      assertEq(decide(FIRST, EPOCH_REBASED).broken, 'undeclared', 'rewritten prefix');
      assertEq(decide(FIRST, SIDE_SWITCHED).broken, 'undeclared', 'model change alone');
      assertEq(
        decide(FIRST, body('test-model', [SYSTEM, user('Round 1')], ['write', 'read'])).broken,
        'undeclared',
        'tool set change',
      );
      assertEq(
        decide(FIRST, body('test-model', [{ role: 'system', content: 'Other.' }, user('Round 1')])).broken,
        'undeclared',
        'system prompt change',
      );
    },
  },

  {
    name: 'ARCH-004 shrinking the transcript is a break, not a continuation',
    fn: () => {
      // A shorter request means the plugin dropped messages the provider already saw.
      // COMPANION-009 exists to make that explicit rather than silent.
      assertEq(decide(APPENDED, FIRST).broken, 'undeclared');
    },
  },

  // ── COMPANION-009: epoch switch ──────────────────────────────────────────

  {
    name: 'COMPANION-009 a declared epoch switch reseals a rebased prefix',
    fn: () => {
      assertEq(decide(FIRST, EPOCH_REBASED, at('epoch-switch')).resealed, 'epoch-switch');
    },
  },

  // ── FALLBACK-004: side switch is narrower than the old exemption ─────────

  {
    name: 'FALLBACK-004 a declared side switch admits a model change only',
    fn: () => {
      assertEq(decide(FIRST, SIDE_SWITCHED, at('fallback-side')).resealed, 'fallback-side');
    },
  },

  {
    name: 'FALLBACK-004 a side switch may not rewrite the transcript',
    fn: () => {
      // The tightening this file's measurement produced. `modelSideCold` allowed the
      // system prompt to change whenever the model id did — but AGENT-001 gives
      // `fast-ROLE` and `deep-ROLE` ONE byte-identical system prompt (verified for
      // coder/manager/reviewer/devops/inspector), so a real side switch moves the
      // model field and nothing else.
      //
      // Declaring `fallback-side` therefore cannot smuggle a message rewrite past the
      // barrier, which is exactly what the old exemption permitted.
      assertEq(
        decide(FIRST, SIDE_SWITCHED_AND_REWRITTEN, at('fallback-side')).broken,
        'fallback-side-rewrote-messages',
      );
    },
  },

  {
    name: 'FALLBACK-004 a side switch may not change tools either',
    fn: () => {
      const retooled = body('test-model-b', [SYSTEM, user('Round 1')], ['write', 'read']);
      assertEq(decide(FIRST, retooled, at('fallback-side')).broken, 'fallback-side-rewrote-messages');
    },
  },

  // ── CTX-010: prefix probe ────────────────────────────────────────────────

  {
    name: 'CTX-010 a declared prefix probe admits a rebased prefix with fixed system/tools',
    fn: () => {
      // The probe replaces the covered head with the synthetic companion memory and
      // keeps the live tail; the system prompt and the tool set are the attempt's
      // fixed parts (PROMPT-008) and must survive byte-identical.
      const probed = body('test-model-b', [SYSTEM, user('[companion memory]'), user('Round 2')]);
      assertEq(decide(FIRST, probed, at('prefix-probe')).resealed, 'prefix-probe');
    },
  },

  {
    name: 'CTX-010 a prefix probe may not rewrite the tool set',
    fn: () => {
      // The tools belong to the attempt profile (PROMPT-008); a probe that swapped
      // them would be changing what the model may call, not rebasing the covered
      // prefix. The system prompt is deliberately exempt: Host 1.18.9 injects the
      // model name into it (system.ts:67), so a fallback side switch — the usual
      // companion of a recovery attempt — changes the system bytes by construction.
      const retooled = body('test-model-b', [SYSTEM, user('[companion memory]'), user('Round 2')], ['write', 'read']);
      assertEq(
        decide(FIRST, retooled, at('prefix-probe')).broken,
        'prefix-probe-rewrote-fixed',
        'tool rewrite is not a probe',
      );

      // A side switch with a rebased prefix and unchanged tools is exactly the
      // recovery shape, and it is admitted.
      const sideSwitchedProbe = body('test-model-b', [SYSTEM, user('[companion memory]'), user('Round 2')]);
      assertEq(decide(FIRST, sideSwitchedProbe, at('prefix-probe')).resealed, 'prefix-probe');
    },
  },

  {
    name: 'CTX-010 an append-only delivery of a probe entry is legal',
    fn: () => {
      // A recovery sequence alternates probe slots and ordinary slots (FALLBACK-012
      // arms only odd offsets), so the same entry delivers breaking and
      // non-breaking requests. The "never fired" check lives at scenario end.
      const decision = decide(FIRST, APPENDED, at('prefix-probe'));
      assertEq(decision.held, true);
    },
  },

  // ── a declaration that never fires is also fatal ─────────────────────────

  {
    name: 'VERIFY-003 a declared boundary that did not break is fatal',
    fn: () => {
      // Same reasoning as an empty `attempts` list: the author believes a cold
      // boundary is covered, and the scenario silently stopped exercising it. Treating
      // it as harmless is how a scenario decays into an assertion about nothing.
      for (const kind of ['epoch-switch', 'fallback-side']) {
        const decision = decide(FIRST, APPENDED, at(kind));
        assertEq(decision.broken, 'boundary-not-reached', `${kind} declared but seal held`);
        assertEq(decision.kind, kind, 'the diagnostic names which declaration went unused');
      }
    },
  },

  {
    name: 'VERIFY-003 a boundary declared on the first request is unreachable',
    fn: () => {
      // Nothing is sealed yet, so no break can happen there. Accepting it silently
      // would make the declaration decorative.
      assertEq(decide(null, FIRST, at('epoch-switch')).broken, 'boundary-not-reached');
    },
  },

  // ── declaration lookup is keyed, never inferred ──────────────────────────

  {
    name: 'VERIFY-003 a boundary governs exactly the step it names',
    fn: () => {
      const boundaries = [compiledAt('epoch-switch')];

      assertTrue(boundaryFor(boundaries, entry('round2')) !== null, 'the named step');
      assertTrue(boundaryFor(boundaries, entry('round2step1')) === null, 'another step');
      assertTrue(boundaryFor(boundaries, entry('round3')) === null, 'another turn');
      assertTrue(boundaryFor(boundaries, undefined) === null, 'an unresolved request has no boundary');
    },
  },

  {
    name: 'VERIFY-003 a boundary cannot spread to a declaration sharing its prefix',
    fn: () => {
      // A prefix-matched boundary would excuse every later turn that happens to start with
      // the same words — one declaration silently covering a whole conversation.
      //
      // This used to be asserted by comparing text with `===`, and that was the defect: the
      // lookup compared its DECLARED turn against the REQUEST turn, and a declaration is a
      // prefix, so they matched only when the author wrote the utterance out in full. Every
      // cold boundary in every real scenario was inert, and this case passed because its
      // fixtures did exactly that.
      //
      // Naming the entry makes it structural: `resolveEntry` picks one declaration, and the
      // boundary either names it or does not.
      const boundaries = [compiledAt('epoch-switch', 'round')];

      assertTrue(boundaryFor(boundaries, entry('round')) !== null);
      assertTrue(boundaryFor(boundaries, entry('round2')) === null, 'a boundary must not spread by prefix');
    },
  },

  {
    name: 'VERIFY-003 two boundaries for one key is an error, not a precedence question',
    fn: () => {
      const boundaries = [compiledAt('epoch-switch'), compiledAt('fallback-side')];

      let threw = null;
      try {
        boundaryFor(boundaries, entry('round2'));
      } catch (error) {
        threw = error.message;
      }

      assertTrue(threw !== null, 'a duplicate declaration must throw');
      assertTrue(threw.includes('epoch-switch') && threw.includes('fallback-side'), 'the message names both');
    },
  },

  // ── load-time validation ─────────────────────────────────────────────────

  {
    name: 'ARCH-004 only the three named kinds exist',
    fn: () => {
      assertEq(validateBoundary(at('epoch-switch')).length, 0);
      assertEq(validateBoundary(at('fallback-side')).length, 0);
      assertEq(validateBoundary(at('prefix-probe')).length, 0);

      // Every rejected name below is a sniffed exemption from the old matcher or a
      // capacity-driven switch CTX-001/CTX-002 forbid outright. `prefix-reset` is the
      // old matcher's unvalidated exemption; `prefix-probe` differs by structurally
      // verifying that the fixed parts survive.
      for (const kind of ['epochCold', 'modelSideCold', 'context-overflow', 'compaction', 'prefix-reset']) {
        const problems = validateBoundary({ ...at('epoch-switch'), kind });
        assertEq(problems.length, 1, `'${kind}' must be rejected`);
        assertTrue(problems[0].includes('unknown cold boundary kind'), problems[0]);
      }
    },
  },

  {
    name: 'VERIFY-003 a boundary must name a turn and a step',
    fn: () => {
      assertTrue(validateBoundary({ kind: 'epoch-switch', step: 0 }).some((p) => p.includes('name the turn')));
      assertTrue(validateBoundary({ kind: 'epoch-switch', turn: '', step: 0 }).some((p) => p.includes('name the turn')));
      assertTrue(
        validateBoundary({ kind: 'epoch-switch', turn: 'x', step: -1 }).some((p) => p.includes('non-negative')),
      );
    },
  },
];
