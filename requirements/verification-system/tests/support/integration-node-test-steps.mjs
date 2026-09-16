import path from 'node:path'

import { PROJECT_CHECK_TIMEOUT_MS } from '../e2e/support/time-budget.js'

/**
 * The wired node:test integration steps — the single source of truth for which
 * non-child-owned integration tests the entry supervises. Shared by
 * `requirements/verification-system/tests/integration/run.mjs` (which executes
 * them) and the entry-coverage behavior test (which proves the wired set covers
 * every discovered integration test), so the two cannot drift.
 *
 * VERIFICATION-SYSTEM-009: the wired set is declared once here; the parent entry
 * and the coverage test both read it.
 *
 * A step may declare `perTestTimeoutMs` when one of its tests legitimately cannot
 * answer inside the integration default. The entry derives both the child's
 * node:test bound and its verdict-silence window from that one number, so a step
 * cannot claim headroom for the dog while telling node:test a shorter story.
 *
 * @param {string} root repository root (absolute)
 * @returns {{ label: string, files: string[], perTestTimeoutMs?: number, releaseOnly?: boolean }[]}
 */
export function integrationNodeTestSteps(root) {
  return [
    // 1. requirements-adapter: 适配层与插件契约、工作树与持久化等日常测试
    {
      label: 'requirements-adapter',
      files: [
        path.join(root, 'requirements/cognitive-environment/tests/integration/001.test.mjs'),
        path.join(root, 'requirements/cognitive-environment/tests/integration/003.test.mjs'),
        path.join(root, 'requirements/cognitive-environment/tests/integration/004.test.mjs'),
        path.join(root, 'requirements/cognitive-environment/tests/integration/005.test.mjs'),
        path.join(root, 'requirements/behavior-diagnosis/tests/integration/001.test.mjs'),
        path.join(root, 'requirements/behavior-diagnosis/tests/integration/002.test.mjs'),
        path.join(root, 'requirements/behavior-diagnosis/tests/integration/004.test.mjs'),
        path.join(root, 'requirements/behavior-diagnosis/tests/integration/005.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/001.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/006.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/009.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/010.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/011.test.mjs'),
        path.join(root, 'requirements/change-integration/tests/integration/002.test.mjs'),
        path.join(root, 'requirements/change-integration/tests/integration/005.test.mjs'),
        path.join(root, 'requirements/change-integration/tests/integration/008.test.mjs'),
        path.join(root, 'requirements/repository-programming/tests/integration/plugin/020.test.mjs'),
        path.join(root, 'requirements/speculative-investigation/tests/integration/strength/008.test.mjs'),
        path.join(root, 'requirements/managed-chat-execution/tests/integration/009.test.mjs'),
        path.join(root, 'requirements/durable-events/tests/integration/003.test.mjs'),
        path.join(root, 'requirements/durable-events/tests/integration/009.test.mjs'),
        path.join(root, 'requirements/durable-convergence/tests/integration/008.test.mjs'),
        path.join(root, 'requirements/durable-convergence/tests/integration/009.test.mjs'),
      ],
    },
    // 2. compiler-canary: 编译边界与影响分析 CLI，耗时较长（releaseOnly: true）
    {
      label: 'compiler-canary',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/011.test.mjs'),
        path.join(root, 'requirements/structured-workflow/tests/integration/012.test.mjs'),
      ],
      perTestTimeoutMs: PROJECT_CHECK_TIMEOUT_MS,
      releaseOnly: true,
    },
    // 3. repository-envelope: 仓库退化守卫 envelope（releaseOnly: true）
    {
      label: 'repository-envelope',
      files: [
        path.join(root, 'requirements/degeneration-guard/tests/integration/004.test.mjs'),
      ],
      releaseOnly: true,
    },
  ]
}

export function selectIntegrationSteps(root, { releaseOnly = false } = {}) {
  return integrationNodeTestSteps(root).filter((step) => releaseOnly || !step.releaseOnly)
}
