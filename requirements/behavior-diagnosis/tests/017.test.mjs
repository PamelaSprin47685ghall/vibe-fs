// BD-017: Bounded protocol repair and AABB state recovery
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const text = (value) => [{ type: 'text', text: value }]

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, payload, options) => {
    captured.push({ session, text: payload, options })
    return dispatch.admittedWithReceipt('receipt-153')
  },
})

const managerOwner = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const valid = (messageId, overrides = {}) => ({
  messageId,
  parts: [{ tool: 'chronicle', callID: 'c1', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }],
  ...overrides,
})
const prose = (messageId) => ({ messageId, parts: [{ type: 'text', text: 'plain response' }] })
const invalid = (messageId) => ({ messageId, parts: [{ tool: 'chronicle', state: { status: 'completed', input: { text: 'no tip' } } }] })
const classify = (messageId, parts) => enforcer.classifyAssistantStep({ messageId, parts })

test('WHAT[BD-017] CHRONICLE_empty_canonical_text_returns_public_consequence', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: '   ', tip: 'primitive-obsession' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'nothing-to-remember')
  assert.equal(result.error, blog.emptyTextError)
})

test('WHAT[BD-017] repeated invalid turns re-open repair and stable terminals complete it', () => {
  assert.equal(turns.repairDefectDecision(false, false, null, []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, false, 'tool-calls', []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, true, 'length', []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('done')), 'NoRepair')
})

test('WHAT[BD-017] concurrent gate nudge deduplicates at the dispatch boundary', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-enf153-nudge-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-153',
      'rt-153',
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await dispatch.acceptHumanRootSelection(
        opened.journal,
        'ses_153_owner',
        'msg-153-owner',
        managerOwner,
      )
      assert.equal(owner.ok, true, owner.ok ? '' : owner.error)

      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        capturingPort(captured),
        opened.journal,
        'ses_153',
        'nudge text',
        'BusyAgentNudge',
        'interaction-repair',
        'run-153',
        owner.profile,
      )
      assert.equal(results.length, 2)
      assert.equal(results[0].ok, true, JSON.stringify(results[0]))
      assert.equal(results[1].ok, true, 'second nudge on same occasion joins, never fails')
      assert.equal(captured.length, 1, `a deduplicated nudge sends once, got ${captured.length}`)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[BD-017] authority gate-nudge admission is required before any physical send', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-enf153-gate-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-153g',
      'rt-153g',
      4243,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true)
    try {
      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        capturingPort(captured),
        opened.journal,
        'ses_noroot_153',
        'nudge',
        'BusyAgentNudge',
        'interaction-repair',
        'run-153',
        { authorityKind: 'AgentOwnerRoot', identitySeed: { kind: 'NoSuchSeed' } },
      )
      assert.equal(results.every((r) => !r.ok), true)
      assert.ok(results.every((r) => /identity seed|seed kind/i.test(r.error ?? '')), JSON.stringify(results))
      assert.equal(captured.length, 0, 'no profile → no physical send')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[BD-017] ENFORCER_061_empty_calls_rebuilds_without_fatal', () => {
  const out = blog.protocol(prose('asst-prose'))
  assert.equal(out.state, 'ProjectMessages')
  assert.equal(out.fatal, null)
})

test('WHAT[BD-017] ENFORCER_061_invalid_tip_is_protocol_skip', () => {
  const out = blog.protocol(invalid('asst-skip'))
  assert.equal(out.state, 'ProjectMessages')
  assert.equal(out.fatal, null)
  assert.equal(enforcer.classifyAssistantStep(invalid('asst-skip')).acceptedCalls, 0)
})

test('WHAT[BD-017] ENFORCER_stopPhysicalRun_argument_order_is_messages_then_fallback', () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const continuation = readFileSync(join(root, 'src/Wanxiangshu/Enforcer/Continuation.fs'), 'utf8')

  assert.match(
    continuation,
    /let private stopPhysicalRun\s*\(messages: obj list\)\s*\(reason: string\)/,
    'definition order is messages then reason (no fallback since ENFORCER-047)',
  )
  const calls = [...continuation.matchAll(/stopPhysicalRun\s+(\w+)\s+(\w+)\s+/g)].map((m) => [
    m[1],
    m[2],
  ])
  assert.ok(calls.length >= 1, `expected injection call site, got ${calls.length}`)
  for (const [first, second] of calls) {
    assert.equal(
      first,
      'rawMessages',
      `stopPhysicalRun first arg must be rawMessages (the ctx.Stop injection), got ${first} ${second}`,
    )
    assert.equal(
      second,
      'reason',
      `stopPhysicalRun second arg must be reason (not fallback), got ${first} ${second}`,
    )
  }
})

test('WHAT[BD-017] ENFORCER_last_assistant_step_ignores_malformed_messages', () => {
  assert.equal(classify('', [null]).providerRun, null)
  assert.equal(classify('', [{ info: { id: 'x', role: 'user' } }]).acceptedCalls, 0)
  assert.equal(classify('', [{ info: { id: 'x' } }]).acceptedCalls, 0)
  assert.equal(classify('', [{ info: { role: 'assistant' } }]).acceptedCalls, 0)

  const bare = classify('a-1', [])
  assert.equal(bare.providerRun, 'a-1')
  assert.equal(bare.acceptedCalls, 0)

  const full = classify('a-2', [{ tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 't' } } }])
  assert.equal(full.providerRun, 'a-2')
  assert.equal(full.acceptedCalls, 1)
})

test('WHAT[BD-017] ENFORCER_bad_tip_decode_is_protocol_skip_and_rebuilds', () => {
  const out = classify('asst-skip', [{ tool: 'chronicle', state: { status: 'completed', input: { text: 'no tip' } } }])
  assert.equal(out.protocol, 'ProjectMessages')
  assert.equal(out.acceptedCalls, 0)
})

test('WHAT[BD-017] ENFORCER_completed_blog_part_in_empty_arm_rebuilds', () => {
  const out = classify('asst-completed-skip', [
    { tool: 'chronicle', state: { status: 'completed', input: {} } },
    { type: 'text', text: 'plain' },
  ])
  assert.equal(out.hasBlogToolPart, true)
  assert.equal(out.acceptedCalls, 0)
})

test('WHAT[BD-017] ENFORCER_interrupted_statusless_blog_part_aabbs', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { metadata: { interrupted: true } } })
  assert.equal(out.blogPartInterrupted, true)
  assert.equal(out.hasFailedBlogAttempt, true)
})

test('WHAT[BD-017] ENFORCER_uninterrupted_statusless_blog_part_rebuilds', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { metadata: { interrupted: false } } })
  assert.equal(out.blogPartInterrupted, false)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_running_blog_part_projects_raw', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { status: 'running' } })
  assert.equal(out.hasIncompleteBlogTool, true)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_unknown_status_blog_part_is_not_a_failed_attempt', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { status: 'weird', metadata: { interrupted: false } } })
  assert.equal(out.hasIncompleteBlogTool, false)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_stateless_blog_part_has_no_status', () => {
  const out = blog.classifyPart({ tool: 'chronicle' })
  assert.equal(out.status, null)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_statusless_blog_part_is_not_incomplete', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: {} })
  assert.equal(out.hasIncompleteBlogTool, false)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_null_part_in_transcript_is_ignored', () => {
  const out = classify('asst-nullpart', [null])
  assert.equal(out.acceptedCalls, 0)
  assert.equal(out.hasBlogToolPart, false)
})
