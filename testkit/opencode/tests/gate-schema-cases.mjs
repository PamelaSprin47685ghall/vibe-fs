/**
 * gate-schema-cases.mjs — a scenario that half-loads is worse than one that fails.
 *
 * VERIFY-003. Every check here runs at load time with no Host and no build artifacts,
 * which is what puts it in VERIFY-001 layer 0 alongside `ssot-lint` and `shock-audit`.
 *
 * The dead-edge check is the one that requires the static whole: a step no flow can
 * reach is a step the author believes is covered. It was unavailable while scenarios
 * could be swapped mid-run (`loadScripts`), which is one of the reasons §8 of
 * `design-script-forest.md` rejects dynamic loading.
 */

import { assertEq, assertTrue } from './gate-lib.mjs';
import { compileScenario, rootKeyOrderProblems } from '../scenario-schema.js';

const compile = (source) => compileScenario(source, { name: 'p.toml' });

const rejects = (source, fragment) => {
  const result = compile(source);
  assertTrue(!result.ok, 'expected rejection');
  assertTrue(
    result.problems.some((problem) => problem.includes(fragment)),
    `expected a problem mentioning '${fragment}', got: ${result.problems.join(' | ')}`,
  );
};

const accepts = (source) => {
  const result = compile(source);
  assertTrue(result.ok, `expected acceptance, got: ${result.ok ? '' : result.problems.join(' | ')}`);
  return result.scenario;
};

const HEALTHY = `scenario = "probe"
description = "ORCH-005 short CAS publish"
must = ["mgr.0"]

flow = [
  { prompt = { agent = "fast-manager", text = "Ship the parser fix." } },
  { wait = "mgr.0" },
]

[[turn]]
id = "mgr"
lane = "fast-manager"
user = "Ship the parser fix."
tools = ["fork", "join"]

  [[turn.step]]
  respond = { type = "tool-call", tool = "fork", args = { agent = "fast-coder", prompt = "Fix it." } }

  [[turn.step]]
  respond = { type = "text", text = "Done." }

[[turn]]
id = "coder"
lane = "coder"
user = "Fix it."

  [[turn.step]]
  respond = { type = "text", text = "Fixed." }

[[fault]]
turn = "mgr"
step = 0
attempts = [1]
delivery = "provider-error"
status = 500

[[epoch]]
turn = "mgr"
step = 1
reason = "epoch-switch"
`;

/** One turn, one step, reachable — the smallest scenario that loads. */
const minimal = (extra = '') =>
  `scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
${extra}`;

