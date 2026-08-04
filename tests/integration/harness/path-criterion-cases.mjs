/**
 * gate-path-criterion-cases.mjs — a static gate may not name a directory the tree lacks.
 *
 * VERIFY-004 禁止退化清单 item 12: 「静态门禁的路径判据指向不存在的目录」, and the clause states
 * the cost itself — 指向不存在目录的检查恒为通过，是伪门禁，等同于没有检查.
 *
 * The instance that produced this file: `stability-checker.js` guarded a forbidden vocabulary
 * behind the directory prefix e2e/opencode/specs/, which this repository has never contained.
 * All 19 files that call `runStaticGate` therefore ran that branch and could not reach it, and
 * the check reported OK forever. That is one edit to fix; the class is not, because nothing
 * else in the tree connects a criterion to the filesystem it claims to describe.
 *
 * ── why this scan does not lex ──────────────────────────────────────────────
 *
 * It matches text: a `.includes` / `.startsWith` / `.endsWith` call whose first argument is a
 * single-line quoted literal, anywhere in the file — including inside a comment or inside a
 * string. `budget-gate.mjs` masks comments and strings before applying its rules and pays for
 * it with a regex-versus-division heuristic; here the two error directions are not symmetric. A
 * scan that under-reports is the pseudo-gate shape this case exists to remove, so this one errs
 * toward reporting, and the two residual costs are named rather than hidden:
 *
 *   prose describing a criterion must name the path without writing the call shape
 *   a fixture criterion in this file is assembled from parts (see `criterionSource`)
 *
 * A criterion that is not a plain single-line literal — a variable, a concatenation, a
 * multi-line template — is not read at all. Nothing static can resolve those.
 *
 * ── the domain, by shape rather than by allowlist ───────────────────────────
 *
 * A literal enters the domain when the repo root can resolve it: it contains a `/`, and it is
 * not absolute (the machine's filesystem is not this repo), does not begin `../` (it escapes
 * the root, so the root is not its base), carries no scheme (a URL is not a location on disk),
 * no `${` (the text is not the path), and no glob metacharacter (a pattern matches a set, not
 * a location). Every exclusion is a statement about the resolution base, not about the author's
 * intent, and there is no list of specific strings — an allowlist is how the criterion being
 * deleted alongside this file survived for two releases.
 *
 * Measured on the tree this file was written against: of 166 string arguments to those three
 * methods, 9 contained a `/`, 5 were repo-relative, and exactly one of those 5 named nothing —
 * the criterion deleted alongside this file. The other 4 left the domain by shape: `/custom/bin`
 * and `/src/` are absolute, `../src` escapes the root, `spec/${file}` interpolates. Those three
 * are content substring tests rather than locators, so the shape rule and the intent agree
 * there; the rule cannot tell a repo-relative path from a media type such as
 * `application/json`, and if one ever appears as an argument here it will be reported.
 *
 * `isRepoRelativePath` below is itself read by the scan, and its four criteria (`/`, `../`)
 * leave the domain by the same absolute and parent-escape rules they implement. That is the
 * shape rule demonstrating it does not need to know about this file.
 */

import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertEq, assertTrue, tmpScenarioDir } from './lib.mjs';
import { walk } from '../../../scripts/lib/walk.mjs';

/** Resolution base, derived from this file rather than from `cwd`, so the gate is location-free. */
const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));

/** The harness's own gate and static-check sources: the files VERIFY-004 is speaking about. */
const SCOPE = [
  { root: 'tests/e2e', extensions: ['.js', '.mjs'] },
  { root: 'scripts', extensions: ['.js', '.mjs'] },
];

const scopedFiles = () =>
  SCOPE.flatMap(({ root, extensions }) => walk(join(REPO_ROOT, root), extensions)).map((file) =>
    relative(REPO_ROOT, file),
  );

