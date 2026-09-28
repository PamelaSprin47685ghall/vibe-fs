import { readdirSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { default: test } = await import('node:test')
const { withExecutablePlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')

const root = resolve(import.meta.dirname, '../../..')

// WHAT[crash-reconciliation-018]: a restart normalizes itself — the interrupted
// child run is settled, process-local execution bindings are rebuilt from the
// durable handles, and durable children are re-enlisted — so there is no explicit
// resume command any more. Nothing may be replayed on the user's behalf, and the
// interrupted tool stays failed in visible history.
test('WHAT[crash-reconciliation-018] CRASH_018_no_explicit_resume_command_is_registered', async () => {
  await withExecutablePlugin(async (hooks) => {
    const config = {}

    hooks.config(config)

    assert.equal(config.command, undefined, 'plugin load must not register any command, including /continue')
  })
})

test('WHAT[crash-reconciliation-018] CRASH_018_explicit_resume_traces_are_gone_from_the_source_tree', () => {
  const walk = (directory) =>
    readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
      const path = resolve(directory, entry.name)
      if (entry.isDirectory()) {
        if (entry.name === '.fable-build' || entry.name === 'node_modules') return []
        return walk(path)
      }
      return [path]
    })

  const offenders = walk(resolve(root, 'src/Wanxiangshu'))
    .filter((path) => /(ExplicitResume|ExplicitSessionResume|SessionResumePort)/.test(path))
    .map((path) => path.slice(root.length + 1))

  assert.deepEqual(offenders, [], 'no explicit-resume module may remain in the compiled source tree')
})
}
