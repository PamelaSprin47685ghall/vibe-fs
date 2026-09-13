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
 * @returns {{ label: string, files: string[], perTestTimeoutMs?: number }[]}
 */
export function integrationNodeTestSteps(root) {
  return [
    // 1. requirements-adapter: 适配层与插件契约、工作树与持久化等日常测试
    {
      label: 'requirements-adapter',
      files: [
        path.join(root, 'requirements/cognitive-environment/tests/integration/resources/prompts.test.mjs'),
        path.join(root, 'requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/auto-injected-tool.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/bash-honeypot-tool.test.mjs'),
        path.join(root, 'requirements/change-integration/tests/integration/worktree-create.test.mjs'),
        path.join(root, 'requirements/change-integration/tests/integration/branch-fast-forward-adapter.test.mjs'),
        path.join(root, 'requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs'),
        path.join(root, 'requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs'),
        path.join(root, 'requirements/managed-chat-execution/tests/integration/process-restart-canary.test.mjs'),
        path.join(root, 'requirements/durable-events/tests/integration/persist/object-identity.test.mjs'),
        path.join(root, 'requirements/durable-events/tests/integration/persist/leave-unread.test.mjs'),
        path.join(root, 'requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs'),
      ],
    },
    // 2. compiler-canary: 编译边界与影响分析 CLI，耗时较长（releaseOnly: true）
    {
      label: 'compiler-canary',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs'),
        path.join(root, 'requirements/structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs'),
      ],
      perTestTimeoutMs: PROJECT_CHECK_TIMEOUT_MS,
      releaseOnly: true,
    },
    // 3. repository-envelope: 仓库退化守卫 envelope（releaseOnly: true）
    {
      label: 'repository-envelope',
      files: [
        path.join(root, 'requirements/degeneration-guard/tests/integration/loop-envelope-repository.test.mjs'),
      ],
      releaseOnly: true,
    },
  ]
}

export function selectIntegrationSteps(root, { releaseOnly = false } = {}) {
  return integrationNodeTestSteps(root).filter((step) => releaseOnly || !step.releaseOnly)
}
