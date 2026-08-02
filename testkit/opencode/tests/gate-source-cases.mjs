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
import { readFileSync } from 'node:fs';
import { formatToml } from '../../../scripts/toml-format.mjs';
import { walk } from '../../../scripts/repo-scan.mjs';

/** The forest itself, so a scenario added later is covered without being registered. */
const SCENARIO_ROOT = 'testkit/opencode/scripts';

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
    name: 'VERIFY-003 a multi-line array indents its continuation lines',
    fn: () => {
      // Found by the first real conversion: `flow = [` spans lines, and the formatter
      // flattened every step to column zero. Balance has to be COUNTED — a header line
      // `[[turn]]` is balanced, inline tables nest, and a bracket inside
      // `command = "sh -lc '[...]'"` must not count at all.
      const formatted = formatToml(`scenario = "p"
flow = [
{ wait = "a" },
{ awaitTerminal = true },
]
`);

      assertTrue(formatted.includes('  { wait = "a" },'), 'a flow step is indented one level');
      assertTrue(formatted.includes('\n]\n'), 'the closing bracket returns to the opener column');
      assertEq(formatToml(formatted), formatted, 'still idempotent');
    },
  },

  {
    name: 'VERIFY-003 a bracket inside a string does not shift indentation',
    fn: () => {
      // The executor command in `process-stress` contains `'trap "" TERM'` and shell
      // quoting; a pattern-matched bracket count would open a level and never close it,
      // pushing the rest of the file rightwards on every format.
      const formatted = formatToml(`scenario = "p"

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "tool-call", args = { command = "sh -lc '[ -f x ] && echo {y}'" } }
`);

      assertTrue(formatted.includes('  respond = { type = "tool-call"'), 'the step key keeps its indent');
      assertEq(formatToml(formatted), formatted);
    },
  },

  {
    name: 'VERIFY-003 a file-header comment stays at column zero',
    fn: () => {
      // A comment adopts the indent of the next CONTENT line. Falling back to the
      // running depth put a file-header comment at whatever depth the file ended on —
      // measured on the first converted scenario, whose header moved two spaces right.
      const formatted = formatToml(`# EXEC-011: the deadline is min(3 x estimate, ceiling)
#
scenario = "p"

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text" }
`);

      assertTrue(formatted.startsWith('# EXEC-011:'), 'a header comment is flush left');
      assertEq(formatToml(formatted), formatted);
    },
  },

  {
    name: 'VERIFY-003 a comment introducing a turn stays attached to it',
    fn: () => {
      // The blank line before a top-level header exists so turns read as blocks. But a
      // comment already carries the header's indent from pass 2, so inserting a blank
      // between them detaches the clause reference from the turn it explains — which is
      // the entire reason §10 chose TOML over JSON.
      //
      // Measured on the third real conversion: `# Attempt 1, at Offset 0 → SideA.` was
      // pushed one blank line away from the `[[turn]]` it introduces.
      const formatted = formatToml(`scenario = "p"

[[turn]]
id = "a"
user = "go"

  [[turn.step]]
  respond = { type = "text" }
# FALLBACK-002: attempt 2 is the same key, delivered again
[[turn]]
id = "b"
user = "go on"

  [[turn.step]]
  respond = { type = "text" }
`);

      assertTrue(
        formatted.includes('# FALLBACK-002: attempt 2 is the same key, delivered again\n[[turn]]'),
        `comment must stay adjacent to its turn:\n${formatted}`,
      );
      assertTrue(
        formatted.includes('respond = { type = "text" }\n\n# FALLBACK-002'),
        'the block separation moves to before the comment, not after it',
      );
      assertEq(formatToml(formatted), formatted);
    },
  },

  {
    name: 'VERIFY-003 every scenario in the forest compiles',
    fn: () => {
      // Until now nothing checked this. Each conversion was verified with a throwaway
      // `node -e` that compiled the one file just written — the one-off probe AGENTS.md
      // forbids as verification, because it proves the file loaded on the author's machine
      // at that moment and nothing afterwards.
      //
      // Walking the directory rather than listing names is the point: a scenario added
      // later is covered without anyone remembering to register it here.
      const files = walk(SCENARIO_ROOT, ['.toml']);
      assertTrue(files.length > 0, `no scenarios found under ${SCENARIO_ROOT}`);

      const broken = files
        .map((file) => ({ file, result: compileScenario(readFileSync(file, 'utf8'), { name: file }) }))
        .filter(({ result }) => !result.ok);

      assertEq(
        broken.length,
        0,
        broken.map(({ result }) => result.problems.join('\n    ')).join('\n  '),
      );
    },
  },

  {
    name: 'VERIFY-003 no internal turn declares a lane',
    fn: () => {
      // Now a load-time rejection in `scenario-schema.js`, so this walks the forest only to
      // prove the rule is actually in force on the real files — a compiler check nothing
      // exercises is the zero-call-site shape this whole package has been removing.
      const offenders = walk(SCENARIO_ROOT, ['.toml']).flatMap((file) => {
        const source = readFileSync(file, 'utf8');
        const blocks = source.split(/^\[\[turn\]\]$/m).slice(1);
        return blocks
          .filter((block) => {
            const own = block.split(/^\[\[/m)[0];
            return /^internal = true$/m.test(own) && /^lane = /m.test(own);
          })
          .map(() => file);
      });

      assertEq(offenders.length, 0, `internal turns must not name a lane: ${offenders.join(', ')}`);
    },
  },

  {
    name: 'COMPANION-002 a Companion turn declares no lane',
    fn: () => {
      // Measured in K9, and it turned every canary red at once.
      //
      // COMPANION-002 gives EVERY Managed Work Session exactly one Companion. A lane-bound
      // blogger turn answers only the single session that alias is bound to, so
      // `manager-full-loop` — six work sessions — had five Companion requests with no
      // declaration to answer them. The failure surfaced as `no-prefix-matched` on a blogger
      // request, which reads like a conversion slip in one file and was a rule violation in
      // eleven.
      //
      // The honest declaration omits the lane. A Companion prompt is IDENTICAL across
      // sessions because production composes it from the delta alone
      // (`src/Wanxiangshu.Next/Session/CompanionHostBlogger.fs:77`): nothing in the request distinguishes
      // one Blogger from another, and inventing a distinction would be the mock re-deriving
      // identity that §5 forbids. An omitted lane says exactly that — this content is a pure
      // function of the prompt and claims nothing about who asked.
      //
      // The retired `neverEnd` flag was standing in for this. K7 was right to remove it (it
      // also made one edge answer at every step, which is what `step` is for), but the thing
      // it approximated is real.
      const offenders = walk(SCENARIO_ROOT, ['.toml']).flatMap((file) =>
        readFileSync(file, 'utf8')
          .split('\n')
          .map((line, index) => ({ file, line: index + 1, text: line.trim() }))
          .filter(({ text }) => /^lane = ".*blogger.*"$/.test(text)),
      );

      assertEq(
        offenders.length,
        0,
        offenders.map(({ file, line, text }) => `${file}:${line} ${text}`).join(', '),
      );
    },
  },

  {
    name: 'COMPANION-002 every scenario declares its Companion turn',
    fn: () => {
      // The other half of the rule. A lane-bound blogger turn answers too few sessions (the
      // case above); NO blogger turn answers none at all, and production sends the request
      // regardless — COMPANION-002 attaches a Companion to every Managed Work Session, and
      // every scenario in this forest has at least one.
      //
      // Measured in K9: four scenarios (executor, fallback-aabb-trace, process-stress,
      // reviewer-restart) had no declaration and all four fail-stopped on the Blogger
      // request. A missing declaration is silent at load time and fatal at run time, which
      // is precisely the asymmetry a load-time gate should remove.
      // LWR sparse schema: normal/reset deltas are data-only TOML whose first table
      // is whatever the first semantic part is (`[[message]]`, `[[tool_result]]`,
      // `[[media_omitted]]`, …). The production-stable prefix is therefore `[[`.
      // Reset no longer has a separate English reanchor shape (same projector).
      const missing = walk(SCENARIO_ROOT, ['.toml']).filter(
        (file) => !readFileSync(file, 'utf8').includes('user = "[["'),
      );

      assertEq(missing.length, 0, `no Companion turn declared in: ${missing.join(', ')}`);
    },
  },

  {
    name: 'VERIFY-003 every scenario is already formatted',
    fn: () => {
      // `gate:toml` enforces this in CI, but that is a separate npm script: a scenario could
      // be committed unformatted and `test:harness` would stay green. The gate that reads
      // the forest should be the gate that says the forest is well-formed.
      const drifted = walk(SCENARIO_ROOT, ['.toml']).filter((file) => {
        const source = readFileSync(file, 'utf8');
        return formatToml(source) !== source;
      });

      assertEq(drifted.length, 0, `run node scripts/toml-format.mjs --write: ${drifted.join(', ')}`);
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
