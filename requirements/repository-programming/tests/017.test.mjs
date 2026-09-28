import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, failureCode } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parallel-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const runWorkflow = async (dir, program) => ({
  outcome: await run(dir, 'Engineer', 'en', program, 2000, Date.now() + 60_000, 1 << 20, null),
})

const succeeded = (outcome) => caseName(outcome) === 'Succeeded'

const failureText = (outcome) => String(failureCode(outcome) ?? 'workflow failed')

test.todo('WHAT[repository-programming-017] concurrent calls in one actual assistant message execute in deterministic Host order')

test('WHAT[repository-programming-017] JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'step0', 'utf8')
    const first = await runWorkflow(
      dir,
      `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    this.rewrite('a.txt', 'step1:' + view.text());
    return { after: view.text() };
  }
}`,
    )
    assert.equal(succeeded(first.outcome), true, failureText(first.outcome))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'step1:step0')

    const second = await runWorkflow(
      dir,
      `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    this.rewrite('a.txt', view.text() + ':step2');
    return { before: view.text() };
  }
}`,
    )
    assert.equal(succeeded(second.outcome), true, failureText(second.outcome))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'step1:step0:step2')
  } finally {
    cleanup()
  }
})