const CRITERION = /\.(includes|startsWith|endsWith)\(\s*(['"`])((?:[^'"`\\\n]|\\.)*)\2/g;

const isRepoRelativePath = (literal) =>
  literal.includes('/') &&
  !literal.startsWith('/') &&
  !literal.startsWith('../') &&
  !/^[a-z][a-z\d+.-]*:/i.test(literal) &&
  !literal.includes('${') &&
  !/[*?\[\]{}]/.test(literal);

/** Every path criterion in `files`, read relative to `base`. */
function pathCriteria(files, base) {
  return files.flatMap((file) => {
    const source = readFileSync(join(base, file), 'utf8');

    return [...source.matchAll(CRITERION)]
      .filter((match) => isRepoRelativePath(match[3]))
      .map((match) => ({
        file,
        line: source.slice(0, match.index).split('\n').length,
        method: match[1],
        criterion: match[3],
      }));
  });
}

/**
 * Whether `criterion` names something that exists under `base`.
 *
 * A trailing slash is load-bearing: a directory-prefix criterion whose name resolves to a FILE
 * can never match any path, so it must resolve to a directory. Treating it as merely "exists"
 * would have accepted the very shape being deleted had `e2e` existed as a file.
 */
const resolvesUnder = (criterion, base) => {
  const target = join(base, criterion);
  if (!existsSync(target)) return false;
  return criterion.endsWith('/') ? statSync(target).isDirectory() : true;
};

const problemsIn = (files, base) =>
  pathCriteria(files, base)
    .filter(({ criterion }) => !resolvesUnder(criterion, base))
    .map(
      ({ file, line, method, criterion }) =>
        `${file}:${line} .${method}('${criterion}') names nothing under the repo root; ` +
        'a criterion that cannot match is a pseudo-gate (VERIFY-004 禁止退化清单 12)',
    );

// ── fixtures ────────────────────────────────────────────────────────────────

/**
 * One source line carrying a criterion, assembled instead of written out.
 *
 * This file is itself inside the scanned scope, so a fixture written literally would be found
 * by the real-tree case and reported as a defect here. Assembly keeps the call shape out of
 * this file's text, which is the price of not lexing (see the header).
 */
const criterionSource = (method, literal, quote = "'") =>
  `export const by${method} = (candidate) => candidate${'.'}${method}(${quote}${literal}${quote});\n`;

/** Write `files` into a fresh temp tree and return its root. */
const fixtureTree = (files) => {
  const base = tmpScenarioDir();
  for (const [name, content] of Object.entries(files)) {
    mkdirSync(join(base, dirname(name)), { recursive: true });
    writeFileSync(join(base, name), content);
  }
  return base;
};

export const pathCriterionCases = [
  {
    name: 'VERIFY-004 every path criterion in the harness resolves on disk',
    fn: () => {
      // After 0.5.3 gate-forest retirement, the live tree may have zero repo-relative path
      // criteria (deleted scanners held the last ones). Vacuous pass is correct: every
      // criterion that still exists must resolve. Reader liveness is covered by fixture
      // cases below (three methods / quote styles / domain exclusions).
      const files = scopedFiles();
      assertTrue(files.length > 0, `no sources found under ${SCOPE.map((s) => s.root).join(', ')}`);

      const problems = problemsIn(files, REPO_ROOT);
      assertEq(problems.length, 0, problems.join(' | '));
    },
  },

  {
    name: 'VERIFY-004 a criterion naming a missing directory is reported with its site',
    fn: () => {
      // The refusal itself. `stability-checker.js` was exercised for two releases by 19 callers
      // and never refused anything, because the only tree it ran against was the one where its
      // criterion was unreachable. This case gives the rule a tree where it must fire.
      const base = fixtureTree({
        'gate.mjs': criterionSource('includes', 'e2e/opencode/specs/'),
        'present/keep.txt': 'x\n',
      });

      const problems = problemsIn(['gate.mjs'], base);
      assertEq(problems.length, 1, `expected exactly one problem, got: ${problems.join(' | ')}`);
      assertTrue(problems[0].startsWith('gate.mjs:1 .includes('), `must name the site: ${problems[0]}`);
      assertTrue(problems[0].includes("('e2e/opencode/specs/')"), `and the criterion: ${problems[0]}`);

      assertEq(problemsIn(['ok.mjs'], fixtureTree({
        'ok.mjs': criterionSource('startsWith', 'present/'),
        'present/keep.txt': 'x\n',
      })).length, 0, 'a criterion that resolves is not a problem');
    },
  },

  {
    name: 'VERIFY-004 a directory prefix that resolves to a file is reported',
    fn: () => {
      // The trailing slash is not decoration. A directory prefix whose name resolves to a FILE
      // matches no path that can ever exist, so it is the same恒真 branch as a missing directory —
      // reachable-looking, and unreachable. Asserted as a pair so the slash is shown to be what
      // decides, not the name.
      //
      // Neither literal is written next to its call shape here, and that is the scan's own
      // residual cost being paid rather than exempted: its first run reported this comment
      // alongside the real defect, because it deliberately reads comments too (see the header).
      const files = {
        'as-directory.mjs': criterionSource('startsWith', 'sibling/'),
        'as-file.mjs': criterionSource('endsWith', 'sibling'),
        'sibling': 'not a directory\n',
      };
      const base = fixtureTree(files);

      assertEq(problemsIn(['as-directory.mjs'], base).length, 1, 'a file behind a directory prefix must fail');
      assertEq(problemsIn(['as-file.mjs'], base).length, 0, 'the same name without the slash resolves');
    },
  },

  {
    name: 'VERIFY-004 the criterion reader covers three methods and three quote styles',
    fn: () => {
      // Guards the reader itself. Every assertion above is vacuous if the pattern stops
      // matching, and a pattern is exactly the kind of thing a later edit narrows by accident —
      // `architecture-gate`'s test-runner criterion matched "the file contains a 3-5 digit
      // number" for as long as anyone had looked at it. Compared as a whole serialized set,
      // because mjs has no rename protection and a per-field check would pass on a dropped field.
      const base = fixtureTree({
        'reader.mjs': [
          criterionSource('includes', 'a/single.txt', "'"),
          criterionSource('startsWith', 'b/double/', '"'),
          criterionSource('endsWith', 'c/backtick.txt', '`'),
          `export const notLiteral = (p, name) => p${'.'}includes(name);\n`,
          `export const noSlash = (p) => p${'.'}endsWith('.mjs');\n`,
        ].join(''),
      });

      const read = pathCriteria(['reader.mjs'], base).map(({ line, method, criterion }) => `${line} ${method} ${criterion}`);

      assertEq(
        JSON.stringify(read),
        JSON.stringify(['1 includes a/single.txt', '2 startsWith b/double/', '3 endsWith c/backtick.txt']),
        'the reader must see all three methods, all three quote styles, and nothing else',
      );
    },
  },

  {
    name: 'VERIFY-004 the domain excludes only what the repo root cannot resolve',
    fn: () => {
      // Each exclusion is paired with the repo-relative literal it must NOT swallow. An
      // exclusion stated as "this shape is fine" is how an allowlist starts: `/src/` leaving
      // the domain has to follow from it being absolute, not from it being known.
      const outside = {
        absolute: '/custom/bin',
        parentEscape: '../src/gone',
        scheme: 'http://127.0.0.1:9999/v1',
        interpolated: 'spec/${file}',
        glob: 'tests/e2e/**/*.mjs',
      };
      const inside = {
        absolute: 'custom/bin',
        parentEscape: 'src/gone',
        scheme: '127.0.0.1/v1',
        interpolated: 'spec/file',
        glob: 'tests/e2e/gone.mjs',
      };

      for (const [reason, literal] of Object.entries(outside)) {
        const base = fixtureTree({ 'gate.mjs': criterionSource('includes', literal, '`') });
        assertEq(problemsIn(['gate.mjs'], base).length, 0, `${reason}: '${literal}' is outside the domain`);
      }

      for (const [reason, literal] of Object.entries(inside)) {
        const base = fixtureTree({ 'gate.mjs': criterionSource('includes', literal, '`') });
        assertEq(problemsIn(['gate.mjs'], base).length, 1, `${reason}: '${literal}' is repo-relative and missing`);
      }
    },
  },
];
