/**
 * gate-testkit.mjs — Executable testkit quality gate runner.
 *
 * Proves environment isolation, strict FIFO, SSE reconnect/event waits,
 * and diagnostics/leak checks using extracted testkit APIs; no fixed sleeps.
 *
 * Run: node testkit/opencode/tests/gate-testkit.mjs
 */

import { cases } from './gate-cases.mjs';
import { arch010Cases } from './gate-arch010-cases.mjs';
import { budgetCases } from './gate-budget-cases.mjs';
import { coldBoundaryCases } from './gate-cold-boundary-cases.mjs';
import { degradationCases } from './gate-degradation-cases.mjs';
import { deliveryCases } from './gate-delivery-cases.mjs';
import { pluginDependencyCase } from './gate-plugin-dependency-case.mjs';
import { forestLibCases } from './gate-forest-lib-cases.mjs';
import { forestCases } from './gate-forest-cases.mjs';
import { mutationCases } from './gate-mutation-cases.mjs';
import { readinessCases } from './gate-readiness-cases.mjs';
import { unitRunnerCases } from './gate-unit-runner-cases.mjs';
import { scenarioRuntimeCases } from './gate-scenario-runtime-cases.mjs';
import { schemaCases } from './gate-schema-cases.mjs';
import { sourceCases } from './gate-source-cases.mjs';
import { pathCriterionCases } from './gate-path-criterion-cases.mjs';
import { singleSourceCases } from './gate-single-source-cases.mjs';
import { projectionCases } from './gate-projection-cases.mjs';
import { runtimeKeyCases } from './gate-runtime-key-cases.mjs';
import { timeoutCases } from './gate-timeout-cases.mjs';

// 有界并发（ARCH-009）：worker pool 每次只放行 GATE_CASE_CONCURRENCY 条用例，失败概率
// 不随机器负载漂移。用例隔离按构造成立——预算覆盖走逐 spawn 的 env（非 process.env 改写）、
// 临时目录逐用例独立、端口 listen(0) 随机、watchdog 子进程各自成组。输出在全部结束后按
// 声明序回放，故并行不改变报告形态，只改变墙钟。
//
// 并发度 8 取 CANARY_MAX_PARALLEL 的实测先例：十五条 canary 各抱一个真实 Host 进程在单机
// 并发 8 已验证可行；gate 用例里最重的 ProcessHost 三例与之同形。并发计数不是时长，不进
// time-budget.js（W1 迁移先例：CANARY_MAX_PARALLEL 居 canary-manifest.js）。
const GATE_CASE_CONCURRENCY = 8;

async function runCase({ name, fn }) {
  const start = Date.now();
  try {
    await fn();
    return { name, ok: true, ms: Date.now() - start };
  } catch (err) {
    return { name, ok: false, ms: Date.now() - start, err };
  }
}

const allCases = [...cases, pluginDependencyCase, ...projectionCases, ...runtimeKeyCases, ...deliveryCases, ...coldBoundaryCases, ...schemaCases, ...scenarioRuntimeCases, ...forestLibCases, ...sourceCases, ...pathCriterionCases, ...singleSourceCases, ...timeoutCases, ...budgetCases, ...readinessCases, ...unitRunnerCases, ...arch010Cases, ...forestCases, ...mutationCases, ...degradationCases];

console.log(`Running testkit/opencode gate tests (${allCases.length} cases, ${GATE_CASE_CONCURRENCY} at a time)...\n`);

const results = new Array(allCases.length);
let next = 0;
const worker = async () => {
  while (next < allCases.length) {
    const index = next++;
    results[index] = await runCase(allCases[index]);
  }
};
await Promise.all(Array.from({ length: GATE_CASE_CONCURRENCY }, worker));

let passed = 0;
let failed = 0;
for (const r of results) {
  if (r.ok) {
    passed++;
    console.log(`  ✓ ${r.name} (${r.ms}ms)`);
  } else {
    failed++;
    console.error(`  ✗ ${r.name}: ${r.err.message}`);
  }
}

console.log(`\n${passed} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
