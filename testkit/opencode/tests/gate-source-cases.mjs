/**
 * gate-source-cases.mjs — scenario SOURCE hygiene: retired vocabulary, and one layout.
 *
 * Two checks that only make sense together, because they answer the same question from
 * opposite ends: can a reader trust what a scenario file says?
 *
 *   K7  a field the compiler would ignore must not load at all
 *   K6  indentation is meaningless to TOML, so nothing keeps it consistent but a tool
 *
 * The first matters more than it looks. A scenario carrying `reusable = true` that the
 * loader silently drops is a scenario whose author believes an edge is reusable and
 * gets a one-shot — the same failure shape as a dangling fault, in the vocabulary
 * layer.
 */

import { assertEq, assertTrue } from './gate-lib.mjs';
import { RETIRED_FIELDS, retiredFieldProblems } from '../legacy-fields.js';
import { compileScenario } from '../scenario-schema.js';
import { formatToml } from '../../../scripts/toml-format.mjs';

const compile = (source) => compileScenario(source, { name: 'p.toml' });

/** One turn, one step, reachable — a scenario that loads, plus whatever is under test. */
const withField = (field) => `scenario = "p"
flow = [ { prompt = { text = "go" } } ]

[[turn]]
id = "a"
user = "go"
${field}

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`;

const rejectsField = (field, fragment) => {
  const result = compile(withField(field));
  assertTrue(!result.ok, `'${field}' must not load`);
  assertTrue(
    result.problems.some((problem) => problem.includes(fragment)),
    `expected a problem mentioning '${fragment}', got: ${result.problems.join(' | ')}`,
  );
};

