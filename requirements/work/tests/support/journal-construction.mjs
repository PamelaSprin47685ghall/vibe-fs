import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import * as lifecycle from '../../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

export const verifyFailedConstruction = Database => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-journal-construction-'))
  const execute = Database.prototype.exec
  const close = Database.prototype.close
  const evidence = []
  try {
    for (const [entry, open] of [
      ['work', () => surface.execute(directory, 'work', 'open', { type: 'Open', root: '/repo', sequence: ['S1'], ownerId: 'owner', openingCursor: 0 })],
      ['ephemeral', () => surface.encodeOpening('plan', 'work', '/repo', 0n)],
      ['ownership', () => surface.ownershipBlockersDurable(directory, 'registry', 'root')],
    ]) {
      for (const fault of ['initialization', 'close-before', 'close-after']) {
        let connection
        let attempts = 0
        let closed = 0
        const initializationError = new Error('journal initialization failed')
        const closeError = new Error('native close acknowledgement failed')
        Database.prototype.exec = function (sql) {
          if (!connection && sql.startsWith('PRAGMA busy_timeout=0;')) {
            connection = this
            throw initializationError
          }
          return execute.call(this, sql)
        }
        Database.prototype.close = function () {
          if (this !== connection) return close.call(this)
          attempts++
          if (fault === 'close-before' && attempts === 1) throw closeError
          let wasOpen = true
          try { execute.call(this, 'SELECT 1') } catch { wasOpen = false }
          const result = close.call(this)
          if (wasOpen) {
            assert.throws(() => execute.call(this, 'SELECT 1'))
            closed++
          }
          if (fault === 'close-after' && attempts === 1) throw closeError
          return result
        }
        try {
          const expected = fault === 'initialization' ? initializationError : closeError
          if (entry === 'ephemeral') assert.equal(open(), `REFUSE ${expected.message}`)
          else assert.throws(open, error => error === expected)
          assert.ok(connection, 'the production constructor must receive the native connection')
          assert.throws(() => execute.call(connection, 'SELECT 1'), 'the original native connection must be closed')
          assert.equal(closed, 1, 'there must be one successful native close, including lost acknowledgements')
          evidence.push({ entry, fault, attempts, closed })
        } finally {
          Database.prototype.exec = execute
          Database.prototype.close = close
          try { close.call(connection) } catch {}
        }
      }
    }
    return evidence
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

if (process.argv.includes('--bun')) {
  const { Database } = await import('bun:sqlite')
  console.log(JSON.stringify(verifyFailedConstruction(Database)))
}
