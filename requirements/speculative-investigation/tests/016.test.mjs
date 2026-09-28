import { spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { OPENCODE_BIN } from '../../verification-system/tests/e2e/support/process-host-utils.js'

{
const { default: assert } = await import('node:assert/strict')

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../..')
const runnerPath = path.join(here, '../../host-boundary/tests/support/run-readonly-delegation-schema-canary.mjs')

const builtins = ['read', 'glob', 'grep', 'edit', 'write']

const checkOpencodeExecutable = () => {
  if (process.env.OPENCODE_BIN && existsSync(process.env.OPENCODE_BIN)) return true
  const localBin = path.join(repoRoot, 'node_modules/.bin/opencode')
  if (existsSync(localBin)) return true
  return existsSync(OPENCODE_BIN)
}

/// WHAT[speculative-investigation-013]: with a Predictor model configured, the
/// plugin decorates the definitions the real Host hands it. A Host built-in
/// tool carries an Effect argument schema and no JSON schema, so the protocol
/// must be published through the provider-visible JSON schema the plugin
/// renders itself — and the built-in must still execute with its original
/// arguments, with the wire history keeping the call the model actually made.
integrationTest(
  'WHAT[speculative-investigation-013] SPEC_INV_013_configured_predictor_decorates_builtin_tools_on_real_host',
  async (t) => {
    if (!checkOpencodeExecutable()) {
      return t.skip(`OpenCode binary not executable at ${OPENCODE_BIN}; requires a real OpenCode host`)
    }

    const launched = spawnSync(process.execPath, [runnerPath], {
      cwd: repoRoot,
      encoding: 'utf8',
      timeout: 180000,
      env: { ...process.env },
    })

    if (launched.status !== 0) {
      console.error(launched.stdout)
      console.error(launched.stderr)
    }
    assert.equal(launched.status, 0, 'the readonly delegation schema canary must run to completion')

    const marker = 'READONLY_DELEGATION_SCHEMA_CANARY '
    const line = launched.stdout
      .split('\n')
      .find((entry) => entry.startsWith(marker))

    assert.ok(line, `the canary must report a summary line; stdout was: ${launched.stdout.slice(-2000)}`)

    const summary = JSON.parse(line.slice(marker.length))

    for (const tool of builtins) {
      assert.ok(
        summary.visibleTools.includes(tool),
        `built-in ${tool} must be visible on the provider wire; observed ${summary.visibleTools.join(', ')}`,
      )
    }

    for (const [name, view] of Object.entries(summary.tools)) {
      assert.ok(
        view.required.includes('delegate_readonly_rounds'),
        `${name} must require the readonly delegation budget on the provider wire`,
      )
      assert.equal(
        view.required.filter((entry) => entry === 'delegate_readonly_rounds').length,
        1,
        `${name} must require the budget exactly once`,
      )
      assert.ok(
        view.properties.includes('self_note'),
        `${name} must expose the optional self_note on the provider wire`,
      )
      assert.ok(
        !view.required.includes('self_note'),
        `${name} must not require self_note`,
      )
      assert.equal(view.budget?.type, 'integer', `${name} budget must be an integer`)
      assert.equal(view.budget?.minimum, 0, `${name} budget minimum must be 0`)
      assert.equal(view.budget?.maximum, 2147483647, `${name} budget maximum must be the int range`)
      assert.equal(view.note?.type, 'string', `${name} self_note must be a string`)
    }

    // Decoration extends the schema; it never replaces the tool's own contract.
    assert.ok(summary.tools.read.required.includes('filePath'), 'read must still require filePath')
    assert.ok(summary.tools.write.required.includes('content'), 'write must still require content')
    assert.ok(summary.tools.edit.required.includes('oldString'), 'edit must still require oldString')
    assert.ok(summary.tools.grep.required.includes('pattern'), 'grep must still require pattern')
    assert.ok(
      summary.tools.read.description.includes('delegate_readonly_rounds on every tool call'),
      'built-in descriptions must carry the collaboration prose',
    )
    assert.ok(
      summary.tools.read.description.includes('companion'),
      'the collaboration prose must name the companion, not a cheaper model',
    )

    // The decorated call really executed with the model's own arguments, and the
    // provider-wire history kept them.
    assert.equal(summary.followUpObserved, true, 'the built-in tool call must settle into a follow-up request')
    assert.equal(summary.historicalArguments?.filePath, 'canary-sample.txt')
    assert.equal(summary.historicalArguments?.delegate_readonly_rounds, 0)
    assert.equal(summary.historicalArguments?.self_note, 'checking the canary fixture')
    assert.match(
      summary.toolResultPreview ?? '',
      /readonly delegation schema canary/,
      'the built-in tool must execute and return its real result',
    )
  },
)
}
