import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { GuestOwner, bounded, absent } from './vibe-fs-mf01-vertical-a-callable-20261021/owner.mjs'

for (const kind of ['class-instance', 'plain-toJSON']) {
  test(`production compatibility requires own command fields from ${kind} without generic JSON projection`, async () => {
    const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-b-command-')))
    let owner
    try {
      owner = await GuestOwner.create(process.execPath, directory, {})
      const command = kind === 'class-instance'
        ? "class Command { constructor() { this.name = 'probe'; this.spawn = 'printf OLD_COMMAND > command.marker'; } } const command = new Command();"
        : "const command = { name: 'probe', spawn: 'printf OLD_COMMAND > command.marker', toJSON() { program.write('toJSON-called', 'changed'); return { different: 1 }; } };"
      await owner.start(`const program = this; ${command}
        class Work {
          get maxDuration() { return 3000; }
          next() { return command; }
          watch() { return false; }
          sanity() { return { nextDone: () => [], watchVeto: () => [] }; }
        }
        return await this.offerCallable(Work);`, 3000)
      const ref = await bounded(owner.nextOffer(), 1000, 'OFFER_TIMEOUT')
      const execution = await owner.callable('construct', ref, { role: 'execution' })
      const result = await owner.callable('callMethod', execution, { method: 'next', context: null }).then(value => ({ value }), error => ({ error: { code: error.code, family: error.family, message: error.message } }))
      console.log(JSON.stringify({ evidence: 'candidate-command-projection', runtime: process.versions.bun ? 'Bun' : process.versions.node, kind, pid: owner.pid, result, records: owner.apis.records }))
      assert.deepEqual(result, { value: { name: 'probe', spawn: 'printf OLD_COMMAND > command.marker' } })
      assert.equal(fs.existsSync(path.join(directory, 'scratch', 'toJSON-called')), false)
    } finally {
      if (owner) {
        const descriptor = owner.replyFd
        const state = await bounded(owner.dispose(), 7000, 'COMPATIBILITY_CLEANUP_TIMEOUT')
        assert.equal(state.phase, 'Closed')
        assert.equal(state.failure, null)
        assert.equal(absent(owner.pid), true)
        assert.equal(owner.accepted.size, 0)
        assert.equal(owner.callablePending.size, 0)
        assert.equal(fs.existsSync(owner.fifoPath), false)
        assert.throws(() => fs.fstatSync(descriptor), { code: 'EBADF' })
        console.log(JSON.stringify({ evidence: 'compatibility-owned-cleanup', kind, pid: owner.pid, state, absent: absent(owner.pid) }))
      }
      fs.rmSync(directory, { recursive: true, force: true })
    }
  })
}
