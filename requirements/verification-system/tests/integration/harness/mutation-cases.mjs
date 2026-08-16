/**
 * gate-mutation-cases.mjs — K11, the forest's reverse self-check.
 *
 * `design-script-forest.md:630` states the reason this file exists:
 *
 *   一个能对错误实现给出绿灯的验证装置，比没有验证装置更危险。
 *
 * K10 proves the forest is structurally sound. Structural soundness does not imply that a
 * WRONG implementation would be refused — the deleted `epochCold` exemption was perfectly
 * well-formed, and its defect was that it let through the very thing it existed to catch.
 * So each case here mutates something and asserts a refusal, and each names the historical
 * false-green (`design-script-forest.md:624-627`) it prevents from returning.
 *
 * ── mutated INPUT, not mutated module ───────────────────────────────────────
 *
 * `gate-forest-lib.mjs` measured that an ES module namespace cannot be patched: the
 * descriptor reports `writable: true` while `[[Set]]` always fails, and under sloppy mode
 * the assignment silently no-ops. A K11 built on module patching could therefore have been
 * vacuously green in exactly the way this package exists to prevent.
 *
 * Three of the four classes are expressed instead as a mutated INPUT — a scenario source or
 * a request that an incorrect implementation would have accepted — driven through the
 * UNMODIFIED forest. That is strictly better than patching: a mutated input cannot fail to
 * apply, and the assertion is about the shipped code rather than about a stand-in.
 *
 * The fourth class is a source-level assertion, because what it must prove is the ABSENCE
 * of a code path (see `requestRoleOf` below) and an absence has no input that exhibits it.
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { assertEq, assertTrue } from './lib.mjs';
import { contextOf, rejectsSelect } from './forest-lib.mjs';
import { compileScenario } from '../../e2e/support/scenario-schema.js';
import { RETIRED_FIELDS, retiredFieldProblems } from '../../e2e/support/legacy-fields.js';
import { ScenarioRuntime } from '../../e2e/support/scenario-runtime.js';

const repoFile = (path) => readFileSync(fileURLToPath(new URL(`../../../${path}`, import.meta.url)), 'utf8');

const SESSION = 'ses_mutation';
const SYSTEM = { role: 'system', content: 'You are a coder.' };

const user = (text) => ({ role: 'user', content: text });
const assistant = (text) => ({ role: 'assistant', content: text });

/** One request with an explicit session id for its test context. */
const request = (messages, { tools = ['write'], sessionId = SESSION } = {}) => ({
  sessionID: sessionId,
  model: 'test-model',
  tools: tools.map((name) => ({ type: 'function', function: { name } })),
  messages,
});

/** Compile a source that must load, or fail naming why. */
const compiled = (source) => {
  const result = compileScenario(source, { name: 'mutation.toml' });
  assertTrue(result.ok === true, `fixture must compile: ${result.ok ? '' : result.problems.join(' | ')}`);
  return result.scenario;
};

/** A runtime over a source that must load, with `lane` bound. */
const runtimeOf = (source, lane = 'fast-coder') => {
  const runtime = new ScenarioRuntime(compiled(source));
  runtime['bind'](lane, SESSION);
  return runtime;
};

/**
 * Assert a source is REFUSED at load time, with the reason named.
 *
 * Local rather than in `gate-lib.mjs`: that file deliberately has no negative helper, and
 * the house pattern is one per cases file (`gate-schema-cases.mjs:18`,
 * `gate-source-cases.mjs:41`). `rejectsSelect` is shared only because K10 and K11 both
 * needed the runtime form.
 */
const rejectsSource = (source, fragment) => {
  const result = compileScenario(source, { name: 'mutation.toml' });
  assertTrue(result.ok !== true, 'the mutated source must not compile');
  assertTrue(
    result.problems.some((problem) => problem.includes(fragment)),
    `expected a problem mentioning '${fragment}', got: ${result.problems.join(' | ')}`,
  );
  return result.problems;
};