export const schemaCases = [
  // ── compilation: source is a conversation, output is a lookup table ────────

  {
    name: 'VERIFY-003 a step position compiles to the runtime step integer',
    fn: () => {
      // §9's two layers. The author never writes a step number; the position within
      // the turn IS the number, which is what K2's `stepOf` counts off the request.
      const scenario = accepts(HEALTHY);

      assertEq(
        scenario.entries.map((entry) => `${entry.id}@${entry.step}`).join(' '),
        'mgr.0@0 mgr.1@1 coder.0@0',
      );
    },
  },

  {
    name: 'VERIFY-003 a fault names a step and compiles to the integer key',
    fn: () => {
      // The deviation from §10 worth pinning: the document writes `step = "fork-agent"`
      // (a name), K2 made the runtime step an integer. The compiler resolves one into
      // the other, and that resolution is what makes dangling references detectable —
      // a name can be checked against the declared set, an integer cannot.
      const scenario = accepts(HEALTHY);

      assertEq(scenario.faults.length, 1);
      assertEq(scenario.faults[0].step, 0);
      assertEq(scenario.faults[0].turn, 'Ship the parser fix.');
      assertEq(scenario.faults[0].kind, 'provider-error');
      assertEq(scenario.faults[0].lane, 'fast-manager');
    },
  },

  {
    name: 'VERIFY-003 a cold boundary compiles the same way',
    fn: () => {
      const scenario = accepts(HEALTHY);

      assertEq(scenario.boundaries.length, 1);
      assertEq(scenario.boundaries[0].step, 1);
      assertEq(scenario.boundaries[0].kind, 'epoch-switch');
    },
  },

  {
    name: 'VERIFY-003 a child turn is reached through a tool call argument',
    fn: () => {
      // `fork(agent, prompt)` is how the session that receives a child turn comes to
      // exist, so the prompt argument is a real edge in the reachability graph. Without
      // this, every child turn in every scenario would read as a dead edge.
      const scenario = accepts(HEALTHY);
      assertTrue(
        scenario.entries.some((entry) => entry.turnId === 'coder'),
        'the coder turn must survive the dead-edge check',
      );
    },
  },

  // ── the TOML root-key trap ───────────────────────────────────────────────

  {
    name: 'VERIFY-003 a root key after a table header is rejected',
    fn: () => {
      // Measured in §10: `flow = [...]` after `[[epoch]]` parses as `epoch[0].flow`
      // with no error. The parsed object cannot reveal this — it is indistinguishable
      // from a `flow` key the author meant to nest — so the check reads source text.
      rejects(
        `scenario = "p"

[[turn]]
id = "a"
user = "go"
flow = []

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`,
        "root key 'flow' appears after",
      );
    },
  },

  {
    name: 'VERIFY-003 every root key is guarded, and the line is reported',
    fn: () => {
      for (const key of ['scenario', 'description', 'must', 'flow']) {
        const problems = rootKeyOrderProblems(`[[turn]]\nid = "a"\n${key} = "x"\n`);
        assertEq(problems.length, 1, `${key} must be guarded`);
        assertTrue(problems[0].startsWith('line 3:'), problems[0]);
      }
    },
  },

  {
    name: 'VERIFY-003 a key inside a table is not mistaken for a root key',
    fn: () => {
      // `id`, `user`, `tools`, `respond` legitimately live under a header. Flagging
      // them would make the check unusable, so only the four root keys are guarded.
      assertEq(rootKeyOrderProblems(HEALTHY).length, 0);
    },
  },

  {
    name: 'VERIFY-003 commented-out root keys are ignored',
    fn: () => {
      // §10 makes comments load-bearing for clause references, so a commented example
      // must not fail the file.
      assertEq(rootKeyOrderProblems('[[turn]]\nid = "a"\n# flow = []\n').length, 0);
    },
  },

  // ── one point, one declaration ───────────────────────────────────────────

  {
    name: 'VERIFY-003 two declarations for one key with different responses are rejected',
    fn: () => {
      rejects(
        `scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "A" }

[[turn]]
id = "b"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "B" }
`,
        'does not say what the model does next',
      );
    },
  },

  {
    name: 'VERIFY-003 two declarations for one key with the SAME response are also rejected',
    fn: () => {
      // The documented deviation. §10 collapses identical templates, which was a
      // mitigation for predicate-conjunction matching where template reuse produced
      // duplicates naturally. Under (lane, turn, step) keying a recurring nudge has
      // several steps, so a true duplicate is debris the author should delete.
      rejects(
        `scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "same" }

[[turn]]
id = "b"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "same" }
`,
        'delete one',
      );
    },
  },

  {
    name: 'VERIFY-003 the same turn text at different steps is legitimate',
    fn: () => {
      // A multi-step turn is the normal shape. If this were an ambiguity the old
      // matcher's `messageCount` predicate would still be needed.
      const scenario = accepts(minimal());
      assertEq(scenario.entries.length, 1);

      accepts(`scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "first" }

  [[turn.step]]
  respond = { type = "text", text = "second" }
`);
    },
  },

  {
    name: 'VERIFY-003 the same turn text in different lanes is legitimate',
    fn: () => {
      accepts(`scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
lane = "one"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "A" }

[[turn]]
id = "b"
lane = "two"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "B" }
`);
    },
  },

  // ── dangling references ──────────────────────────────────────────────────

  {
    name: 'VERIFY-003 a fault referencing an undeclared turn is rejected',
    fn: () => {
      rejects(
        minimal('\n[[fault]]\nturn = "nope"\nstep = 0\nattempts = [1]\ndelivery = "provider-error"\n'),
        "references turn 'nope'",
      );
    },
  },

  {
    name: 'VERIFY-003 a fault referencing an undeclared step is rejected',
    fn: () => {
      // The narrower dangling case: the turn exists, the step does not. Silently
      // accepting it would make the fault inert — a step the author believes is
      // covered and is not, which is the same damage as an empty `attempts` list.
      rejects(
        minimal('\n[[fault]]\nturn = "a"\nstep = 7\nattempts = [1]\ndelivery = "provider-error"\n'),
        "references step '7'",
      );
    },
  },

  {
    name: 'VERIFY-003 a cold boundary referencing an undeclared step is rejected',
    fn: () => {
      rejects(minimal('\n[[epoch]]\nturn = "nope"\nstep = 0\nreason = "epoch-switch"\n'), "references turn 'nope'");
      rejects(minimal('\n[[epoch]]\nturn = "a"\nstep = 3\nreason = "epoch-switch"\n'), "references step '3'");
    },
  },

  {
    name: 'VERIFY-003 must referencing an undeclared step is rejected',
    fn: () => {
      rejects(
        `scenario = "p"
must = ["ghost"]
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`,
        "must references 'ghost'",
      );
    },
  },

  {
    name: 'VERIFY-003 a flow wait referencing an undeclared step is rejected',
    fn: () => {
      // Not in §12's list, added here: a `wait` on a step that cannot arrive hangs the
      // scenario until the watchdog fires, and the diagnostic then points at a timeout
      // rather than at the typo.
      rejects(
        `scenario = "p"
flow = [ { prompt = { text = "go" } }, { wait = "ghost" } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`,
        "flow wait references 'ghost'",
      );
    },
  },

  {
    name: 'VERIFY-003 must and wait may name a turn or a step',
    fn: () => {
      // Both granularities are useful: a turn id for "this exchange happened", a step
      // id for "this particular provider step happened".
      accepts(HEALTHY);
      accepts(`scenario = "p"
must = ["a"]
flow = [ { prompt = { text = "go" } }, { wait = "a" } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`);
    },
  },

  // ── dead edges: the check only a static whole allows ─────────────────────

  {
    name: 'VERIFY-003 a turn no flow can reach is rejected',
    fn: () => {
      rejects(
        `scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }

[[turn]]
id = "orphan"
user = "nobody ever sends this"

  [[turn.step]]
  respond = { type = "text", text = "x" }
`,
        'dead edge',
      );
    },
  },

  {
    name: 'VERIFY-003 a production-composed lane opts out with internal',
    fn: () => {
      // The Blogger and the Executor map child cannot be reached by any scenario text:
      // production composes their prompts itself
      // (`../next/Session/CompanionHostBlogger.fs:72,77,118`,
      // `../next/OpenCode/ExecutorSummarize.fs:95`). Without an opt-out the dead-edge
      // check has no evidence either way and rejects a correct scenario.
      accepts(`scenario = "p"
prompt = { text = "go" }

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }

[[turn]]
id = "blogger"
lane = "coder-blogger"
internal = true
user = "You are the blogger of a coding agent session."

  [[turn.step]]
  respond = { type = "text", text = "blog" }
`);
    },
  },

  {
    name: 'VERIFY-003 internal must be true when present',
    fn: () => {
      // `internal = false` would read as "checked and reachable", which is the opposite
      // of what the field means. Omitting it is the way to say that.
      rejects(
        `scenario = "p"
prompt = { text = "go" }

[[turn]]
id = "a"
internal = false
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`,
        'internal must be true when present',
      );
    },
  },

  {
    name: 'VERIFY-003 parentSession is retired, not a reachability input',
    fn: () => {
      // Measured dead twice over. Its only source was
      // `__testkitHeaders['x-parent-session-id']` — harness bookkeeping the provider
      // never sees — and `matchesExpectation` resolved it through `sessionBindings`,
      // where all 16 scenarios declaring a parent had never bound it. So the comparison
      // short-circuited and never ran, in every scenario, for its whole life.
      rejects(
        `scenario = "p"
prompt = { text = "go" }

[[turn]]
id = "a"
parentSession = "manager"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`,
        'mark the lane internal = true instead',
      );
    },
  },

  {
    name: 'VERIFY-003 a title turn is reachable through the turn it titles',
    fn: () => {
      // No special case needed: a title request carries the conversation being titled,
      // so its declared text prefix-matches the real turn's. Special-casing `kind` here
      // would have exempted title turns from the dead-edge check for nothing.
      const scenario = accepts(`scenario = "p"
prompt = { text = "Ship the parser fix now." }

[[turn]]
id = "a"
user = "Ship the parser fix now."

  [[turn.step]]
  respond = { type = "text", text = "ok" }

[[turn]]
id = "title"
kind = "title"
user = "Ship the parser fix"

  [[turn.step]]
  respond = { type = "title", text = "Parser fix" }
`);

      assertEq(scenario.entries.length, 2);
    },
  },

  {
    name: 'VERIFY-003 reachability is prefix-based in both directions',
    fn: () => {
      // A flow prompt may be longer than the declared fragment (the scenario declares
      // a distinctive prefix) or shorter (the flow sends a short instruction and the
      // turn text quotes more of it). Requiring equality would force authors to repeat
      // long prompts verbatim in two places.
      accepts(`scenario = "p"
flow = [ { prompt = { text = "Ship the parser fix now, carefully." } } ]

[[turn]]
id = "a"
user = "Ship the parser fix"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`);
    },
  },

  // ── structural requirements ──────────────────────────────────────────────

  {
    name: 'VERIFY-003 a scenario needs a name, and a turn needs user text and a step',
    fn: () => {
      rejects('[[turn]]\nid = "a"\nuser = "go"\n\n  [[turn.step]]\n  respond = { type = "text" }\n', 'scenario name');
      rejects('scenario = "p"\nflow = []\n\n[[turn]]\nid = "a"\n\n  [[turn.step]]\n  respond = {}\n', 'needs user text');
      rejects('scenario = "p"\nflow = []\n\n[[turn]]\nid = "a"\nuser = "go"\n', 'at least one step');
      rejects('scenario = "p"\nflow = []\n\n[[turn]]\nuser = "go"\n\n  [[turn.step]]\n  respond = {}\n', 'needs an id');
    },
  },

  {
    name: 'VERIFY-003 malformed TOML is reported as a parse failure, not a schema problem',
    fn: () => {
      // The two are different author actions: fix the syntax, versus fix the meaning.
      rejects('scenario = "p"\n[[turn\nid = "a"\n', 'TOML parse failed');
    },
  },

  {
    name: 'VERIFY-003 a rejected scenario yields no partial result',
    fn: () => {
      // A scenario that half-loads is a scenario whose author believes something is
      // covered that is not — the same failure mode as a dangling fault, one level up.
      const result = compile(minimal('\n[[fault]]\nturn = "nope"\nstep = 0\nattempts = [1]\ndelivery = "provider-error"\n'));

      assertTrue(!result.ok);
      assertTrue(result.scenario === undefined, 'a rejection must not carry a usable scenario');
    },
  },

  {
    name: 'VERIFY-003 every problem is prefixed with the file it came from',
    fn: () => {
      // Nineteen scenarios load at once; a bare message would not say which file.
      const result = compile('scenario = "p"\nflow = []\n\n[[turn]]\nid = "a"\n\n  [[turn.step]]\n  respond = {}\n');

      assertTrue(!result.ok);
      assertTrue(
        result.problems.every((problem) => problem.startsWith('p.toml: ')),
        result.problems.join(' | '),
      );
    },
  },
];
