/**
 * scenario.js — Per-scenario isolation orchestration for OpenCode E2E.
 *
 * One scenario = one temp HOME + one temp XDG + one workspace
 *               + one StrictMockProvider + one opencode serve process.
 *
 * No shared-host mode. The runner auto-calls expectSatisfied() after
 * each test fn returns and aborts the test as failed if cleanup errors.
 * Cleanup errors are test errors, not warnings.
 *
 * Setup runs provider start, project-file write, git init, and host
 * start in parallel where dependencies allow (see scenario-parallel.js).
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { FsOracle, HttpClient, getSessionId } from './scenario-http.js';
import { runScenarioTests } from './scenario-runner.js';
import { setupScenarioParallel, Scenario } from './scenario-parallel.js';

function tmpDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'oc-e2e-'));
}

export { Scenario };

export async function setupScenario(opts = {}) {
  return setupScenarioParallel(opts, tmpDir);
}

export async function teardownScenario(scenario, { keepOnFailure = false } = {}) {
  if (scenario._tornDown) return;
  scenario._tornDown = true;
  scenario.watchdog?.stop();
  const errors = [];

  // 1. 停止继续发送 mock 响应
  try {
    if (scenario.provider) {
      scenario.provider.stopMocking();
    }
  } catch (e) {
    errors.push(`stopMocking: ${e.message}`);
  }

  // 2. abort SSE reader
  // 3. 等待 SSE reader 正常退出
  try {
    if (scenario.events) {
      await scenario.events.close();
    }
  } catch (e) {
    errors.push(`EventProbe: ${e.message}`);
  }

  // 4. 请求或触发 session abort
  for (const sid of scenario.sessionIds) {
    try { await scenario.client.abort(sid); } catch {}
  }

  // 5. 等待 session idle / terminated
  for (const sid of scenario.sessionIds) {
    try {
      await scenario.client.waitForSessionIdle(sid, 2000);
    } catch {}
  }

  // 6. kill 所有 PTY
  // 7. SIGTERM opencode
  // 8. 最多等待短 deadline
  // 9. 未退出则 SIGKILL
  // 10. await child exit
  // 12. 检查无活跃 socket
  // 13. 检查无已知子进程
  try {
    if (scenario.host) {
      await scenario.host.stop({ assert: true });
    }
  } catch (e) {
    errors.push(`Host: ${e.message}`);
  }

  // 11. 关闭 mock provider
  try {
    if (scenario.provider) {
      await scenario.provider.stop();
    }
  } catch (e) {
    errors.push(`Provider: ${e.message}`);
  }

  // 14. 删除 lock
  if (scenario.host && scenario.host.workDir) {
    const lockPath = path.join(scenario.host.workDir, '.lock');
    if (fs.existsSync(lockPath)) {
      try { fs.unlinkSync(lockPath); } catch {}
    }
  }

  // 15. 删除临时 HOME 和工作区
  if (!keepOnFailure) {
    try {
      fs.rmSync(scenario.scenarioDir, { recursive: true, force: true });
    } catch (e) {
      errors.push(`Cleanup: ${e.message}`);
    }
  }

  if (errors.length > 0) {
    throw new Error(`E2E cleanup failed: ${errors.join('; ')}`);
  }
}

export async function runScenario(opts, tests) {
  return runScenarioTests(opts, tests, setupScenario);
}

export { FsOracle, HttpClient, getSessionId };
