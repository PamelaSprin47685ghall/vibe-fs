/**
 * gate-single-source-cases.mjs — a cardinality may not be maintained beside the collection it counts.
 *
 * VERIFY-004 禁止退化清单 item 11: 「数量常量与清单各自维护」, stated positively in the same
 * clause — canary 清单必须是单一事实来源。用于日志或断言的数量常量必须从清单派生，不得独立维护.
 *
 * The instance that produced this file: `run-canary-staggered.mjs` declared `CANARY_COUNT = 17`
 * eighteen lines above a `CANARY_TESTS` array holding 16 entries, and its one use printed both
 * sides of the disagreement in a single sentence — `Concurrency: 16 / 16 (expected ~17)`.
 *
 * It had drifted TWICE, and in opposite directions: the migration ledger recorded the array at
 * 19 against the same 17, then package K retired three canaries and left 16. That is the whole
 * argument for this being a class rather than a typo. A hand-maintained integer beside a list it
 * describes does not drift in a predictable direction that a reviewer could learn to watch for;
 * it drifts whichever way the next edit happens to go, and the edit that moves the list is never
 * the edit that remembers the integer.
 *
 * ── what the scan looks for ─────────────────────────────────────────────────
 *
 * A cardinality-declaring NAME assigned an integer literal, in a file that also declares a
 * collection about the same subject. Both halves are required, and the second is what keeps the
 * rule from being a ban on integers: `MAX_PARALLEL = 8` names a limit, not a population, and a
 * file holding `const RETRIES = 3` with no retry collection is not maintaining a duplicate fact.
 * The subject is what makes two declarations the same fact — `CANARY_COUNT` and `CANARY_TESTS`
 * are one fact spelled twice; `LEDGER_ENTRY_TTL_MS` and `ENTRIES` are not.
 *
 * A mutable accumulator (`let count = 0`, `let totalHits = 0`) is excluded by two conditions
 * together, not by name: it is `let`/`var` AND it starts at zero. Neither alone is enough — a
 * `const` zero cardinality is still a claim about a population, and a `let` seeded at 16 is
 * still a maintained number. Measured on this tree: the exclusion is what separates this rule
 * from the nine counters in `scenario-runner.js`, `harness/run.mjs`, `reaper.mjs` and
 * `strip-doc-bold.mjs`, all of which are `let … = 0`.
 *
 * Like `path-criterion-cases.mjs` and unlike former budget gates, this scan does not mask
 * comments or strings: under-reporting IS the degradation being removed, so the two error
 * directions are not symmetric. The cost is that this file's own fixtures assemble their
 * declarations from parts instead of spelling them (see `declarationSource`).
 */

import { readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { availableParallelism } from 'node:os';
import { basename, isAbsolute, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertEq, assertTrue, tmpScenarioDir } from './lib.mjs';
import { walk } from '../../../scripts/lib/walk.mjs';
import {
  CANARY_DIR,
  CANARY_MAX_PARALLEL,
  CANARY_SUFFIX,
  CANARY_TESTS,
  nonConformingCanaryNames,
  readCanaryTests,
} from '../../e2e/support/manifest.mjs';

/** Resolution base, derived from this file rather than from `cwd`, so the gate is location-free. */
const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));

/** The one consumer whose hand-maintained registry this package replaced. */
const RUNNER = 'tests/e2e/run.mjs';

/** The harness and its scripts: where a scenario registry and its consumers live. */
const SCOPE = [
  { root: 'tests/e2e', extensions: ['.js', '.mjs'] },
  { root: 'scripts', extensions: ['.js', '.mjs'] },
];

const scopedFiles = () =>
  SCOPE.flatMap(({ root, extensions }) => walk(join(REPO_ROOT, root), extensions)).map((file) =>
    relative(REPO_ROOT, file),
  );

// ── reading a declaration ───────────────────────────────────────────────────

/** A name bound to a bare integer literal — the initializer must be the whole value. */
const INTEGER_DECL = /\b(const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(\d+)\s*(?=[;,)\]}\n]|$)/g;

