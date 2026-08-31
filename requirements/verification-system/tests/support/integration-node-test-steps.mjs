import path from 'node:path'

import { FCS_PROJECT_CHECK_TIMEOUT_MS } from '../e2e/support/time-budget.js'

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
    {
      label: 'resources/prompts.test.mjs (cognitive-environment)',
      files: [path.join(root, 'requirements/cognitive-environment/tests/integration/resources/prompts.test.mjs')],
    },
    {
      label: 'resources/enforcer-rulebook.test.mjs (behavior-diagnosis)',
      files: [path.join(root, 'requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs')],
    },
    {
      label: 'blogger-nudge-plugin-repro.test.mjs (behavior-diagnosis)',
      files: [path.join(root, 'requirements/behavior-diagnosis/tests/integration/blogger-nudge-plugin-repro.test.mjs')],
    },
    {
      label: 'plugin contracts (capability-enforcement)',
      files: [
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/auto-injected-tool.test.mjs'),
        path.join(root, 'requirements/capability-enforcement/tests/integration/plugin/bash-honeypot-tool.test.mjs'),
      ],
    },
    {
      label: 'worktree-create.test.mjs (change-integration)',
      files: [path.join(root, 'requirements/change-integration/tests/integration/worktree-create.test.mjs')],
    },
    {
      label: 'branch-fast-forward-adapter.test.mjs (change-integration)',
      files: [path.join(root, 'requirements/change-integration/tests/integration/branch-fast-forward-adapter.test.mjs')],
    },
    // Both structured-workflow steps below run real `dotnet fsi` F# project checks: an F# project
    // check over the production tree cannot report a verdict inside the integration default, so the
    // budget is declared here rather than raised for every step. Measured lanes: 11s + 110s here,
    // 34s + 107s + 5s next door.
    {
      label: 'owner-dependencies-fcs.test.mjs (structured-workflow)',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/owner-dependencies-fcs.test.mjs'),
      ],
      perTestTimeoutMs: FCS_PROJECT_CHECK_TIMEOUT_MS,
    },
    {
      label: 'workflow-constitution-scanners.test.mjs (structured-workflow)',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/workflow-constitution-scanners.test.mjs'),
      ],
      perTestTimeoutMs: FCS_PROJECT_CHECK_TIMEOUT_MS,
    },
    {
      label: 'plugin/file-mutation-tools.test.mjs (repository-programming)',
      files: [path.join(root, 'requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs')],
    },
    {
      label: 'strength/lifecycle.test.mjs (speculative-investigation)',
      files: [path.join(root, 'requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs')],
    },
    // Persist owns the only durable substrate, and these three files were reachable only by
    // running them by hand — a self-test outside the gate is not a gate. `object-identity` in
    // particular pins our in-process Git object writer against the real binary.
    {
      label: 'persist (durable-events)',
      files: [
        path.join(root, 'requirements/durable-events/tests/integration/persist/object-identity.test.mjs'),
        path.join(root, 'requirements/durable-events/tests/integration/persist/leave-unread.test.mjs'),
      ],
    },
    {
      label: 'persist (durable-convergence)',
      files: [
        path.join(root, 'requirements/durable-convergence/tests/integration/persist/dumb-server.test.mjs'),
      ],
    },
  ]
}
