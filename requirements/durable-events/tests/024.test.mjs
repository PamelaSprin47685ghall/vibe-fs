import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { existsSync, mkdtempSync, readFileSync, realpathSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { runVerificationToolProbe } from '../../../scripts/lib/verification-tool-probe.mjs'
import * as settlement from '../../../dist/Sphinx/V2/Composition/SettlementSurface.js'
import * as wire from '../../../dist/Sphinx/V2/Wire/Surface.js'
import * as mcp from '../../../dist/Sphinx/V2/Hosts/Mcp/Surface.js'

const child = fileURLToPath(new URL('./support/sphinx-command-settlement-child.mjs', import.meta.url))
const configuration = {
  commandNamespace: 'settlement-owner', createdBy: 'authorized-controller', profileRef: 'sphinx.default@2',
  executionMode: 'delegated', resourceSpecs: [{ name: 'calls', kind: { case: 'consumed', payload: 'calls' }, authorizedLimit: 0 }],
  renderReserve: { calls: 0 },
}

for (const scenario of ['valid', 'malformed-release', 'valid-release', 'not-attempted-controlled', 'no-new-write-controlled']) {
  test(`WHAT[durable-events-024] actual Sphinx Commands.start ${scenario} preserves its original settlement capability and guard`, async t => {
    assert.equal(typeof settlement.create, 'function')
    const root = realpathSync(mkdtempSync(join(tmpdir(), 'sphinx-command-settlement-')))
    const commonDir = join(root, '.git')
    const sourceWriter = randomUUID()
    const sourceFile = join(commonDir, 'wanxiang', 'events', `${sourceWriter}.ndjson`)
    const command = { commandId: 'settlement-' + scenario, goalText: '实际目标\r\nNUL:\u0000；雪 😀 尾部  ',
      constraints: ['preserve the authorized text'], materialRefs: ['material:source'],
      authorizationRef: 'authorized-user', profileRef: configuration.profileRef }
    const controlled = scenario.endsWith('-controlled')
    const malformed = scenario === 'malformed-release'
    const release = scenario.endsWith('-release')
    const env = { ...process.env }
    delete env.NODE_TEST_CONTEXT
    const probe = async (mode, writerId, request) => {
      try {
        return JSON.parse(await runVerificationToolProbe(process.execPath,
          [child, mode, commonDir, writerId, scenario, JSON.stringify(request)],
          { cwd: root, env, signal: t.signal }))
      } catch (error) {
        if (error?.stderr && typeof error.message === 'string') error.message += '\n' + error.stderr
        throw error
      }
    }
    let completed = false
    try {
      const measured = await probe('measure', sourceWriter, { configuration, command })
      assert.notEqual(measured.pid, process.pid)
      const { physical, original, requested } = measured
      const expectedCounts = controlled ? { append: 0, fsync: 0, close: 0, release: 0, injected: 0 }
        : { append: 1, fsync: 1, close: 1, release: 1, injected: release ? 1 : 0 }
      assert.deepEqual(physical.counts, expectedCounts)
      assert.equal(physical.writerCreated, !controlled)
      assert.equal(existsSync(sourceFile), !controlled)
      assert.equal(physical.lockReleased, true)
      assert.equal(physical.openDescriptors, 0)
      assert.equal(physical.syncedDescriptors, 0)
      assert.equal(existsSync(join(commonDir, 'wanxiang.lock')), false)
      assert.equal(controlled ? '' : readFileSync(sourceFile, 'base64'), physical.bytes)
      assert.equal(physical.facts.length, controlled ? 0 : malformed ? 2 : 1)
      assert.equal(original.payload.commandId, command.commandId)
      assert.equal(original.payload.inquiry, measured.inquiryId)
      assert.equal(original.payload.events[0].payload.goal.originalText, command.goalText)
      assert.deepEqual(requested, { ...original, payload: malformed ? {} : original.payload })
      if (!controlled) assert.deepEqual(physical.facts[0], requested)

      const coldWriter = randomUUID()
      const cold = await probe('cold', coldWriter, { sourceWriter, inquiryId: measured.inquiryId,
        writerCreated: physical.writerCreated, bytes: physical.bytes, facts: physical.facts, current: physical.current })
      assert.notEqual(cold.pid, measured.pid)
      assert.notEqual(cold.pid, process.pid)
      assert.equal(cold.writerId, coldWriter)
      assert.equal(cold.preserved, true)
      assert.deepEqual(cold.current, physical.current)
      assert.equal(cold.bytes, physical.bytes)
      assert.equal(existsSync(join(commonDir, 'wanxiang', 'events', `${coldWriter}.ndjson`)), false)
      assert.equal(controlled ? '' : readFileSync(sourceFile, 'base64'), physical.bytes)
      t.diagnostic(JSON.stringify({ scenario, measuredPid: measured.pid, coldPid: cold.pid,
        physicalAndCold: true, counts: physical.counts, cutIds: measured.cuts.map(cut => cut.cutEventId) }))

      if (scenario === 'valid') {
        assert.equal(measured.result.ok, true, JSON.stringify(measured.result.error))
        assert.equal(measured.result.value.outcome, 'created')
        assert.equal(measured.result.value.inquiryId, measured.inquiryId)
        assert.equal(measured.result.value.eventId, original.id)
        assert.equal(measured.appendError, null)
      } else {
        const code = controlled ? scenario === 'not-attempted-controlled' ? 'AppendNotAttempted' : 'NoNewWriteReleaseFailed' : 'CommitUnknown'
        assert.deepEqual(measured.appendError, { code,
          phase: scenario === 'not-attempted-controlled' ? 'BeforePhysicalAppend' : 'StoreRelease',
          causeSame: true, requested: [requested],
          prepared: controlled ? null : { durableEvents: physical.facts, cuts: measured.cuts },
          cleanupFailures: [], priorRejection: null })
        assert.equal(measured.result.ok, false)
        assert.equal(measured.result.error.code, controlled
          ? scenario === 'not-attempted-controlled' ? 'PERSISTENCE_NOT_ATTEMPTED' : 'RELEASE_FAILED' : 'COMMIT_UNKNOWN')
        assert.equal(measured.result.error.path, 'inquiryId')
        for (const identity of [measured.inquiryId, command.commandId, original.id]) {
          assert.ok(measured.result.error.message.includes(identity), identity)
        }
      }
      assert.equal(measured.cuts.length, malformed ? 1 : 0)
      assert.equal(measured.incidentReceipts.length, malformed ? 1 : 0)
      assert.deepEqual(measured.timeline, malformed ? ['append-settled', 'incident', 'returned'] : ['append-settled', 'returned'])
      if (malformed) {
        assert.deepEqual(measured.incidentReceipts[0], { inquiryId: measured.inquiryId,
          commandId: command.commandId, eventId: original.id, causeSame: true,
          evidenceSame: true, observedSettlements: 1, physical })
        assert.equal(measured.redelivery.errorMessage, 'Sphinx append-cut incident was already delivered')
        assert.equal(measured.redelivery.callbacksBefore, 1)
        assert.equal(measured.redelivery.callbacksAfter, 1)
        assert.ok(measured.redelivery.ioBefore > 0, 'Actual owned filesystem observation has a nonzero control')
        assert.equal(measured.redelivery.ioAfter, measured.redelivery.ioBefore)
        assert.equal(measured.redelivery.bytesUnchanged, true)
        assert.equal(measured.redelivery.currentUnchanged, true)
        assert.deepEqual(measured.redelivery.countsBefore, expectedCounts)
        assert.deepEqual(measured.redelivery.countsAfter, expectedCounts)
      } else assert.equal(measured.redelivery, null)
      completed = true
    } finally {
      if (completed) rmSync(root, { recursive: true, force: true })
      else t.diagnostic('SPHINX_COMMAND_SETTLEMENT_FAILURE_EVIDENCE: retained ' + root)
    }
  })
}

const physicalCoordinator = fileURLToPath(new URL('./support/sphinx-physical-binding-coordinator.mjs', import.meta.url))
const physicalChild = fileURLToPath(new URL('./support/sphinx-physical-binding-child.mjs', import.meta.url))

for (const binding of ['wire', 'mcp']) {
  for (const scenario of ['valid', 'valid-release', 'malformed-release']) {
    test(`WHAT[durable-events-024] original Sphinx ${binding} binding ${scenario} settles before its physical terminal and independent cold replay`, async t => {
      assert.equal(typeof (binding === 'wire' ? wire.createWithStore : mcp.serveConfigured), 'function')
      const root = realpathSync(mkdtempSync(join(tmpdir(), 'sphinx-physical-binding-')))
      const commonDir = join(root, '.git')
      const sourceWriter = randomUUID()
      const fatal = scenario === 'malformed-release'
      const release = scenario.endsWith('-release')
      const command = { commandId: `physical-${binding}-${scenario}`, goalText: '原授权目标\r\nNUL:\u0000；雪 😀 尾部  ',
        constraints: [], materialRefs: [], authorizationRef: 'user', profileRef: configuration.profileRef }
      const cancelReason = '原用户请求取消\r\nNUL:\u0000；雪 😀 尾部  '
      const env = { ...process.env }
      delete env.NODE_TEST_CONTEXT
      delete env.NODE_OPTIONS
      delete env.WANXIANGSHU_NO_FATAL_EXIT
      const probe = async (entry, args) => {
        try {
          return JSON.parse(await runVerificationToolProbe(process.execPath, [entry, ...args],
            { cwd: root, env, signal: t.signal }))
        } catch (error) {
          if (error?.stderr && typeof error.message === 'string') error.message += '\n' + error.stderr
          throw error
        }
      }
      let completed = false
      try {
        let seed = null
        if (binding === 'mcp') {
          const seedWriter = randomUUID()
          const seeded = await probe(child, ['measure', commonDir, seedWriter, 'valid', JSON.stringify({
            configuration, command: { ...command, commandId: 'seed-' + command.commandId },
          })])
          assert.equal(seeded.result.ok, true)
          assert.equal(seeded.result.value.outcome, 'created')
          assert.equal(seeded.physical.facts.length, 1)
          seed = { writerId: seedWriter, pid: seeded.pid, inquiryId: seeded.inquiryId,
            bytes: seeded.physical.bytes, facts: seeded.physical.facts }
        }
        const measured = await probe(physicalCoordinator, [binding, commonDir, sourceWriter, scenario,
          JSON.stringify({ configuration, command, cancelReason, seed })])
        assert.notEqual(measured.coordinatorPid, process.pid)
        assert.notEqual(measured.pid, process.pid)
        assert.notEqual(measured.pid, measured.coordinatorPid)
        assert.equal(measured.cleanupRequested, false, 'cleanup never supplies the fatal evidence')
        assert.deepEqual({ exitCode: measured.exitCode, signal: measured.signal }, fatal
          ? { exitCode: null, signal: 'SIGKILL' } : { exitCode: 0, signal: null })
        const receipt = measured.measured
        assert.equal(receipt.parentPid, measured.coordinatorPid)
        assert.equal(receipt.binding, binding)
        assert.equal(receipt.commandId, command.commandId)
        const { physical, original, requested, cuts } = receipt
        assert.equal(original.type, 'sphinx/v2-transition@2')
        assert.equal(original.payload.inquiry, receipt.inquiryId)
        assert.equal(original.payload.commandId, command.commandId)
        assert.equal(original.payload.events.length, 1)
        assert.equal(original.payload.events[0].case, binding === 'wire' ? 'InquiryCreated' : 'CancelRequested')
        if (binding === 'wire') assert.equal(original.payload.events[0].payload.goal.originalText, command.goalText)
        else {
          assert.equal(receipt.inquiryId, seed.inquiryId)
          assert.equal(original.payload.events[0].payload.reason, cancelReason)
          assert.equal(measured.protocol.command.reason, cancelReason)
          assert.equal(measured.protocol.replies, fatal ? 0 : 1)
          assert.equal(measured.protocol.read.inquiryId, seed.inquiryId)
          assert.notEqual(seed.pid, measured.pid)
        }
        assert.deepEqual(requested, [{ ...original, payload: fatal ? {} : original.payload }])
        assert.equal(physical.facts.length, fatal ? 2 : 1)
        assert.deepEqual(physical.facts[0], requested[0])
        assert.deepEqual(physical.counts, { append: 1, fsync: 1, close: 1, release: 1, injected: release ? 1 : 0 })
        assert.equal(physical.lockReleased, true)
        assert.equal(physical.openDescriptors, 0)
        assert.equal(physical.syncedDescriptors, 0)
        assert.equal(existsSync(join(commonDir, 'wanxiang.lock')), false)
        const sourceFile = join(commonDir, 'wanxiang', 'events', `${sourceWriter}.ndjson`)
        assert.equal(readFileSync(sourceFile, 'base64'), physical.bytes)
        for (const head of physical.heads) {
          const fact = physical.facts.find(value => value.stream === head.stream)
          assert.equal(head.head, fact.id)
          assert.deepEqual(head.heads, [fact.id])
        }
        assert.equal(cuts.length, fatal ? 1 : 0)
        if (release) {
          assert.deepEqual(receipt.appendError, { code: 'CommitUnknown', phase: 'StoreRelease', causeSame: true,
            requested, prepared: { durableEvents: physical.facts, cuts }, cleanupFailures: [], priorRejection: null })
        } else assert.equal(receipt.appendError, null)
        if (fatal) {
          const [cut] = cuts
          const cutFact = physical.facts[1]
          assert.equal(cut.rule, 'SphinxV2')
          assert.equal(cut.failedEventId, original.id)
          assert.equal(cut.cutEventId, cutFact.id)
          assert.equal(cutFact.type, 'ProjectionCutTail')
          assert.equal(cutFact.payload.rule, 'SphinxV2')
          assert.equal(cutFact.payload.failed_event_id, original.id)
          assert.deepEqual(cutFact.parents, [original.id])
          assert.notEqual(cutFact.stream, original.stream)
          assert.equal(physical.current.ok, false)
          assert.equal(physical.current.error.code, 'SemanticCut')
          assert.match(physical.current.error.message, /INVALID_TRANSITION_DTO/)
          assert.equal(measured.returned, null)
          assert.equal(measured.reports.length, 1)
          assert.equal(measured.reports[0].operation, 'sphinx-semantic-cut')
          for (const text of [receipt.inquiryId, command.commandId, original.id, 'StoreRelease', receipt.causeText]) {
            assert.ok(measured.reports[0].result.includes(text), text)
          }
        } else {
          assert.deepEqual(measured.reports, [])
          assert.equal(physical.current.ok, true)
          assert.equal(physical.current.value.eventHead, original.id)
          assert.equal(physical.current.value.revision, binding === 'wire' ? '0' : '1')
          assert.equal(measured.returned.outcome, release ? 'refused' : binding === 'wire' ? 'created' : 'applied')
          if (binding === 'mcp') {
            assert.deepEqual(physical.current.value.status, { case: 'Cancelling' })
            if (!release) assert.equal(measured.returned.status, 'cancelling')
          }
          if (release) {
            assert.equal(measured.returned.refusal.code, 'COMMIT_UNKNOWN')
            for (const text of [receipt.inquiryId, command.commandId, original.id, receipt.causeText]) {
              assert.ok(measured.returned.refusal.message.includes(text), text)
            }
          }
        }
        const coldWriter = randomUUID()
        const cold = await probe(physicalChild, ['cold', commonDir, coldWriter, scenario,
          JSON.stringify({ sourceWriter, inquiryId: receipt.inquiryId, physical, seed })])
        assert.notEqual(cold.pid, measured.pid)
        assert.notEqual(cold.pid, measured.coordinatorPid)
        assert.notEqual(cold.pid, process.pid)
        assert.equal(cold.writerId, coldWriter)
        assert.equal(cold.preserved, true)
        assert.deepEqual(cold.current, physical.current)
        assert.equal(cold.bytes, physical.bytes)
        assert.equal(existsSync(join(commonDir, 'wanxiang', 'events', `${coldWriter}.ndjson`)), false)
        assert.equal(readFileSync(sourceFile, 'base64'), physical.bytes)
        if (seed) assert.equal(readFileSync(join(commonDir, 'wanxiang', 'events', `${seed.writerId}.ndjson`), 'base64'), seed.bytes)
        t.diagnostic(JSON.stringify({ binding, scenario, nativePid: measured.pid, coldPid: cold.pid,
          terminal: measured.signal ?? measured.exitCode, physicalAndCold: true }))
        completed = true
      } finally {
        if (completed) rmSync(root, { recursive: true, force: true })
        else t.diagnostic('SPHINX_PHYSICAL_BINDING_FAILURE_EVIDENCE: retained ' + root)
      }
    })
  }
}

test.todo('WHAT[durable-events-024] actual semantic-cut caller requires injected fatal capability and refuses fatal before committed or unknown settlement')
test.todo('WHAT[durable-events-024] actual physical child persists bad fact and cut then reports and exits once; duplicate incident cannot execute again')