/** A name bound to something that holds a population. */
const COLLECTION_DECL =
  /\b(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(\[|new\s+(?:Set|Map)\s*\(|Object\.freeze\s*\(\s*\[)/g;

/** `canaryCount` and `CANARY_COUNT` are the same name to a reader, so make them the same string. */
const upperSnake = (name) => name.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();

/**
 * Compare names by letters alone, singular — `CANARY_TESTS` and `canaryTest` are one subject.
 *
 * The `IES` branch is not decoration: `NUM_CANARIES` beside `CANARY_TESTS` is the most natural way
 * to spell this defect in English, and stripping a trailing `S` alone leaves `CANARIE`, which
 * shares no prefix with `CANARY`. Measured as a false negative on this file's own fixture.
 */
const normalize = (name) =>
  upperSnake(name)
    .replace(/[^A-Z0-9]/g, '')
    .replace(/IES$/, 'Y')
    .replace(/S$/, '');

const CARDINALITY_SUFFIX = /^(.*?)_?(?:COUNT|TOTAL|SIZE|LEN|LENGTH|NUM)S?$/;
const CARDINALITY_PREFIX = /^(?:NUM|TOTAL|COUNT)_(.+)$/;

/**
 * What `name` claims to count, or null if it does not claim to count anything.
 *
 * The subject must survive as at least three letters. `count`, `total` and `n` reduce to nothing
 * — they name the act of counting, not a population, and a bare accumulator is the only thing
 * ever spelled that way.
 */
function subjectOf(name) {
  const upper = upperSnake(name);
  const match = CARDINALITY_SUFFIX.exec(upper) ?? CARDINALITY_PREFIX.exec(upper);
  if (!match) return null;
  const subject = normalize(match[1]);
  return subject.length >= 3 ? subject : null;
}

/**
 * How many top-level entries the collection opened at `from` holds, or null if it cannot be read.
 *
 * Reported so the message states the drift itself (`17` beside a list of `16`) rather than only
 * its shape. A collection this scan cannot count is still a violation — the duplication is the
 * defect, and the two numbers agreeing today is not a defence.
 *
 * Entries are counted as SEPARATED CONTENT, not as separators plus one. The first version counted
 * commas and added one; run against the real defect it reported `CANARY_TESTS, which holds 17`
 * for a list of 16, because that array ends with a trailing comma. A gate whose message states a
 * false number about the tree is the failure shape this file exists to remove, one level up.
 */
function entryCount(source, from) {
  if (source[from] !== '[') return null;
  let depth = 0;
  let entries = 0;
  let pending = false;

  for (let at = from; at < source.length; at += 1) {
    const char = source[at];
    if (char === '"' || char === "'" || char === '`') {
      const end = source.indexOf(char, at + 1);
      if (end === -1) return null;
      at = end;
      pending = true;
      continue;
    }
    if ('[({'.includes(char)) {
      if (depth > 0) pending = true;
      depth += 1;
    } else if (')}'.includes(char)) depth -= 1;
    else if (char === ']') {
      depth -= 1;
      if (depth === 0) return pending ? entries + 1 : entries;
    } else if (char === ',' && depth === 1) {
      if (pending) entries += 1;
      pending = false;
    } else if (!/\s/.test(char)) pending = true;
  }

  return null;
}

/** Every collection a file declares, as subject → { name, line, entries }. */
function collectionsIn(source) {
  COLLECTION_DECL.lastIndex = 0;
  const found = [];
  let match;
  while ((match = COLLECTION_DECL.exec(source)) !== null) {
    found.push({
      name: match[1],
      subject: normalize(match[1]),
      line: source.slice(0, match.index).split('\n').length,
      entries: entryCount(source, match.index + match[0].length - 1),
    });
  }
  return found;
}

/** Every cardinality maintained beside a same-subject collection, as reportable strings. */
function duplicatedCardinalities(files, base) {
  return files.flatMap((file) => {
    const source = readFileSync(join(base, file), 'utf8');
    const collections = collectionsIn(source);
    if (collections.length === 0) return [];

    INTEGER_DECL.lastIndex = 0;
    const problems = [];
    let match;

    while ((match = INTEGER_DECL.exec(source)) !== null) {
      const [, declarator, name, literal] = match;
      const value = Number(literal);
      if (declarator !== 'const' && value === 0) continue;

      const subject = subjectOf(name);
      if (subject === null) continue;

      const paired = collections.find(
        (collection) => collection.subject.includes(subject) || subject.includes(collection.subject),
      );
      if (paired === undefined) continue;

      const held = paired.entries === null ? 'the collection' : `${paired.name}, which holds ${paired.entries}`;
      problems.push(
        `${file}:${source.slice(0, match.index).split('\n').length} ${name} = ${value} restates the size of ` +
          `${held} (declared at :${paired.line}); derive it from the collection ` +
          '(VERIFY-004 禁止退化清单 11)',
      );
    }

    return problems;
  });
}

// ── fixtures ────────────────────────────────────────────────────────────────

/**
 * One declaration line, assembled instead of written out.
 *
 * This file is inside the scanned scope and the scan deliberately reads strings and comments, so a
 * fixture spelled literally would be found by the real-tree case above and reported as a defect
 * here. Assembly keeps the declaration shape out of this file's text — the price of not lexing,
 * paid the same way `gate-path-criterion-cases.mjs` pays it.
 */
const declarationSource = (declarator, name, value) => `${declarator} ${name}${' ='} ${value};\n`;

/** A collection literal of `entries` string members, spread over lines like the real registry was. */
const listSource = (declarator, name, entries, trailingComma = true) =>
  `${declarator} ${name}${' ='} [\n` +
  Array.from({ length: entries }, (unused, at) => `  "member-${at}"`).join(',\n') +
  (trailingComma && entries > 0 ? ',' : '') +
  '\n]\n';

/**
 * Audit one assembled source without touching the repo, and take the temp tree back down.
 *
 * The removal is not housekeeping. VERIFY-004's leak check names 临时目录 among the things a scenario
 * must leave empty, and these cases build one per fixture: 17 per run, measured. The sibling case
 * files do not remove theirs — 35 more per harness run, against 848 already on this machine's
 * `/tmp` when this was written — and a gate that leaks the resource it audits is in no position to
 * report a leak. Out of scope to fix theirs here; in scope not to add to it.
 */
const reported = (source) => {
  const dir = tmpScenarioDir();
  try {
    writeFileSync(join(dir, 'fixture.mjs'), source);
    return duplicatedCardinalities(['fixture.mjs'], dir);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
};

const rejects = (source, fragment) => {
  const problems = reported(source);
  assertTrue(problems.length > 0, `expected a violation for:\n${source}`);
  assertTrue(problems.some((problem) => problem.includes(fragment)), `expected '${fragment}', got: ${problems.join(' | ')}`);
  return problems;
};

const accepts = (source, why) => {
  const problems = reported(source);
  assertEq(problems.length, 0, `${why}: ${problems.join(' | ')}`);
};

/** Build a temp tree, hand its root to `body`, and remove it whatever `body` does. */
const withTree = (files, body) => {
  const dir = tmpScenarioDir();
  try {
    for (const [name, content] of Object.entries(files)) writeFileSync(join(dir, name), content);
    return body(dir);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
};

export const singleSourceCases = [
  {
    name: 'VERIFY-004 no cardinality is maintained beside the collection it counts',
    fn: () => {
      // The case that was red when it was written, naming `run-canary-staggered.mjs` and
      // `CANARY_COUNT`. Both halves of the assertion matter: a scan that stopped reading
      // declarations would report zero problems, which is the same green as a correct tree.
      const files = scopedFiles();
      assertTrue(files.length > 0, `no sources found under ${SCOPE.map((s) => s.root).join(', ')}`);

      const collections = files.filter((file) => collectionsIn(readFileSync(join(REPO_ROOT, file), 'utf8')).length > 0);
      assertTrue(collections.length > 0, 'the collection reader matched nothing — it has stopped reading');

      const problems = duplicatedCardinalities(files, REPO_ROOT);
      assertEq(problems.length, 0, problems.join(' | '));
    },
  },

  {
    name: 'VERIFY-004 a count beside its collection is reported with both numbers',
    fn: () => {
      // The refusal itself, on the measured defect: a 17 declared above a list of 16. The message
      // carries BOTH numbers because the duplication is only visible as a pair — 「derive this」 on
      // its own reads like a style note, while 17-beside-16 is the argument.
      const problems = rejects(
        declarationSource('const', 'CANARY_COUNT', 17) + listSource('const', 'CANARY_TESTS', 16),
        'restates the size of',
      );

      assertEq(problems.length, 1, `exactly one problem: ${problems.join(' | ')}`);
      assertTrue(problems[0].startsWith('fixture.mjs:1 CANARY_COUNT = 17 '), `must name the site: ${problems[0]}`);
      assertTrue(problems[0].includes('CANARY_TESTS, which holds 16'), `and the other side: ${problems[0]}`);
      assertTrue(problems[0].includes('declared at :2'), `and where it is: ${problems[0]}`);

      // Agreeing today is not a defence: the defect is the second place to edit, not the mismatch.
      rejects(declarationSource('const', 'CANARY_COUNT', 16) + listSource('const', 'CANARY_TESTS', 16), 'derive it');

      // Both spellings this tree uses, and the prefix form.
      rejects(declarationSource('const', 'canaryCount', 16) + listSource('const', 'canaryTests', 16), 'canaryCount');
      rejects(declarationSource('const', 'NUM_CANARIES', 16) + listSource('const', 'CANARY_TESTS', 16), 'NUM_CANARIES');
      rejects(
        declarationSource('const', 'SCENARIO_TOTAL', 3) + `const scenarios${' ='} new Set(["a"]);\n`,
        'SCENARIO_TOTAL',
      );
    },
  },

  {
    name: 'VERIFY-004 a bound, an accumulator and an unpaired count are not duplicated facts',
    fn: () => {
      // The discriminator, stated as acceptances — without these the rule degenerates into "no
      // integers near arrays", which is the shape that gets deleted rather than obeyed.
      accepts(
        declarationSource('const', 'MAX_PARALLEL', 8) + listSource('const', 'CANARY_TESTS', 16),
        'a limit is not a population',
      );
      accepts(
        declarationSource('let', 'passed', 0) + declarationSource('let', 'failureCount', 0) + listSource('const', 'FAILURES', 3),
        'a mutable accumulator seeded at zero counts what it observes, not what a list holds',
      );
      accepts(
        declarationSource('const', 'RETRY_COUNT', 3) + `const message${' ='} "no collection here";\n`,
        'a count with no same-subject collection duplicates nothing',
      );
      accepts(
        declarationSource('const', 'LANE_COUNT', 4) + listSource('const', 'CANARY_TESTS', 16),
        'a count about another subject is another fact',
      );

      // The two exclusion conditions are required TOGETHER, so each is shown to be load-bearing on
      // its own: a `const` zero is still a claim about a population, and a `let` seeded at a real
      // number is still a maintained number.
      rejects(declarationSource('const', 'CANARY_COUNT', 0) + listSource('const', 'CANARY_TESTS', 16), 'CANARY_COUNT = 0');
      rejects(declarationSource('let', 'CANARY_COUNT', 16) + listSource('const', 'CANARY_TESTS', 16), 'CANARY_COUNT = 16');
    },
  },

  {
    name: 'VERIFY-004 the reader counts entries, not separators',
    fn: () => {
      // Measured while writing this file: counting commas and adding one reported the real defect
      // as `CANARY_TESTS, which holds 17` — the registry ends with a trailing comma, so the two
      // 17s agreed and the drift looked like a match. A gate whose message states a false number
      // about the tree is the failure this file exists to remove, one level up (a green check that
      // restates a wrong count is worse than no check).
      const holds = (source) => {
        const problems = rejects(source, 'restates the size of');
        return problems[0].replace(/^.*which holds (\d+).*$/, '$1');
      };

      assertEq(holds(declarationSource('const', 'ITEM_COUNT', 1) + listSource('const', 'ITEMS', 3)), '3', 'trailing comma');
      assertEq(
        holds(declarationSource('const', 'ITEM_COUNT', 1) + listSource('const', 'ITEMS', 3, false)),
        '3',
        'no trailing comma',
      );
      assertEq(
        holds(declarationSource('const', 'ITEM_COUNT', 1) + `const items${' ='} [];\n`),
        '0',
        'an empty collection holds zero, and a count beside it is still a second fact',
      );
      assertEq(
        holds(declarationSource('const', 'ITEM_COUNT', 1) + `const items${' ='} [{ a: 1, b: 2 }, { a: 3, b: 4 }];\n`),
        '2',
        'commas inside a nested object are not separators of the outer collection',
      );
    },
  },

  // ── the manifest: the derivation itself, and its two fail-closed refusals ──

  {
    name: 'VERIFY-004 canary parallelism follows available CPUs',
    fn: () => {
      assertEq(
        CANARY_MAX_PARALLEL,
        Math.min(Math.max(1, Math.floor(availableParallelism() / 2)), CANARY_TESTS.length),
        'one canary slot must reserve one CPU each for its Host and scenario/provider process',
      );
    },
  },

  {
    name: 'VERIFY-004 the canary suite is exactly the -canary.mjs files on disk',
    fn: () => {
      // The other direction of the same rule. The count case above catches a reintroduced
      // `CANARY_COUNT`; this catches a reintroduced LIST — a hand-written array with no count
      // constant beside it would satisfy every assertion so far while quietly omitting a canary.
      //
      // Read with `readdirSync` rather than with the manifest's own `walk`, so the comparison is
      // against the filesystem and not against the manifest agreeing with itself.
      const onDisk = readdirSync(CANARY_DIR)
        .filter((name) => name.endsWith(CANARY_SUFFIX))
        .sort();

      assertTrue(onDisk.length > 0, `no ${CANARY_SUFFIX} file under ${CANARY_DIR}`);
      assertEq(
        JSON.stringify(CANARY_TESTS.map((file) => basename(file))),
        JSON.stringify(onDisk),
        'the manifest and the directory must be the same set, in the same order',
      );

      // Absolute, because the manifest resolves its own root: a repo-relative path would make the
      // suite depend on the caller's cwd, which the derivation itself does not.
      assertTrue(CANARY_TESTS.every((file) => isAbsolute(file)), 'every entry must be an absolute path');
      assertTrue(Object.isFrozen(CANARY_TESTS), 'the single source may not be mutated by a consumer');
    },
  },

  {
    name: 'VERIFY-004 the staggered runner keeps no canary list of its own',
    fn: () => {
      // A gate that only counted would be satisfied by pasting the array back without its
      // constant. The runner is allowed exactly one way to know what the suite is.
      const runner = readFileSync(join(REPO_ROOT, RUNNER), 'utf8');

      assertTrue(/from\s*['"][^'"]*manifest/.test(runner), `${RUNNER} must import the manifest`);

      const pasted = runner
        .split('\n')
        .map((text, at) => ({ line: at + 1, text }))
        .filter(({ text }) => new RegExp(`['"\`][^'"\`]*${CANARY_SUFFIX.replace('.', '\\.')}`).test(text));

      assertEq(
        pasted.length,
        0,
        `${RUNNER} names scenario case files directly: ${pasted.map(({ line, text }) => `${line} ${text.trim()}`).join(' | ')}`,
      );
    },
  },

  {
    name: 'VERIFY-004 an empty or missing canary directory is refused, not read as a suite of zero',
    fn: () => {
      // 「一个能对错误实现给出绿灯的验证装置，比没有验证装置更危险」. A manifest returning `[]` would let
      // the release gate run its 恰好 3 轮 over nothing and exit 0 — the strongest possible green,
      // proving nothing. Both failing trees are exercised because they need different fixes and the
      // message is the only thing that distinguishes them.
      withTree({ 'gate-lib.mjs': 'export const x = 1;\n' }, (empty) => {
        for (const [why, dir] of Object.entries({ empty, missing: join(empty, 'gone') })) {
          let refusal = null;
          try {
            readCanaryTests(dir);
          } catch (err) {
            refusal = err.message;
          }

          assertTrue(refusal !== null, `a ${why} directory must throw, not return an empty suite`);
          assertTrue(refusal.includes(dir), `the refusal must name where it looked: ${refusal}`);
          assertTrue(refusal.includes(CANARY_SUFFIX), `and what it looked for: ${refusal}`);
        }
      });
    },
  },

  {
    name: 'VERIFY-004 a file that claims to be a canary must match the convention',
    fn: () => {
      // The silent-omission hazard one level up from a stale array. Legacy `*-canary.mjs` stems claim
      // canary identity without matching `*.test.mjs`, so they would shrink the suite and stay green.
      // Reported rather than thrown at import: a narrower suite is a failing harness case, while
      // taking every scenario run down over an unluckily named helper is not proportionate.
      const claims = ['real.test.mjs', 'scenario-driver.mjs', 'underscore_canary.mjs', 'legacy-canary.mjs'];

      withTree(Object.fromEntries(claims.map((name) => [name, 'export const x = 1;\n'])), (dir) => {
        assertEq(
          JSON.stringify(nonConformingCanaryNames(dir)),
          JSON.stringify(['legacy-canary.mjs', 'underscore_canary.mjs']),
          'a stem ending in canary must use the case suffix; a file merely ABOUT scenarios claims nothing',
        );
        assertEq(JSON.stringify(readCanaryTests(dir).map((file) => basename(file))), JSON.stringify(['real.test.mjs']));
      });

      // And the real directory obeys it.
      assertEq(JSON.stringify(nonConformingCanaryNames()), '[]', 'rename it to end in the suffix, or stop claiming to be one');
    },
  },
];
