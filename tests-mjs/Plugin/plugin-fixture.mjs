// tests-mjs/Plugin/plugin-fixture.mjs — shared layer-2 plugin fixture (VERIFY-008).
//
// Deliberately NOT named `*.test.mjs`. `tests-mjs/runner.mjs:98` discovers tests with
// `walk('tests-mjs', ['.test.mjs'])`, so a helper carrying that suffix would be run as
// a test file (zero tests inside it, but its top-level import cost paid twice).
// `scripts/architecture-gate.mjs` scans `['.mjs']`, so this file is still gated.

import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const { initSpikePlugin } = await import('../../build/next/OpenCode/SpikePlugin.js')

/**
 * A plugin instance over a throwaway Git repo.
 *
 * The journal lives under the Git common directory (PERSIST-006), so `git init` is
 * what makes the runtime addressable at all. `events.listen` is the smallest port
 * that satisfies the signal source; no scenario, no HTTP, no mock provider.
 *
 * Measured cost per call: 275-331ms wall, almost entirely `git init` plus
 * `initSpikePlugin`. Callers under the 1000ms per-test bound (`runner.mjs:27`) must
 * count calls, not assertions.
 */
export const withPlugin = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-'))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const hooks = await initSpikePlugin({
      client: {},
      directory,
      events: { listen: () => () => {} },
    })
    await body(hooks, directory)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}
