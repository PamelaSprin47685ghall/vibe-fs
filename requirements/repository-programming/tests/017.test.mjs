import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, render, caseName, failureCode } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { parse as parseToml } from 'smol-toml'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parallel-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const runWorkflow = async (dir, program) => ({
  outcome: await run(dir, 'Engineer', 'en', program, 2000, Date.now() + 60_000, 1 << 20, null),
})

const succeeded = (outcome) => caseName(outcome) === 'Succeeded'

const failureText = (outcome) => String(failureCode(outcome) ?? 'workflow failed')
const observedData = (outcome) => parseToml(render(outcome)).data

// Root cause at the host ingress: the OpenCode/omp host dispatches ONE
// `tool.execute` per tool call, in the order the assistant message carried
// them. `WorkflowSurface.run` / `ToolHost` executes each call as its own
// transaction against the CURRENT workspace snapshot, and the freshness
// (CAS) check rejects a write whose observed original text no longer
// matches. So calls that look "concurrent" inside one assistant turn are
// serialized by the host in message order: each transaction observes the
// snapshot committed by its predecessor, never a stale one, and no update is
// lost.
test('WHAT[repository-programming-017] JS017_concurrent_calls_in_one_assistant_turn_commit_in_deterministic_serial_order_without_lost_update', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'turn0', 'utf8')

    // One assistant turn, two tool calls; the host dispatches them per
    // `tool.execute`, in message order.
    const turn = [
      {
        callID: 'call1',
        program: `class Js extends JsProgram {
  async run() {
    const text = await this.cat('a.txt');
    this.write('a.txt', text + '->call1');
    return { observed: text };
  }
}`,
      },
      {
        callID: 'call2',
        program: `class Js extends JsProgram {
  async run() {
    const text = await this.cat('a.txt');
    this.write('a.txt', text + '->call2');
    return { observed: text };
  }
}`,
      },
    ]

    const observed = []
    for (const call of turn) {
      const outcome = await run(dir, 'Engineer', 'en', call.program, 2000, Date.now() + 60_000, 1 << 20, null)
      assert.equal(succeeded(outcome), true, `${call.callID}: ${failureText(outcome)}`)
      observed.push(observedData(outcome).observed)
    }

    // Deterministic serial order: each transaction saw the committed
    // snapshot of its predecessor, so no update was lost.
    assert.deepEqual(observed, ['turn0', 'turn0->call1'])
    assert.equal(
      readFileSync(join(dir, 'a.txt'), 'utf8'),
      'turn0->call1->call2',
      'both calls in the one assistant turn committed, in order',
    )
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-017] JS018_consecutive_transactions_re_snapshot_committed_state_no_lost_update', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'step0', 'utf8')
    const first = await runWorkflow(
      dir,
      `class Js extends JsProgram {
  async run() {
    const text = await this.cat('a.txt');
    this.write('a.txt', 'step1:' + text);
    return { after: text };
  }
}`,
    )
    assert.equal(succeeded(first.outcome), true, failureText(first.outcome))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'step1:step0')

    const second = await runWorkflow(
      dir,
      `class Js extends JsProgram {
  async run() {
    const text = await this.cat('a.txt');
    this.write('a.txt', text + ':step2');
    return { before: text };
  }
}`,
    )
    assert.equal(succeeded(second.outcome), true, failureText(second.outcome))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'step1:step0:step2')
  } finally {
    cleanup()
  }
})
