/**
 * gate-forest-lib-cases.mjs — the harness's own tests.
 *
 * `gate-forest-lib.mjs` is test infrastructure two later packages rest on, so it needs
 * evidence of its own or K10 and K11 are green about an unverified device.
 * `design-script-forest.md` §14:630 states the stake: a verification device that
 * green-lights a wrong implementation is worse than no device.
 *
 * Each case below targets one way the harness could be silently useless rather than
 * broken:
 *
 *   a patcher that restores correctly but never applied anything
 *   a negative helper that passes because `select` succeeded
 *   a serialiser that returns a constant, so two runs "agree"
 *   a loader that skips a scenario it could not compile
 */

import * as coldBoundary from '../../e2e/support/cold-boundary.js';

import { assertEq, assertTrue } from './lib.mjs';
import { compileScenario } from '../../e2e/support/scenario-schema.js';
import { ScenarioRuntime } from '../../e2e/support/scenario-runtime.js';
import {
  contextOf,
  deriveRequests,
  forestSources,
  loadForest,
  rejectsSelect,
  runForest,
  withPatched,
} from './forest-lib.mjs';

const SESSION = 'ses_forest_lib';

const TWO_STEPS = `scenario = "p"
prompt = { text = "Ship the parser fix." }

[[turn]]
id = "mgr"
lane = "fast-manager"
user = "Ship the parser fix."

  [[turn.step]]
  respond = { type = "text", text = "step zero" }

  [[turn.step]]
  respond = { type = "text", text = "step one" }
`;

const runtimeOf = (source) => {
  const result = compileScenario(source, { name: 'p.toml' });
  assertTrue(result.ok, `fixture must compile: ${result.ok ? '' : result.problems.join(' | ')}`);
  const runtime = new ScenarioRuntime(result.scenario);
  runtime.bind('fast-manager', SESSION);
  return runtime;
};

const request = (messages) => ({
  sessionID: SESSION,
  model: 'test-model',
  messages,
});

const user = (text) => ({ role: 'user', content: text });

/** The message of whatever `body` throws, or a sentinel saying it did not throw. */
const thrownMessage = (body) => {
  try {
    body();
  } catch (error) {
    return error.message;
  }
  return '<did not throw>';
};

const scenarioOf = (file) => {
  const forest = loadForest();
  const found = forest.find((entry) => entry.file.endsWith(file));
  assertTrue(found !== undefined, `${file} must be in the forest`);
  return found.scenario;
};

