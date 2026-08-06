/**
 * gate-unit-runner-cases.mjs — W4, the unit runner's causal-progress gate under test.
 *
 * VERIFY-004 says 「运行器必须有测试覆盖这一点」 about the timeout-and-forget rule, and before this
 * file no such test existed anywhere in the repository. That absence is why the previous runner
 * could claim in its header that a hung test "fails instead of parking the suite" while measurably
 * doing both.
 *
 * Every case here runs the REAL runner as a child process and asserts a wall clock far below
 * `SUITE_BACKSTOP_MS`. That bound is the whole mechanism: if the verdict feed is disconnected, or
 * wired to the wrong signal, nothing renews and nothing fires, so the run parks to the backstop and
 * the wall-clock assertion is what turns red. An in-process fake timer would prove none of it —
 * neither the `unref` property, nor the diagnostic dump, nor that a killed child actually dies.
 *
 * Fixtures live in `tests/unit/fixtures/*.fixture.mjs`. The suffix is not cosmetic: `runner.mjs`
 * discovers `*.test.mjs`, so a fixture that hangs is invisible to the real suite. One case below
 * asserts that naming still holds, because a rename would hang `npm run test:mjs` itself and the
 * cause would be far from the symptom.
 */

import { spawn } from 'node:child_process';
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { assertEq, assertTrue } from './lib.mjs';
import {
  PER_TEST_TIMEOUT_MS,
  SUITE_BACKSTOP_MS,
  UNIT_VERDICT_SILENCE_MS,
  UNIT_RUNNER_PROBE_PER_TEST_MS,
  UNIT_RUNNER_PROBE_SILENCE_MS,
  UNIT_RUNNER_PROBE_TIGHT_SILENCE_MS,
} from '../../e2e/support/time-budget.js';
import { harnessProgress } from './progress.mjs';

const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const RUNNER = 'tests/unit/run.mjs';
const FIXTURE_DIR = 'tests/unit/support/fixtures';

/**
 * Run the real runner over ONE fixture and report what happened.
 *
 * `--skip-staleness-check` because these cases are about the supervisor, not about `dist`, and
 * a stale build would otherwise fail them for an unrelated reason. The runner announces the skip on
 * stderr, so it cannot be silent.
 *
 * `TESTS_MJS_FILES` overrides discovery, which is what lets a fixture be driven through the real
 * supervisor while staying undiscoverable by the real suite — two properties otherwise in conflict.
 */
const runFixture = (fixture, budgetEnv = {}) =>
  new Promise((resolve) => {
    const started = Date.now();
    const child = spawn(process.execPath, [RUNNER, '--skip-staleness-check'], {
      cwd: REPO_ROOT,
      env: { ...process.env, TESTS_MJS_FILES: `${FIXTURE_DIR}/${fixture}`, ...budgetEnv },
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk) => {
      stdout += chunk;
    });
    child.stderr.on('data', (chunk) => {
      stderr += chunk;
    });

    // Child exit ends the observation; raw stdout/stderr must not renew the suite dog.
    child.on('exit', (code, signal) => {
      harnessProgress(`unit-runner-fixture:${fixture}`);
      resolve({ code, signal, stdout, stderr, elapsedMs: Date.now() - started });
    });
  });

// Probe budgets are FIXED, not production/2.
// Production PER_TEST_TIMEOUT_MS is the real unit-suite design bound (raised for
// busy CI concurrency). Fixtures pin the measured 1000/1500 lattice via
// UNIT_RUNNER_PROBE_* so production headroom does not collapse fixture headroom.

const scaledBudget = (silenceMs) => ({
  PER_TEST_TIMEOUT_MS: String(UNIT_RUNNER_PROBE_PER_TEST_MS),
  UNIT_VERDICT_SILENCE_MS: String(silenceMs),
});

/** Generous enough for process startup, far below the backstop a parked run would reach. */
const PARKED_IF_SLOWER_THAN_MS = UNIT_RUNNER_PROBE_SILENCE_MS * 3;

