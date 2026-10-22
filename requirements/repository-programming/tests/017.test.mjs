import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, failureCode } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { parse as parseToml } from 'smol-toml'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parallel-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const runWorkflow = async (dir, program) => ({
  outcome: await run(dir, 'Engineer', 'en', program, 2000, Date.now() + 60_000, 1 << 20, null),
})

const succeeded = (outcome) => caseName(outcome) === 'Succeeded'

const failureText = (outcome) => String(failureCode(outcome) ?? 'workflow failed')

const SESSION = 'ses-017-host-dispatch'
const RUN = 'run-017-host-dispatch'

const appendProgram = (mark) => `class Js extends JsProgram {
  async run() {
    const text = await this.cat('shared.txt');
    this.write('shared.txt', text + '->${mark}');
    return { observed: text };
  }
}`

const parsed = (result) => parseToml(String(result))
const observedOf = (result) => parsed(result).data?.observed

const instrument = (hooks, log) => {
  const original = hooks.tool['todowrite-for-engineer']

  hooks.tool['todowrite-for-engineer'] = {
    ...original,
    execute: async (args, context) => {
      const entry = { callID: context.callID, enteredAt: Date.now(), leftAt: null }
      log.push(entry)

      try {
        return await original.execute(args, context)
      } finally {
        entry.leftAt = Date.now()
      }
    },
  }
}

const overlapped = (log) => {
  if (log.length < 2) return false

  const [first, second] = log

  return (
    first.enteredAt < (second.leftAt ?? Number.MAX_SAFE_INTEGER) &&
    second.enteredAt < (first.leftAt ?? Number.MAX_SAFE_INTEGER)
  )
}

test('WHAT[repository-programming-017] two tool calls of one assistant message commit in a deterministic serial order, the second reading the first commit', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'engineer')

    const root = directory
    writeFileSync(join(root, 'shared.txt'), 'base', 'utf8')

    const log = []
    instrument(hooks, log)

    // Dispatch both calls before either completes; the product owns their order.
    const [first, second] = await Promise.all([
      hooks.tool['todowrite-for-engineer'].execute(
        { program: appendProgram('call1') },
        { sessionID: SESSION, agent: 'engineer', callID: 'call-017-1', messageID: RUN },
      ),
      hooks.tool['todowrite-for-engineer'].execute(
        { program: appendProgram('call2') },
        { sessionID: SESSION, agent: 'engineer', callID: 'call-017-2', messageID: RUN },
      ),
    ])

    assert.equal(log.length, 2, 'both tool calls of the one assistant message must reach the product')
    assert.deepEqual(
      log.map((entry) => entry.callID).sort(),
      ['call-017-1', 'call-017-2'],
      'the two calls of the one assistant message must both be dispatched',
    )

    const concurrency = overlapped(log)
      ? 'the two calls were dispatched concurrently by the host'
      : 'the two calls did not overlap in the product'

    assert.equal(
      parsed(first).code,
      undefined,
      `the first call of the one assistant message must succeed: ${String(first)} (${concurrency})`,
    )
    assert.equal(
      parsed(second).code,
      undefined,
      `the second call of the one assistant message must succeed: ${String(second)} (${concurrency})`,
    )

    assert.deepEqual(
      [observedOf(first), observedOf(second)],
      ['base', 'base->call1'],
      `the second call must read the first call's committed state (${concurrency})`,
    )

    assert.equal(
      readFileSync(join(root, 'shared.txt'), 'utf8'),
      'base->call1->call2',
      `the two calls of the one assistant message must commit in order, without a lost update (${concurrency})`,
    )
  })
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
