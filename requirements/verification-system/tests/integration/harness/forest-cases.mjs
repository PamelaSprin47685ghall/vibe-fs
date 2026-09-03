/**
 * gate-forest-cases.mjs — K10, the forest checked as a whole rather than per fixture.
 *
 * `design-script-forest.md:560` lists four items for K10: 纯函数性、索引无冲突、fault 有限、
 * 无死边. Three of them were already implemented and already gated when this file was
 * written, so this file does NOT re-implement them — a second checker for a rule that
 * already has one is the shape packages W1 and W2 of this migration spent their whole
 * effort removing, and two checkers can disagree in a way one cannot.
 *
 * What is left is therefore two things:
 *
 *   the one unimplemented obligation, `design-script-forest.md:581`
 *     「森林自检：同请求序列 → 同内容序列」 — 纯函数性 stated forest-wide
 *
 *   a presence table proving the other three are actually in force, so that
 *     "already covered" is a checked fact instead of a claim in prose
 *
 * The table is shaped after `scripts/shock-audit.mjs`'s symbol-extinction table with the
 * direction reversed: that one fails when a retired symbol still exists, this one fails
 * when an enforcing symbol or case name stops existing. Both answer the same question —
 * is the thing I believe about this repository still true — and neither takes prose as
 * evidence.
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { assertEq, assertTrue } from './lib.mjs';
import { deriveRequests, loadForest, runForest } from './forest-lib.mjs';

const repoFile = (path) => readFileSync(fileURLToPath(new URL(`../../../${path}`, import.meta.url)), 'utf8');

/**
 * Each already-gated K10 item, the production symbol that enforces it, and the case name
 * that proves the enforcement fires.
 *
 * Case NAMES rather than case counts: a count moves whenever anyone adds an unrelated case,
 * so it would either be updated reflexively or become a permanent nuisance. A name changes
 * only when someone renames or deletes the specific case, which is the event this table
 * exists to catch.
 *
 * Read out of the files rather than recalled — `duplicateDeclarations` for instance is
 * enforced by two cases (different responses, and identical responses), and listing only
 * one of them would let the other be deleted silently.
 */
const PRESENCE_TABLE = [
  {
    item: '无死边',
    symbols: [['tests/e2e/support/scenario-schema.js', 'deadEdges']],
    cases: [
      ['tests/integration/harness/schema-cases.mjs', 'VERIFY-003 a turn no flow can reach is rejected'],
    ],
  },
  {
    item: '索引无冲突',
    symbols: [
      ['tests/e2e/support/scenario-schema.js', 'duplicateDeclarations'],
      ['tests/e2e/support/runtime-key.js', 'ambiguousTurn'],
    ],
    cases: [
      [
        'tests/integration/harness/schema-cases.mjs',
        'VERIFY-003 two declarations for one key with different responses are rejected',
      ],
      [
        'tests/integration/harness/schema-cases.mjs',
        'VERIFY-003 two declarations for one key with the SAME response are also rejected',
      ],
      [
        'tests/integration/harness/runtime-key-cases.mjs',
        'VERIFY-003 two same-length prefixes are ambiguous, never scored',
      ],
      [
        'tests/integration/harness/runtime-key-cases.mjs',
        'REVIEW-003 two fragment declarations of equal weight are an author error',
      ],
    ],
  },
  {
    item: 'fault 有限',
    symbols: [
      ['tests/e2e/support/delivery-plan.js', 'validateFault'],
      ['tests/e2e/support/scenario-schema.js', 'conflictingFaults'],
    ],
    cases: [
      [
        'tests/integration/harness/delivery-cases.mjs',
        'VERIFY-003 an empty attempts list is rejected at load time',
      ],
      [
        'tests/integration/harness/schema-cases.mjs',
        'VERIFY-003 a malformed fault is rejected by the real compiler, not only the unit',
      ],
      [
        'tests/integration/harness/schema-cases.mjs',
        'VERIFY-003 two faults on one key are rejected at load, not at delivery',
      ],
    ],
  },
];