export const forestLibCases = [
  // ── withPatched: the mutation must actually be in force ────────────────────

  {
    name: 'VERIFY-004 withPatched applies the mutation and restores it on normal return',
    fn: () => {
      // The half that a restore-only test would miss. `select` is replaced on the PROTOTYPE
      // and then called through a real instance, so the assertion fails if the patch did not
      // reach the call site — which is what a module-namespace patch would have produced.
      const original = ScenarioRuntime.prototype.select;
      const runtime = runtimeOf(TWO_STEPS);
      const body = request([user('Ship the parser fix.')]);
      const context = contextOf(body.sessionID);

      assertEq(runtime.select(body, context).entry.id, 'mgr.0', 'unpatched selection first');

      const observed = withPatched(ScenarioRuntime.prototype, 'select', () => ({ unmatched: { mutated: true } }), () =>
        runtime.select(body, context),
      );

      assertEq(
        JSON.stringify(observed),
        JSON.stringify({ unmatched: { mutated: true } }),
        'the replacement must be what the call site reached',
      );
      assertTrue(ScenarioRuntime.prototype.select === original, 'the original binding is restored by identity');
      assertEq(runtime.select(body, context).entry.id, 'mgr.0', 'and the real implementation answers again');
    },
  },

  {
    name: 'VERIFY-004 withPatched restores when the body throws',
    fn: () => {
      // A thrown assertion is the NORMAL exit for a mutation case, so this is the path that
      // decides whether the cases after it run against the shipped code.
      const original = ScenarioRuntime.prototype.select;

      const message = thrownMessage(() =>
        withPatched(ScenarioRuntime.prototype, 'select', () => ({ unmatched: {} }), () => {
          throw new Error('the mutation case failed, as a mutation case does');
        }),
      );

      assertEq(message, 'the mutation case failed, as a mutation case does', 'the body error propagates unwrapped');
      assertTrue(ScenarioRuntime.prototype.select === original, 'the mutation may not leak into the next case');
      const body = request([user('Ship the parser fix.')]);
      assertEq(runtimeOf(TWO_STEPS).select(body, contextOf(body.sessionID)).entry.id, 'mgr.0');
    },
  },

  {
    name: 'VERIFY-004 an ES module export cannot be patched, and withPatched says so',
    fn: () => {
      // The measurement the module header records, kept executable because it is the reason
      // K11 mutates INPUTS rather than modules. The descriptor is the trap: it claims
      // writable, and a patcher that believed it would report success.
      const descriptor = Object.getOwnPropertyDescriptor(coldBoundary, 'sealDecision');
      assertEq(
        JSON.stringify({
          writable: descriptor.writable,
          configurable: descriptor.configurable,
          enumerable: descriptor.enumerable,
        }),
        JSON.stringify({ writable: true, configurable: false, enumerable: true }),
        'a module namespace member claims writable while its [[Set]] always fails',
      );

      const original = coldBoundary.sealDecision;
      const assignMessage = thrownMessage(() => {
        coldBoundary.sealDecision = () => ({ held: true });
      });
      assertTrue(assignMessage !== '<did not throw>', 'assignment to a module namespace must fail');
      assertTrue(coldBoundary.sealDecision === original, 'and the binding is unchanged either way');

      assertEq(
        thrownMessage(() => withPatched(coldBoundary, 'sealDecision', () => ({ held: true }), () => 'unreachable')),
        "withPatched cannot patch the ES module namespace member 'sealDecision': its [[Set]] always fails and " +
          'its descriptor claims writable: true. Patch a class prototype or an object, or mutate the INPUT the ' +
          'module is given',
        'the refusal names the member and the alternative',
      );
    },
  },

  // ── rejectsSelect: a refusal is a returned value, not a throw ─────────────

  {
    name: 'VERIFY-003 rejectsSelect fails when select succeeded',
    fn: () => {
      // Without this, every K11 case could pass by asserting a refusal against a request the
      // forest happily answers.
      const runtime = runtimeOf(TWO_STEPS);

      assertEq(
        thrownMessage(() => rejectsSelect(runtime, request([user('Ship the parser fix.')]), 'unmatched')),
        'select must return { unmatched }, got delivered mgr.0',
        'the message names both the expected refusal and what actually happened',
      );
    },
  },

  {
    name: 'VERIFY-003 rejectsSelect distinguishes the three refusals and returns the refusal',
    fn: () => {
      // The discriminant is required because the three refusals mean different things. A
      // helper that accepted "any refusal" would pass a mutation that turned a real ambiguity
      // into an unmatched request — K11 class 2's exact failure mode.
      const runtime = runtimeOf(TWO_STEPS);
      const undeclared = request([user('Nobody declared this.')]);

      const refusal = rejectsSelect(runtime, undeclared, 'unmatched');
      assertEq(refusal.unmatched.key.lane, 'fast-manager', 'the refusal itself is returned for further assertions');

      assertEq(
        thrownMessage(() => rejectsSelect(runtime, undeclared, 'ambiguous')),
        'select must return { ambiguous }, got unmatched',
        'the wrong refusal is still a failure',
      );

      assertEq(
        thrownMessage(() => rejectsSelect(runtime, undeclared, 'refused')),
        "rejectsSelect: 'refused' is not a refusal shape; select returns one of unmatched, ambiguous, sealBroken",
        'a misspelled discriminant fails loudly rather than never matching',
      );
    },
  },

  // ── the serialiser ────────────────────────────────────────────────────────

  {
    name: 'VERIFY-003 two runs of one derived sequence serialise to identical text',
    fn: () => {
      // The forest-wide property K10 rests on (`design-script-forest.md:581`), proven here on
      // the sole One World scenario — K10 owns the all-on-disk case.
      const scenario = scenarioOf('long-stroke.toml');
      const derived = deriveRequests(scenario);
      assertEq(derived.underivable, undefined, 'long-stroke must be derivable');

      const first = runForest(scenario, derived);
      const second = runForest(scenario, derived);

      assertEq(second.text, first.text, 'the same request sequence must produce the same content sequence');
      assertEq(first.mismatches.join(' | '), '', 'every request resolved to the entry it was derived for');

      // Non-vacuity, because a serialiser that returned a constant would satisfy the above.
      // A second scenario is INLINE (TWO_STEPS) — do not add another TOML on disk.
      const inline = compileScenario(TWO_STEPS, { name: 'inline-two-steps.toml' });
      assertTrue(inline.ok, `inline fixture must compile: ${inline.ok ? '' : inline.problems.join(' | ')}`);
      const otherDerived = deriveRequests(inline.scenario);
      assertEq(otherDerived.underivable, undefined, 'inline TWO_STEPS must be derivable');
      const other = runForest(inline.scenario, otherDerived);
      assertTrue(other.text !== first.text, 'two different scenarios may not serialise identically');
      assertEq(
        first.text.trimEnd().split('\n').length,
        derived.requests.length + 1,
        'one header line plus one line per request',
      );
      assertEq(
        first.text.split('\n')[0],
        `scenario long-stroke requests ${derived.requests.length}`,
        'the header names the scenario and the count, so two empty runs cannot agree',
      );
    },
  },

  // ── the loader ────────────────────────────────────────────────────────────

  {
    name: 'VERIFY-003 the forest loader fails closed and names the file that did not compile',
    fn: () => {
      // Built inline: writing a broken file into `tests/e2e/scripts/` would break
      // `gate:toml` and every other gate that reads the directory, so the fail-closed path has
      // to be reachable without one.
      assertEq(
        thrownMessage(() => loadForest({ sources: [{ file: 'synthetic-bad.toml', source: 'scenario = "bad"\n' }] })),
        'synthetic-bad.toml does not compile, so the forest is incomplete: synthetic-bad.toml: ' +
          'a scenario declares at least one turn; a file with none describes no provider behaviour',
        'the report names the file and the compiler problem verbatim',
      );

      // And the throw is conditional on the failure, not on being called.
      const loaded = loadForest({ sources: [{ file: 'synthetic-good.toml', source: TWO_STEPS }] });
      assertEq(loaded.length, 1);
      assertEq(loaded[0].name, 'p');
    },
  },

  {
    name: 'VERIFY-003 every scenario on disk is compiled, none skipped',
    fn: () => {
      // The count is compared against the directory rather than pinned to 15: a pinned number
      // and a walk-derived loader would drift, and the drift would show up as a scenario
      // quietly outside the forest-wide property. Same defect as `CANARY_COUNT = 17`.
      const sources = forestSources();
      const loaded = loadForest();

      assertEq(loaded.length, sources.length, 'one compiled scenario per file on disk');
      assertEq(
        loaded.map((entry) => entry.file).join('\n'),
        sources.map((entry) => entry.file).join('\n'),
        'in the same order, so a report line can be read against the directory',
      );
    },
  },
];
