import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { parse } from 'smol-toml'

test('WHAT[repository-programming-060] the executed SDK uses this members and exposes no global js or nested api', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-member-vocabulary-'))
  try {
    const outcome = await run(directory, 'DevOps', 'en', `class Js extends JsProgram {
      async run() {
        return { global: typeof js, nested: typeof this._api.js, call: typeof this.call, callable: typeof this.Callable, write: typeof this.write };
      }
    }`, 2000, Date.now() + 60000, 1 << 20, null)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(parse(render(outcome)).data, { global: 'undefined', nested: 'undefined', call: 'function', callable: 'function', write: 'function' })
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})