export const forestCases = [
  // ── the one unimplemented obligation ──────────────────────────────────────

  {
    name: 'VERIFY-003 the same request sequence produces the same content sequence, forest-wide',
    fn: () => {
      // 纯函数性 is the only one of K10's four items with no existing gate, and the only one
      // that cannot be stated per fixture: a single scenario can look deterministic while the
      // matcher still carries state that shows up only when a second run reuses it.
      //
      // Three things would make this vacuous, and each is defended against here:
      //
      //   comparing a run to itself             → two independent ScenarioRuntime instances
      //   deriving the sequence from the answers → `deriveRequests` reads the COMPILED
      //                                            SCENARIO only, so the input cannot adapt
      //                                            to the output
      //   serialising too little                 → each line carries the resolved entry id,
      //                                            the selection shape, the attempt number,
      //                                            and a digest of the response; a matcher
      //                                            that answered a different entry, refused
      //                                            instead of delivering, mis-counted a
      //                                            delivery, or changed a reply moves the text
      //
      // The historical target is `pathCursor`: the retired matcher advanced a per-path cursor
      // on every match, so asking the same question twice could answer differently. Under
      // (lane, kind, turn, step) there is no cursor to advance, and this is where that claim
      // stops being an argument and becomes a measurement.
      // Deliberately NOT pinned to a scenario count. The first draft of this case asserted
      // `forest.length === 15`, and package W2's single-source gate rejected it on the spot:
      //
      //   gate-forest-cases.mjs:103 FOREST_SIZE = 15 restates the size of a collection;
      //   derive it from the collection (VERIFY-004 禁止退化清单 11)
      //
      // The gate was right, and about my own code. `loadForest` walks the directory precisely
      // so a scenario added later joins this property automatically; a pinned count would
      // contradict that and would have to be edited by hand every time the forest changed —
      // the same drift `CANARY_COUNT = 17` produced against a 16-entry list one layer up.
      //
      // Coverage is instead reported: `underivable` names any scenario this property does not
      // reach, so the claim is bounded by evidence rather than by a number.
      const forest = loadForest();

      const drifted = [];
      const underivable = [];

      for (const { name, scenario } of forest) {
        const derived = deriveRequests(scenario);
        if (derived.underivable !== undefined) {
          underivable.push(`${name}: ${derived.underivable}`);
          continue;
        }

        const first = runForest(scenario, derived);
        const second = runForest(scenario, derived);

        if (first.text !== second.text) drifted.push(`${name} differs between two runs`);
        if (first.mismatches.length > 0) {
          drifted.push(`${name} resolved elsewhere: ${first.mismatches.join('; ')}`);
        }
      }

      // Reported rather than tolerated. A scenario whose requests cannot be derived is
      // outside this property, and naming it keeps the coverage claim honest instead of
      // letting fifteen quietly become fourteen.
      assertEq(underivable.length, 0, `underivable: ${underivable.join(' | ')}`);
      assertEq(
        drifted.length,
        0,
        `content sequence is not a function of the request sequence: ${drifted.join(' | ')}`,
      );
    },
  },

  {
    name: 'VERIFY-003 every declared step of every scenario is reached by its derived sequence',
    fn: () => {
      // The other half of the determinism claim, and why the case above cannot stand alone:
      // two runs that both reach nothing agree perfectly. `unanswered()` is the runtime's own
      // report of declared steps no request selected, so a derived sequence that exercises
      // only part of a scenario surfaces here rather than as a stable-looking pass there.
      //
      // `internal` turns are exempt inside `unanswered()` itself — production decides whether
      // to compose those prompts at all — so this asserts what the runtime considers
      // reachable, not what the file happens to contain.
      const gaps = [];

      for (const { name, scenario } of loadForest()) {
        const derived = deriveRequests(scenario);
        if (derived.underivable !== undefined) continue;

        const run = runForest(scenario, derived);
        if (run.unanswered.length > 0) gaps.push(`${name}: ${run.unanswered.join(', ')}`);
      }

      assertEq(gaps.length, 0, `declared but never reached by the derived sequence: ${gaps.join(' | ')}`);
    },
  },

  {
    name: 'VERIFY-003 a second session on the same lane does not change what content is selected',
    fn: () => {
      // Not in K10's charter; added because `lanesOf` binds an alias to a SET of sessions
      // (measured in K9: `reviewer` legitimately holds two forks), which makes "does the
      // session leak into content selection" a real question rather than a rhetorical one.
      //
      // The property: binding a SECOND session id to every alias must not change any answer.
      // Content is a function of the request, and which session asked is part of the
      // request's addressing, not of its content. If a second binding moved a single line the
      // matcher would be consulting the binding table for something other than the lane.
      const changed = [];

      for (const { name, scenario } of loadForest()) {
        const derived = deriveRequests(scenario);
        if (derived.underivable !== undefined) continue;

        const baseline = runForest(scenario, derived);

        const second = derived.bindings.map(([alias, sessionId]) => [alias, `${sessionId}_second`]);
        const withSecond = runForest(scenario, {
          bindings: [...derived.bindings, ...second],
          requests: derived.requests,
        });

        if (baseline.text !== withSecond.text) changed.push(name);
      }

      assertEq(
        changed.length,
        0,
        `a second session on the same lane changed content selection: ${changed.join(', ')}`,
      );
    },
  },

  // ── the presence table for the three already-gated items ──────────────────

  {
    name: 'VERIFY-003 every already-gated K10 item still has its enforcing symbol',
    fn: () => {
      // Without this, "three of four are already covered" is a sentence in a commit message.
      // With it, deleting `deadEdges` or renaming `conflictingFaults` fails here rather than
      // quietly reducing K10 to one item.
      const missing = [];

      for (const { item, symbols } of PRESENCE_TABLE) {
        for (const [path, symbol] of symbols) {
          if (!repoFile(path).includes(symbol)) missing.push(`${item}: ${path} no longer defines ${symbol}`);
        }
      }

      assertEq(missing.length, 0, missing.join(' | '));
    },
  },

  {
    name: 'VERIFY-003 every already-gated K10 item still has its enforcing case',
    fn: () => {
      // A symbol that exists but is never exercised is the zero-call-site shape this
      // repository has now measured four times: `buildAttemptExecutionProfile` with no
      // caller, `faultFor` keyed by text so no fault ever fired, `boundaryFor` the same, and
      // the `containsTool` branch package W3 deleted. So the table pins the case NAME too,
      // and a rename surfaces as a failure naming the case that vanished.
      const missing = [];

      for (const { item, cases } of PRESENCE_TABLE) {
        for (const [path, caseName] of cases) {
          if (!repoFile(path).includes(caseName)) missing.push(`${item}: ${path} lost case "${caseName}"`);
        }
      }

      assertEq(missing.length, 0, missing.join(' | '));
    },
  },

  {
    name: 'VERIFY-003 the presence table covers exactly the three items K10 delegates',
    fn: () => {
      // The table's own completeness. K10 has four items; one is asserted directly by the
      // determinism cases above and three are delegated. A table that silently dropped an
      // item would leave that item unchecked while every other case in this file still
      // passed — the hazard this file's placeholder header warned about.
      assertEq(
        PRESENCE_TABLE.map(({ item }) => item).join(', '),
        '无死边, 索引无冲突, fault 有限',
        'K10 delegates exactly these three; 纯函数性 is asserted directly above',
      );

      for (const { item, symbols, cases } of PRESENCE_TABLE) {
        assertTrue(symbols.length > 0, `${item} has no enforcing symbol, so the delegation is unproven`);
        assertTrue(cases.length > 0, `${item} has no enforcing case, so the symbol could be dead`);
      }
    },
  },
];
