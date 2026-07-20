/**
 * full.js — OpenCode full E2E suite runner entry.
 *
 * Runs all canary, lifecycle, recovery, and concurrency specs.
 *
 * Usage: node e2e/opencode/specs/full.js
 */

import { runScenario } from "../harness/scenario.js";
import basicTests from "./p0-canary-tests-basics.js";
import ptyTests from "./p0-canary-tests-pty.js";
import advancedTests from "./p0-canary-tests-advanced.js";
import fallbackTests from "./p0-canary-tests-fallback.js";
import fuzzyExecutorTests from "./p0-canary-tests-fuzzy-executor.js";
import nudgeSubTests from "./p0-canary-tests-nudge-sub.js";
import gateTests from "./p0-canary-tests-gate.js";
import webTests from "./p0-canary-tests-web.js";

import lifecycleTests from "./p0-canary-tests-lifecycle.js";
import lifecycleExtraTests from "./p0-canary-tests-lifecycle-extra.js";
import recoveryTests from "./p0-canary-tests-recovery.js";

const common = {
  plugin: true,
  timeoutMs: 120000,
  allowSynthetic: true,
  allowTitleGen: true,
};

const webTestsNormal = webTests.filter(
  (t) =>
    t.name !== "OC-WEB-012 MCP process failure = child + resources cleaned",
);
const webTestsFail = webTests.filter(
  (t) =>
    t.name === "OC-WEB-012 MCP process failure = child + resources cleaned",
);

// 1. Canary + Lifecycle suites
const exitCode1 = await runScenario({ ...common, contextLimit: 20000 }, [
  ...basicTests,
  ...ptyTests,
  ...advancedTests,
  ...fallbackTests,
  ...gateTests,
  ...webTestsNormal,
  ...lifecycleTests,
]);

// 2. Nudge + Sub-agent scenarios
const exitCode2 = await runScenario(
  { ...common, contextLimit: 100000 },
  nudgeSubTests,
);

// 3. Fuzzy Executor with custom workspace
const exitCode3 = await runScenario(
  {
    ...common,
    contextLimit: 20000,
    project: {
      "page_file_1.txt": "1\n",
      "page_file_2.txt": "2\n",
      "page_file_3.txt": "3\n",
      "page_file_4.txt": "4\n",
      "exhaust_file_1.txt": "1\n",
      "exhaust_file_2.txt": "2\n",
      "exhaust_file_3.txt": "3\n",
      "cleanup_file_1.txt": "1\n",
      "cleanup_file_2.txt": "2\n",
      "cleanup_file_3.txt": "3\n",
      "multi_pat_file_a.txt": "content_pat_x\n",
      "multi_pat_file_b.txt": "content_pat_y\n",
      "work_cwd_marker.txt": "cwd-correct\n",
    },
  },
  fuzzyExecutorTests,
);

// 4. MCP Browser Failure check
const exitCode4 = await runScenario(
  {
    ...common,
    contextLimit: 20000,
    extraEnv: {
      STEALTH_BROWSER_MCP_FAIL: "true",
    },
  },
  webTestsFail,
);

// 5. Extra Lifecycle & Concurrency (separate run due to unique session contexts or cleanup patterns)
const exitCode5 = await runScenario(
  { ...common, contextLimit: 20000 },
  lifecycleExtraTests,
);

// 6. Recovery & Event Sourcing suites
const exitCode6 = await runScenario(
  { ...common, contextLimit: 20000 },
  recoveryTests,
);

process.exit(
  exitCode1 || exitCode2 || exitCode3 || exitCode4 || exitCode5 || exitCode6,
);