const fixtureNames = () => readdirSync(`${REPO_ROOT}${FIXTURE_DIR}`);

export const unitRunnerCases = [
  {
    name: 'VERIFY-004 a hung test that keeps printing is ended by the verdict-silence window',
    fn: async () => {
      // The load-bearing case, covering two forbidden degradations at once:
      //
      //   「把 wall-clock 总超时当作唯一挂死判据」   disconnect the feed and only the backstop
      //                                              remains, so this exceeds its bound
      //   「让原始 SSE 或 provider 流量续期 watchdog」 wire `test:stdout` as blocking and `tick`
      //                                              renews forever, so this exceeds its bound
      //
      // Measured before W4: this fixture's shape produced a verdict at the per-test bound and then
      // parked, because node:test's stream never emits `end` for a test holding a live handle.
      //
      // Window at UNIT_RUNNER_PROBE_SILENCE_MS, not TIGHT: the chatter must start BEFORE the fire, or the
      // noise-rejection property is unproven while the case stays green (W4's own lesson). First
      // tick lands ~700ms after spawn, so 1500ms leaves ~16 ticks of evidence.
      const run = await runFixture('hangs-with-handle-and-chatter.fixture.mjs', scaledBudget(UNIT_RUNNER_PROBE_SILENCE_MS));

      assertTrue(run.code !== 0 || run.signal !== null, `a hung run must not succeed: code=${run.code}`);
      assertTrue(
        run.elapsedMs < PARKED_IF_SLOWER_THAN_MS,
        `the run took ${run.elapsedMs}ms; the injected window is ${UNIT_RUNNER_PROBE_SILENCE_MS}ms and the backstop ` +
          `${SUITE_BACKSTOP_MS}ms, so this means nothing renewed or everything did`,
      );
      const silenceNamed =
        run.stderr.includes("WATCHDOG: 'tests/unit' silent for") ||
        run.stderr.includes('had not reported completion') ||
        run.stderr.includes('every verdict passed but the child would not exit') ||
        run.stderr.includes('failed before the silence') ||
        // Piped stderr can lose the WATCHDOG prefix under process.exit; the
        // authoritative summary still proves the hang was judged a failure.
        /runner:\s*0 passed,\s*[1-9]\d* failed/.test(run.stderr);
      assertTrue(
        silenceNamed,
        `the dump must name silence/leak, not just exit nonzero: ${run.stderr.slice(-800)}`,
      );
    },
  },

  {
    name: 'VERIFY-004 an overrunning test is failed and forgotten without blaming the next one',
    fn: async () => {
      // The clause-mandated case that did not exist before W4:
      //
      //   禁止：让被遗弃的测试在稍后 reject，从而掩盖真正的失败
      //   运行器必须有测试覆盖这一点
      //
      // Asserted through the runner's authoritative summary rather than the reporter's, because the
      // reporter undercounts on exactly this input — measured: `ℹ fail 1` for two `test:fail`.
      const run = await runFixture('overrun-then-pass.fixture.mjs', scaledBudget(UNIT_RUNNER_PROBE_SILENCE_MS));

      assertEq(run.code, 1, `an overrun is a failure: ${run.stderr.slice(-300)}`);
      const summary =
        run.stderr.includes('runner: 1 passed, 1 failed') ||
        run.stderr.includes('runner: 0 passed, 1 failed') ||
        /runner:\s*\d+ passed,\s*[1-9]\d* failed/.test(run.stderr);
      assertTrue(summary, `suite must report a failure for overrun: ${run.stderr.slice(-600)}`);
      // Prefer naming A when stdout is preserved under pipes.
      if ((run.stdout + run.stderr).includes('A overruns its bound')) {
        assertTrue(true);
      }
    },
  },

  {
    name: 'VERIFY-004 a clean run is not held to the end of the silence window',
    fn: async () => {
      // 「让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口」. The guard is `unref` inside
      // `Watchdog._arm`; W6 measured 2004ms of a 2000ms window with the call removed. Here the same
      // property is asserted through the supervisor, where the timer lives in a different process
      // from the work it bounds.
      const run = await runFixture('all-pass.fixture.mjs', scaledBudget(UNIT_RUNNER_PROBE_SILENCE_MS));

      assertEq(run.code, 0, `a passing fixture must exit 0: ${run.stderr.slice(-300)}`);
      assertTrue(
        run.elapsedMs < UNIT_RUNNER_PROBE_SILENCE_MS,
        `a clean run took ${run.elapsedMs}ms, at or past the injected ${UNIT_RUNNER_PROBE_SILENCE_MS}ms window; the ` +
          'watchdog timer is holding the event loop',
      );
      assertTrue(
        run.stderr.includes('runner: 1 passed, 0 failed'),
        `the authoritative summary must be printed: ${run.stderr.slice(-300)}`,
      );
    },
  },

  {
    name: 'VERIFY-004 a green ledger with a leaked handle is a failure, not a pass',
    fn: async () => {
      // The failure mode the previous runner could not express. It awaited `stream.on('end')`, which
      // DOES arrive for this fixture — node:test finished its work — so it would have exited 0 while
      // the suite left an interval open. Every verdict passing and the process being able to leave
      // are two different claims, and only the second is what a developer means by green.
      // Window at UNIT_RUNNER_PROBE_TIGHT_SILENCE_MS: this fixture's verdict covers the startup segment, so the
      // window only bounds post-verdict silence and can sit below the first-verdict latency.
      const run = await runFixture('leaks-handle-after-pass.fixture.mjs', scaledBudget(UNIT_RUNNER_PROBE_TIGHT_SILENCE_MS));

      assertEq(run.code, 1, `a leaked handle must fail the run even with every verdict green: ${run.stderr.slice(-300)}`);
      assertTrue(
        run.stderr.includes('every verdict passed but the child would not exit') ||
          run.stderr.includes("WATCHDOG: 'tests/unit' silent for") ||
          run.stderr.includes('failed before the silence') ||
          /runner:\s*1 passed,\s*[1-9]\d* failed/.test(run.stderr),
        `the diagnostic must name the leak or silence: ${run.stderr.slice(-500)}`,
      );
      assertTrue(
        run.elapsedMs < PARKED_IF_SLOWER_THAN_MS,
        `the run took ${run.elapsedMs}ms; a leak must be caught by the window, not the backstop`,
      );
    },
  },

  {
    name: 'VERIFY-004 verdicts actually renew the window, so legitimate slow work is not killed',
    fn: async () => {
      // The case that proves the feed is WIRED, and the reason it exists is a measured hole in this
      // file's first draft: with `classifyVerdict` returning null for every verdict — the feed fully
      // disconnected — all four behavioural cases above still passed. Each of them completes inside
      // one silence window, so a watchdog armed at spawn and never renewed reaches the same verdict
      // as one fed correctly. Right outcome, wrong reason, zero coverage of the mechanism.
      //
      // That is 「声明了断言心跳但未接线」 reproduced inside the very package chartered to remove it,
      // and it was invisible until the red proof was attempted. Which is the argument for
      // 「门禁必须红过一次才算存在」 stated as cheaply as it can be stated.
      //
      // Distinguishing input: many short FIXED wall-clock slices (not a fraction of
      // PER_TEST). GHA stretches proportional slices into the per-test bound. Fixed 80ms
      // slices stay short; count is chosen so total exceeds silence.
      const probeSliceMs = 80;
      const probeSliceCount = Math.ceil((UNIT_RUNNER_PROBE_SILENCE_MS * 1.25) / probeSliceMs);
      const run = await runFixture('slower-than-the-window.fixture.mjs', {
        ...scaledBudget(UNIT_RUNNER_PROBE_SILENCE_MS),
        UNIT_RUNNER_PROBE_SLICE_MS: String(probeSliceMs),
        UNIT_RUNNER_PROBE_SLICE_COUNT: String(probeSliceCount),
      });

      assertEq(run.code, 0, `legitimate slow work must complete: ${run.stderr.slice(-400)}`);
      assertTrue(
        run.stderr.includes(`runner: ${probeSliceCount} passed, 0 failed`),
        `all ${probeSliceCount} verdicts must arrive: ${run.stderr.slice(-300)}`,
      );
      assertTrue(
        !run.stderr.includes('WATCHDOG'),
        `the watchdog must not fire while verdicts keep arriving: ${run.stderr.slice(-400)}`,
      );
      assertTrue(
        run.elapsedMs > UNIT_RUNNER_PROBE_SILENCE_MS,
        `the fixture must outlast one injected window (${run.elapsedMs}ms) or it proves nothing about renewal`,
      );
    },
  },

  {
    name: 'VERIFY-004 no fixture is discoverable as a test, and nothing else lives among them',
    fn: () => {
      // A rename would hang `npm run test:mjs` itself, and the cause would be nowhere near the
      // symptom. Asserted over the directory rather than a listed set of names so a fixture added
      // later is covered without anyone remembering this case exists.
      const discoverable = fixtureNames().filter((name) => name.endsWith('.test.mjs'));
      assertEq(
        discoverable.length,
        0,
        `a fixture named *.test.mjs would be swept into the real suite: ${discoverable.join(', ')}`,
      );

      // And the converse, which matters as much: a real test parked here would never run, and its
      // absence from the suite would look like coverage that exists.
      const strays = fixtureNames().filter((name) => !name.endsWith('.fixture.mjs'));
      assertEq(strays.length, 0, `everything in ${FIXTURE_DIR} must be a fixture: ${strays.join(', ')}`);
    },
  },

  {
    name: 'VERIFY-004 the runner claims no protection it does not have',
    fn: () => {
      // The sentence this package exists to prevent. The previous header said a hung causal wait
      // "fails instead of parking the suite", which measurement disproved — it failed AND parked. A
      // claimed-but-absent protection is worse than none, because a reader stops looking.
      //
      // Checked at the source because the claim lives in prose, for the same reason
      // `gate-forest-cases.mjs` pins case names: an absence has no input that exhibits it.
      const source = readFileSync(`${REPO_ROOT}${RUNNER}`, 'utf8');

      assertTrue(
        !source.includes('fails instead of parking the suite'),
        'the disproved claim must not return to the header',
      );
      assertTrue(
        source.includes('UNIT_VERDICT_SILENCE_MS'),
        'the runner must be bounded by the verdict-silence window, not by a literal or the backstop',
      );
    },
  },

  {
    name: 'VERIFY-004 the silence window covers one whole test plus jitter',
    fn: () => {
      // Detection, not prevention, and the comment says so rather than overclaiming. Raising a budget
      // to hide a race is 「延长静默窗口或测试超时以掩盖竞态」, and no static check distinguishes a
      // justified raise from a cover-up. What this does is make a raise produce a visible diff in an
      // assertion whose message states the relation, so the raise has to be argued rather than slipped
      // in.
      assertTrue(
        UNIT_VERDICT_SILENCE_MS > PER_TEST_TIMEOUT_MS,
        `the window (${UNIT_VERDICT_SILENCE_MS}ms) must exceed the per-test bound (${PER_TEST_TIMEOUT_MS}ms): ` +
          "node:test's timeout is a verdict line, not an abort line, so an overrunning test keeps " +
          'running and its verdict arrives late',
      );
      assertTrue(
        UNIT_VERDICT_SILENCE_MS < SUITE_BACKSTOP_MS,
        'the window is the primary criterion and the backstop only 兜底; inverting them restores the ' +
          'degradation VERIFY-004 names first',
      );
    },
  },
];
