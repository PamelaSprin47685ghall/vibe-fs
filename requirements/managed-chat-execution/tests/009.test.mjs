import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const chatExecution = await import('../../../dist/Execution/Session/ChatExecution/Surface.js')

const fixture = readFileSync(
  new URL('./fixtures/chat-execution-v1.json', import.meta.url),
  'utf8',
).trim()
const keyWire = {
  SessionId: ['SessionId', 'ses-chat-fixture'],
  PhysicalUserMessageId: ['PhysicalUserMessageId', 'msg-chat-fixture'],
}
const factWire = (factCase, payload) => JSON.stringify(['Agent', ['ChatExecution', [factCase, payload]]])
const acceptedEvidence = JSON.parse(fixture)[1][1][1].Evidence
const startedEvidence = {
  Accepted: acceptedEvidence,
  ProviderRun: ['ProviderRunIdentity', 'provider-chat-fixture'],
  RequestKind: 'WorkMain',
  ProjectionChoice: 'UseCommittedEpoch',
}
const started = factWire('ProviderStarted', {
  Evidence: startedEvidence,
  Key: keyWire,
  SchemaVersion: 1,
})
const terminal = factWire('Terminal', {
  Disposition: 'Completed',
  Evidence: ['AfterProviderStart', startedEvidence],
  Key: keyWire,
  SchemaVersion: 1,
})
const canonicalize = (wire) => {
  const result = chatExecution.canonicalize(wire)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[managed-chat-execution-009] durable execution fact round-trip excludes process-local artifacts', () => {
  const history = [canonicalize(fixture), canonicalize(started), canonicalize(terminal)]

  for (const line of history) {
    assert.doesNotMatch(
      line,
      /"[^"]*(?:lease|handle|binding|waiter|callback|queue|cancellationToken|subscription)[^"]*"\s*:/i,
    )
  }
})

const scenario = fileURLToPath(new URL('./support/process-restart-scenario.mjs', import.meta.url))

const runChild = (mode, workspace, marker) =>
  spawnSync(process.execPath, [scenario, mode, workspace, marker], {
    cwd: process.cwd(),
    encoding: 'utf8',
    env: process.env,
  })

const readMarker = (path) => JSON.parse(readFileSync(path, 'utf8'))

integrationTest('WHAT[managed-chat-execution-009] abrupt process exit retains exact Accepted and empties local admission capacity', () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-chat-crash-'))
  const beforeMarker = join(workspace, 'before-crash.json')
  const afterMarker = join(workspace, 'after-reopen.json')

  try {
    execFileSync('git', ['init', '--quiet', workspace])
    const crashed = runChild('crash-after-accepted', workspace, beforeMarker)
    assert.equal(crashed.status, 86, crashed.stderr || crashed.stdout)
    assert.deepEqual(readMarker(beforeMarker), {
      status: { accepted: true, providerStarted: false, terminal: false },
      bindingCount: 1,
      decoy: {
        status: { accepted: false, providerStarted: false, terminal: false },
        bindingCount: 0,
      },
      exactCapacity: { token: true, custody: true, execution: true, waiter: false, owner: true },
      capacityCounts: {
        ledgerEntries: 1,
        tokens: 1,
        custodies: 1,
        executions: 1,
        waiters: 0,
        owners: 1,
        lineage: 0,
        active: 0,
      },
    })

    const reopened = runChild('reopen-after-crash', workspace, afterMarker)
    assert.equal(reopened.status, 0, reopened.stderr || reopened.stdout)
    assert.deepEqual(readMarker(afterMarker), {
      status: { accepted: true, providerStarted: false, terminal: false },
      bindingCount: 0,
      decoy: {
        status: { accepted: false, providerStarted: false, terminal: false },
        bindingCount: 0,
      },
      exactCapacity: { token: false, custody: false, execution: false, waiter: false, owner: false },
      capacityCounts: {
        ledgerEntries: 0,
        tokens: 0,
        custodies: 0,
        executions: 0,
        waiters: 0,
        owners: 0,
        lineage: 0,
        active: 0,
      },
    })
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})