const TWO_STEPS = `scenario = "mutation"
prompt = { text = "Ship the parser fix." }

[[turn]]
id = "mgr"
lane = "fast-coder"
user = "Ship the parser fix."

  [[turn.step]]
  respond = { type = "text", text = "step zero" }

  [[turn.step]]
  respond = { type = "text", text = "step one" }
`;

export const mutationCases = [
  // ── class 1: epochCold ────────────────────────────────────────────────────

  {
    name: 'VERIFY-003 a prefix rewrite is refused even when tools and the system message are unchanged',
    fn: () => {
      // 「epochCold  放过不该发生的 epoch 切换（tools+system 未变即通过）」
      //
      // The deleted exemption's predicate was exactly "tools and the leading system message
      // are unchanged", and on that basis it admitted ANY rewrite of the conversation — which
      // is most of what a wrong prefix replacement looks like. It passed the mutations it
      // existed to catch.
      //
      // So this mutation reproduces the predicate precisely: identical `tools`, byte-identical
      // system message, an EARLIER user message injected. The last user message is left alone
      // on purpose — K9 measured that changing it makes the request come back `unmatched`, so
      // the seal would never be consulted and the case would prove nothing about ARCH-004.
      //
      // `gate-scenario-runtime-cases.mjs` already covers the generic undeclared rewrite. This
      // is the narrower claim that generic case cannot make: tools and system are not an
      // escape hatch.
      const runtime = runtimeOf(TWO_STEPS);

      const first = request([SYSTEM, user('Ship the parser fix.')]);
      const context = contextOf(first.sessionID);
      const selection = runtime.select(first, context);
      assertEq(selection.entry.id, 'mgr.0', 'the baseline delivery must land before a seal can break');
      runtime.consume(first, selection, context);

      // Same tools, same system, same last user message. Only history changed.
      const rewritten = request([
        SYSTEM,
        user('An injected head the first request never carried.'),
        user('Ship the parser fix.'),
        assistant('step zero'),
      ]);

      const refusal = rejectsSelect(runtime, rewritten, 'sealBroken');
      assertEq(
        refusal.sealBroken.reason,
        'undeclared',
        'ARCH-004 admits a break only where the scenario declares one',
      );
    },
  },

  {
    name: 'VERIFY-003 a declared epoch boundary admits the rewrite, and only at the step it names',
    fn: () => {
      // The other half, without which the case above could be satisfied by refusing
      // everything. A gate that never admits a legitimate rebase is as wrong as one that
      // admits every illegitimate one, and it would be discovered later and more expensively.
      const runtime = runtimeOf(`${TWO_STEPS}
[[epoch]]
turn = "mgr"
step = 1
reason = "epoch-switch"
`);

      const first = request([SYSTEM, user('Ship the parser fix.')]);
      const firstContext = contextOf(first.sessionID);
      const firstSelection = runtime.select(first, firstContext);
      runtime.consume(first, firstSelection, firstContext);

      const rebasedBody = request([
        SYSTEM,
        user('Condensed companion context.'),
        user('Ship the parser fix.'),
        assistant('step zero'),
      ]);
      const rebased = runtime.select(rebasedBody, contextOf(rebasedBody.sessionID));

      assertEq(rebased.resealed, 'epoch-switch');
      assertEq(rebased.entry.id, 'mgr.1');
    },
  },

  // ── class 2: specificity ──────────────────────────────────────────────────

  {
    name: 'VERIFY-003 two declarations for one point are refused at load, never scored',
    fn: () => {
      // 「specificity  两条边同时命中时静默选一条」
      //
      // The retired scorer summed substring lengths and added magic numbers
      // (`afterToolResult === true` → +50) to choose between edges that both matched. The
      // mutation here is the input scoring existed to survive: two declarations for the same
      // (lane, turn, step). It must be a load-time refusal, because at load the whole scenario
      // is available and the author can be told; at runtime the only options are to guess or
      // to fail late.
      const problems = rejectsSource(
        `scenario = "mutation"
prompt = { text = "Ship the parser fix." }

[[turn]]
id = "left"
lane = "fast-coder"
user = "Ship the parser fix."

  [[turn.step]]
  respond = { type = "text", text = "left" }

[[turn]]
id = "right"
lane = "fast-coder"
user = "Ship the parser fix."

  [[turn.step]]
  respond = { type = "text", text = "right" }
`,
        'the scenario does not say what the model does next',
      );

      // The message must name BOTH declarations. A refusal naming one would leave the author
      // guessing which other declaration it collided with — and guessing is this class.
      assertTrue(
        problems.some((problem) => problem.includes('left.0') && problem.includes('right.0')),
        `the refusal must name both declarations: ${problems.join(' | ')}`,
      );
    },
  },

  {
    name: 'VERIFY-003 two equal-weight prefixes are reported as ambiguous, not resolved by score',
    fn: () => {
      // The runtime half, and a distinction the charter's one-line summary elides: two
      // declarations with IDENTICAL text are a load-time refusal (the case above), while two
      // DIFFERENT declarations of equal weight that both match one request can only be
      // detected when that request arrives. Both must refuse; neither may score.
      //
      // Stated here rather than left to `gate-runtime-key-cases.mjs` because that file asserts
      // the property of `resolveEntry` in isolation, while this asserts the composed runtime
      // still refuses — the layer a scenario author actually meets.
      // Two prompts, one per declaration. The dead-edge check reaches a fragment declaration
      // through its LAST fragment (the earlier ones are production's wrapper, which no
      // scenario text carries), so a single prompt would leave one turn unreachable and the
      // fixture would be refused for that instead — hiding the tie this case is about.
      const runtime = runtimeOf(`scenario = "mutation"
prompt = { text = "xy" }

flow = [ { prompt = { text = "yz" } } ]

[[turn]]
id = "left"
lane = "fast-coder"
user = ["HEAD", "xy"]

  [[turn.step]]
  respond = { type = "text", text = "left" }

[[turn]]
id = "right"
lane = "fast-coder"
user = ["HEAD", "yz"]

  [[turn.step]]
  respond = { type = "text", text = "right" }
`);

      const refusal = rejectsSelect(runtime, request([SYSTEM, user('HEAD xy yz')]), 'ambiguous');
      assertEq(
        refusal.ambiguous.entries.map((entry) => entry.id).sort().join(','),
        'left.0,right.0',
        'both tied declarations must be reported, so the author can see the tie',
      );
    },
  },

  // ── class 3: requestRoleOf ────────────────────────────────────────────────

  {
    name: 'PROMPT-008 a scenario cannot declare a role, and the refusal names the real source',
    fn: () => {
      // 「requestRoleOf  与生产 role 推导不一致时按自己那套判」
      //
      // PROMPT-008 makes `AttemptExecutionProfile` the only source of a role. The old mock
      // inferred one from the wire and then judged by its own answer, so a disagreement with
      // production was invisible — the mock was self-consistent.
      //
      // Two halves, and both are needed: a scenario must not be able to DECLARE a role, and
      // the selection path must not INFER one (next case). Either alone leaves the class open —
      // a rejected field with an inferring matcher just moves the inference out of sight.
      for (const field of ['role', 'requestRoleOf']) {
        assertTrue(field in RETIRED_FIELDS, `${field} must stay retired, with its replacement named`);
        assertTrue(
          RETIRED_FIELDS[field].includes('PROMPT-008'),
          `${field}'s rejection must cite the clause that owns the answer: ${RETIRED_FIELDS[field]}`,
        );
      }

      const problems = retiredFieldProblems({ turn: [{ id: 'a', role: 'manager', user: 'go' }] });
      assertTrue(
        problems.some((problem) => problem.includes('AttemptExecutionProfile')),
        `a declared role must be refused, naming the real source: ${problems.join(' | ')}`,
      );
    },
  },

  {
    name: 'PROMPT-008 the ScenarioRuntime selection path contains no role inference',
    fn: () => {
      // The absence half, asserted at the source because an absence has no input that
      // exhibits it. This is the one case in this file that is not a mutated input.
      //
      // It was not hypothetical: before K9 landed, `requestRoleOf` was retired as a
      // scenario FIELD while its function body stayed alive in `strict-mock-matches.js`,
      // called by `strict-mock-forest.js` to infer a role from the wire on the old
      // `selectExpectation` path. K9 has since deleted both the body and the caller;
      // this case keeps the NEW path from growing a replacement.
      //
      // The assertion is scoped to the selection path and says plainly what it covers.
      const selectionPath = [
        'tests/e2e/support/scenario-runtime.js',
        'tests/e2e/support/runtime-key.js',
        'tests/e2e/support/scenario-schema.js',
        'tests/e2e/support/cold-boundary.js',
        'tests/e2e/support/delivery-plan.js',
      ];

      const executable = (line) => {
        const text = line.trimStart();
        return !text.startsWith('*') && !text.startsWith('//') && !text.startsWith('/*');
      };

      const offenders = selectionPath.filter((path) =>
        repoFile(path)
          .split('\n')
          .some((line) => executable(line) && line.includes('requestRoleOf')),
      );

      assertEq(offenders.length, 0, `role inference must not exist on the selection path: ${offenders.join(', ')}`);
    },
  },

  // ── class 4: loadScripts, re-anchored ─────────────────────────────────────

  {
    name: 'VERIFY-003 a declared edge still resolves after a restart clears the seals',
    fn: () => {
      // 「loadScripts  重启后匹配空间被换掉，原本该暴露的错命中消失」
      //
      // `loadScripts` was deleted in K8c, so there is no dynamic loading left to mutate. What
      // it destroyed is still a property worth pinning: a static scenario's declared edge must
      // survive a restart. Re-anchored to the mechanism a restart actually uses, `clearSeals` —
      // the new process rebuilds its request view from the journal, so the next request is a
      // fresh baseline rather than a continuation.
      //
      // The mutation is the restart itself. Under `loadScripts` the matching space was swapped
      // at exactly this moment, so an edge that should have been hit silently was not, and a
      // wrong production reply had nothing left to contradict it.
      const runtime = runtimeOf(TWO_STEPS);

      const first = request([SYSTEM, user('Ship the parser fix.')]);
      const context = contextOf(first.sessionID);
      const before = runtime.select(first, context);
      assertEq(before.entry.id, 'mgr.0');
      runtime.consume(first, before, context);

      runtime.clearSeals();

      // The SAME request, after the restart. It must resolve to the same declaration — that is
      // what "the scenario is static" means — and it must not be refused as a seal break, since
      // a fresh baseline is not a broken prefix.
      const after = runtime.select(first, contextOf(first.sessionID));
      assertTrue(after.sealBroken === undefined, 'a post-restart baseline is not an ARCH-004 break');
      assertEq(after.entry.id, 'mgr.0', 'a static scenario answers the same request the same way after a restart');
      assertEq(after.attempt, 2, 'deliveries still accumulate across the restart; only the seal resets');
    },
  },

  {
    name: 'VERIFY-003 a restart does not resurrect an edge the scenario never declared',
    fn: () => {
      // The complement, and why the case above is not enough alone: "still resolves" could be
      // satisfied by a matcher that became MORE permissive after a restart, which is the
      // direction `loadScripts` actually failed in — it replaced the matching space, so what
      // matched afterwards was no longer what the file said.
      const runtime = runtimeOf(TWO_STEPS);

      const first = request([SYSTEM, user('Ship the parser fix.')]);
      const context = contextOf(first.sessionID);
      const selection = runtime.select(first, context);
      runtime.consume(first, selection, context);
      runtime.clearSeals();

      const unknown = request([SYSTEM, user('Something this scenario never declared.')]);
      rejectsSelect(runtime, unknown, 'unmatched');
    },
  },
];
