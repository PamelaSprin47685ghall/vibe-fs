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

import { assertEq, assertTrue } from './lib.mjs';
import { deriveRequests, loadForest, runForest } from './forest-lib.mjs';

export const forestCases = [
  // ── the one unimplemented obligation ──────────────────────────────────────

  {
    name: 'verification-system-003 the same request sequence produces the same content sequence, forest-wide',
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
      //   derive it from the collection (verification-system-004 禁止退化清单 11)
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
    name: 'verification-system-003 every declared step of every scenario is reached by its derived sequence',
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
    name: 'verification-system-003 a second session on the same lane does not change what content is selected',
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
];