export const sourceCases = [
  // ── K7: retired vocabulary ────────────────────────────────────────────────

  {
    name: 'VERIFY-003 predicate-bag matching is retired',
    fn: () => {
      // §4: a conjunction of predicates can match several edges, which is why
      // `specificity` had to exist. The turn prefix replaced the whole bag.
      rejectsField('match = { user = "go" }', 'keyed by (lane, turn, step)');
      rejectsField('userRegex = "Fix.*"', 'cannot be prefix-ordered');
      rejectsField('containsText = ["go"]', 'as a prefix');
      rejectsField('messageCount = 3', 'use step');
      rejectsField('afterToolResult = true', 'already in the semantic prefix');
    },
  },

  {
    name: 'VERIFY-003 scoring is retired because a longest prefix is unique',
    fn: () => {
      rejectsField('specificity = 9', 'nothing left to score');
    },
  },

  {
    name: 'PROMPT-008 the mock may not carry a role',
    fn: () => {
      // §5: the mock re-deriving a domain concept. The role comes from
      // `AttemptExecutionProfile`, and a scenario naming its own would let the two
      // disagree — with the mock's version winning inside the mock.
      rejectsField('role = "manager"', 'AttemptExecutionProfile');
      rejectsField('requestRoleOf = "manager"', 'only source');
    },
  },

  {
    name: 'VERIFY-003 harness bookkeeping may not participate in matching',
    fn: () => {
      // §6: out-of-band identity leaking into content. A scenario matches what the
      // provider received, and the provider never sees a testkit header.
      rejectsField('__testkitHeaders = { "x-session-id" = "s" }', 'one-way');
    },
  },

  {
    name: 'VERIFY-003 the four matching flags are retired',
    fn: () => {
      // §7's flag explosion. Each one existed to exempt an edge from a mechanism that
      // no longer exists: there is no cursor, no claim count, and content is pure.
      rejectsField('reusable = true', 'inherently reusable');
      rejectsField('pathless = true', 'no cursor to be exempt from');
      rejectsField('neverEnd = true', 'declare those steps');
      rejectsField('blocking = false', 'assert arrival with must');
      rejectsField('claimCount = 2', 'independently waitable');
      rejectsField('aliases = ["second-perfect"]', 'two steps (REVIEW-003)');
    },
  },

  {
    name: 'VERIFY-003 tool-set predicates are retired but declared tools are not',
    fn: () => {
      // The distinction worth pinning: a tool set is part of the request identity
      // (ARCH-004 counts it in the seal), so `tools` on a turn is legitimate. What is
      // retired is using it as a FILTER over otherwise-matching edges.
      rejectsField('requiredTools = ["fork"]', 'not a filter');
      rejectsField('forbiddenTools = ["executor"]', 'a different turn');

      assertTrue(compile(withField('tools = ["fork", "join"]')).ok, 'declared tools must still load');
    },
  },

  {
    name: 'VERIFY-003 dynamic loading is retired at any nesting depth',
    fn: () => {
      // §8. `loadScripts` lived inside `flow`, not on an edge, so a location-aware
      // check would need the very map of legacy shapes this replaces — hence the
      // whole-tree walk.
      const result = compile(`scenario = "p"
flow = [ { loadScripts = "after.toml" } ]

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text", text = "ok" }
`);

      assertTrue(!result.ok);
      assertTrue(result.problems[0].includes('one static file'), result.problems[0]);
      assertTrue(result.problems[0].includes('flow[0].loadScripts'), 'the path locates it');
    },
  },

  {
    name: 'ARCH-001 turn as an ordinal is retired, turn as text is required',
    fn: () => {
      // `lane.turn = 7` was an index into a hand-maintained sequence — the program
      // counter ARCH-001 forbids, one layer down. Text makes the key a function of the
      // request; a number makes it a function of the author's bookkeeping.
      const result = compile(`scenario = "p"
flow = []

[[fault]]
turn = 7
step = 0
attempts = [1]
delivery = "provider-error"
`);

      assertTrue(!result.ok);
      assertTrue(result.problems[0].includes('not an ordinal'), result.problems[0]);
      assertTrue(result.problems[0].includes('step already carries position'), result.problems[0]);
    },
  },

  {
    name: 'VERIFY-003 every retired field names its replacement',
    fn: () => {
      // A refusal that does not say what to write instead gets worked around. The
      // argument lives in `design-script-forest.md` §3-§8, so the message carries the
      // conclusion rather than just the verdict.
      for (const [field, reason] of Object.entries(RETIRED_FIELDS)) {
        assertTrue(typeof reason === 'string' && reason.length > 20, `${field} needs a real reason`);
      }
      assertEq(retiredFieldProblems({}).length, 0, 'a clean scenario reports nothing');
    },
  },

  {
    name: 'VERIFY-003 retired vocabulary is reported before structural problems',
    fn: () => {
      // A scenario written entirely in the old form has no `user` key either. Reporting
      // that first would tell the author to add a field, when the real answer is that
      // the whole edge shape changed.
      const result = compile(`scenario = "p"
flow = []

[[turn]]
id = "a"
match = { user = "go" }
reusable = true
`);

      assertTrue(!result.ok);
      assertTrue(
        result.problems.every((problem) => problem.includes('is retired')),
        `expected only retirement problems, got: ${result.problems.join(' | ')}`,
      );
    },
  },

  // ── K6: one layout, preserved comments ───────────────────────────────────

  {
    name: 'VERIFY-003 the formatter is idempotent',
    fn: () => {
      // The property that makes it usable in a pre-commit hook: formatting a formatted
      // file changes nothing, so the check and the fix cannot disagree.
      const messy = `scenario = "p"



[[turn]]
     id = "a"
user = "go"
[[turn.step]]
respond = { type = "text" }
      [[turn.step]]
  respond = { type = "text", text = "2" }
[[fault]]
   turn = "a"
`;

      const once = formatToml(messy);
      assertEq(formatToml(once), once, 'format(format(x)) must equal format(x)');
    },
  },

  {
    name: 'VERIFY-003 nesting depth becomes indentation',
    fn: () => {
      const formatted = formatToml(`scenario = "p"
[[turn]]
id = "a"
[[turn.step]]
respond = { type = "text" }
`);

      const lines = formatted.split('\n');
      assertTrue(lines.includes('[[turn]]'), 'a top-level header is flush left');
      assertTrue(lines.includes('  [[turn.step]]'), 'a nested header is indented one level');
      assertTrue(lines.includes('  respond = { type = "text" }'), 'its keys follow it');
    },
  },

  {
    name: 'VERIFY-003 comments survive and attach to what they explain',
    fn: () => {
      // §10's main argument for TOML over JSON: `# REVIEW-003: …` sits next to the two
      // steps it constrains. A parse-and-restringify formatter would emit canonical
      // TOML and delete every comment, removing the format's whole advantage.
      const formatted = formatToml(`scenario = "p"
[[turn]]
id = "a"
# REVIEW-003: two PERFECT verdicts are two steps
[[turn.step]]
respond = { type = "text" }
`);

      assertTrue(formatted.includes('# REVIEW-003'), 'the comment must survive');
      assertTrue(
        formatted.includes('  # REVIEW-003: two PERFECT verdicts are two steps'),
        'and take the indent of the step it introduces',
      );
    },
  },

  {
    name: 'VERIFY-003 a multi-line string is copied byte for byte',
    fn: () => {
      // Measured on §10's own example: trimming interior lines turned
      // `"  Read AGENTS.md.\n    Then fix…"` into `"Read AGENTS.md.\nThen fix…"`. A
      // formatter that rewrites the prompt a scenario declares makes it stop matching
      // what production sends — silent, and in the one place that must be exact.
      const source = `scenario = "p"

[[turn]]
id = "a"
user = """
  Read AGENTS.md.
    Then fix the failing test.
"""

  [[turn.step]]
  respond = { type = "text" }
`;

      const formatted = formatToml(source);
      assertTrue(formatted.includes('  Read AGENTS.md.'), 'leading spaces inside the string are kept');
      assertTrue(formatted.includes('    Then fix the failing test.'), 'so is deeper indentation');
      assertEq(formatToml(formatted), formatted, 'still idempotent with a multi-line string');
    },
  },

  {
    name: 'VERIFY-003 formatting never changes what the TOML means',
    fn: () => {
      // The formatter's one hard constraint. Asserted by compiling both forms and
      // comparing the compiled scenarios, not by comparing text.
      const source = `scenario = "p"
flow = [ { prompt = { text = "go" } } ]
[[turn]]
     id = "a"
user = "go"
[[turn.step]]
respond = { type = "text", text = "ok" }
`;

      const before = compile(source);
      const after = compile(formatToml(source));

      assertTrue(before.ok && after.ok, 'both forms must load');
      assertEq(JSON.stringify(after.scenario), JSON.stringify(before.scenario));
    },
  },

  {
    name: 'VERIFY-003 blank runs collapse and turns read as blocks',
    fn: () => {
      const formatted = formatToml(`scenario = "p"



[[turn]]
id = "a"
[[turn.step]]
respond = { type = "text" }
[[turn]]
id = "b"
`);

      assertTrue(!formatted.includes('\n\n\n'), 'no run of blank lines survives');
      assertTrue(formatted.includes('respond = { type = "text" }\n\n[[turn]]'), 'a blank line precedes each turn');
      assertTrue(formatted.startsWith('scenario = "p"'), 'no leading blank line');
      assertTrue(formatted.endsWith('id = "b"\n'), 'exactly one trailing newline');
    },
  },
];
