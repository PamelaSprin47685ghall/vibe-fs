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
    // Structured-workflow steps below run real `dotnet fsi` F# project checks. Each physical check
    // is its own file and sequential step so completion renews the verdict watchdog. Combining
    // several checks in one file hides every leaf verdict behind the file wrapper and turns normal
    // aggregate runtime into false silence. The wider budget remains local to these checks.
    {
      label: 'owner-dependencies-fcs.test.mjs (structured-workflow)',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/owner-dependencies-fcs.test.mjs'),
      ],
      perTestTimeoutMs: FCS_PROJECT_CHECK_TIMEOUT_MS,
    },
    {
      label: 'composition-root-scanner.test.mjs (structured-workflow)',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/composition-root-scanner.test.mjs'),
      ],
      perTestTimeoutMs: FCS_PROJECT_CHECK_TIMEOUT_MS,
    },
    {
      label: 'plugin-decorator-scanners.test.mjs (structured-workflow)',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/plugin-decorator-scanners.test.mjs'),
      ],
      perTestTimeoutMs: FCS_PROJECT_CHECK_TIMEOUT_MS,
    },
    {
      label: 'semantic-decorator-fcs.test.mjs (structured-workflow)',
      files: [
        path.join(root, 'requirements/structured-workflow/tests/integration/semantic-decorator-fcs.test.mjs'),
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
