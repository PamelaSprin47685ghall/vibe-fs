// RELAY successor admission — real managed-chat proof (no prompt-count proxy).
//
// Pre-fix Fold closes HumanRoot on ANY RetirementCommitted, so the successor
// continuation has no active LogicalRun and managed admission fails with:
//   TransactionFailed (AcceptanceFailed (IntentRejected
//     ("Continuation managed intent requires an active logical run")))
// Post-fix Fold retains active HumanRoot across SuccessorRequested=true and
// only closes when QualityCandidateAccepted=true. Continuation keeps exact
// LogicalRun/root/identity; successor gets a new AuditPending incumbency.
//
// All ingress goes through production hooks: HumanRoot via chat.message,
// review/suicide via production tool registrations with exact transcript
// binding, retired-tail pinch + successor dispatch via
// experimental.chat.messages.transform, successor delivery via real
// chat.message admission (stub prompt transport only carries the PromptKey;
// counting promptAsync alone would miss the missing admission).

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import test from 'node:test'

import {
  observeAuthority,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const commitWorkspace = (directory) => {
  execFileSync('git', [
    '-c', 'user.name=fixture',
    '-c', 'user.email=fixture@example.com',
    'commit', '--allow-empty', '-m', 'initial',
  ], { cwd: directory, stdio: 'ignore' })
}

const NONPERFECT = {
  language_algorithms: 9,
  simplicity: 10,
  structure: 10,
  granularity: 10,
  tests_evidence: 10,
  logic_reliability_boundaries: 10,
  caller_ergonomics: 10,
  completeness: 10,
}

const PERFECT = {
  language_algorithms: 10,
  simplicity: 10,
  structure: 10,
  granularity: 10,
  tests_evidence: 10,
  logic_reliability_boundaries: 10,
  caller_ergonomics: 10,
  completeness: 10,
}

const admitHumanRootViaChat = async (hooks, sessionID, rootID, text = 'root work') => {
  const output = {
    message: {
      id: rootID,
      role: 'user',
      sessionID,
      agent: 'manager',
      model: { providerID: 'host', modelID: 'placeholder' },
    },
    parts: [{ type: 'text', text }],
  }
  await hooks['chat.message']({ sessionID, agent: 'manager' }, output)
}

const pushReviewTranscript = (runtime, sessionID, rootID, reviewRun, reviewCall, scores, narrative = 'narrative before review') => {
  runtime.pushHostMessage(sessionID, {
    id: rootID,
    role: 'user',
    parts: [{ type: 'text', text: 'root work' }],
  })
  runtime.pushHostMessage(sessionID, {
    id: reviewRun,
    role: 'assistant',
    parentID: rootID,
    parts: [
      { type: 'text', text: narrative },
      {
        type: 'tool-call',
        id: reviewCall,
        callID: reviewCall,
        tool: 'review',
        args: scores,
        state: { status: 'pending', input: scores },
      },
    ],
  })
}

const lastPromptKey = (runtime) => {
  const last = runtime.prompts[runtime.prompts.length - 1]
  return (
    last?.body?.metadata?.wanxiangshu_prompt_key ??
    last?.body?.parts?.[0]?.metadata?.wanxiangshu_prompt_key ??
    last?.parts?.[0]?.metadata?.wanxiangshu_prompt_key ??
    null
  )
}

const transformMessages = (rootID, sessionID, extra = []) => ({
  messages: [
    {
      info: { id: rootID, sessionID, role: 'user' },
      role: 'user',
      parts: [{ type: 'text', text: 'root work' }],
    },
    ...extra,
  ],
})

test('WHAT[RELAY-006] successor-managed admission retains exact LogicalRun across successor-needed retirement', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    const sessionID = 'ses-successor-admission'
    const rootID = `root-${sessionID}`
    const reviewRun = `run-review-${sessionID}`
    const reviewCall = `call-review-${sessionID}`
    const suicideRun = `run-suicide-${sessionID}`
    const suicideCall = `call-suicide-${sessionID}`
    const successorPhysical = `msg-successor-${sessionID}`

    await admitHumanRootViaChat(hooks, sessionID, rootID)
    const before = observeAuthority(runtime, sessionID)
    assert.ok(before.activeLogicalRun, 'HumanRoot manager must be active before review')
    assert.equal(before.activeLogicalRun.authorityKind, 'HumanRoot')
    const beforeRun = before.activeLogicalRun.logicalRun
    const beforeRoot = before.activeLogicalRun.authorityRoot

    pushReviewTranscript(runtime, sessionID, rootID, reviewRun, reviewCall, NONPERFECT)
    const reviewResult = await hooks.tool.review.execute(
      { ...NONPERFECT },
      { sessionID, agent: 'manager', messageID: reviewRun, callID: reviewCall },
    )
    assert.match(reviewResult, /recorded/, 'nonperfect review must be recorded via exact transcript binding')
    assert.match(reviewResult, /WorkOwned/, 'nonperfect assessment must enter WorkOwned')

    const suicideResult = await hooks.tool.suicide.execute(
      {},
      { sessionID, agent: 'manager', messageID: suicideRun, callID: suicideCall },
    )
    assert.match(suicideResult, /retired/, 'suicide must retire via production tool')
    assert.match(suicideResult, /successor_requested.*true/s, 'nonperfect retirement must request a successor')

    // Retired-tail pinch: this transform commits SuccessorActivated and claims
    // the successor prompt through the formal relay-successor gate. Counting
    // promptAsync alone would miss the missing managed admission below.
    const promptCountBefore = runtime.prompts.length
    const retiredOutput = transformMessages(rootID, sessionID, [
      {
        info: { id: reviewRun, sessionID, role: 'assistant', parentID: rootID },
        role: 'assistant',
        parts: [
          { type: 'text', text: 'narrative before review' },
          { type: 'tool-call', callID: reviewCall, tool: 'review', args: { ...NONPERFECT } },
        ],
      },
      {
        info: { id: suicideRun, sessionID, role: 'assistant', parentID: rootID },
        role: 'assistant',
        parts: [{ type: 'tool-call', callID: suicideCall, tool: 'suicide', args: {} }],
      },
    ])
    await hooks['experimental.chat.messages.transform']({ sessionID }, retiredOutput)
    assert.ok(
      runtime.prompts.length > promptCountBefore,
      'retired transform must dispatch the successor prompt through the formal gate',
    )
    assert.deepEqual(retiredOutput.messages, [], 'retired run continuation must be pinched off before network')

    const successorKey = lastPromptKey(runtime)
    assert.ok(successorKey, 'successor prompt must carry a durable PromptKey')

    // Real managed chat admission for the successor — pre-fix this throws
    // TransactionFailed (AcceptanceFailed (IntentRejected
    // ("Continuation managed intent requires an active logical run"))).
    const successorOutput = {
      message: { id: successorPhysical, sessionID, role: 'user' },
      parts: [
        {
          type: 'text',
          text: 'successor continuation',
          metadata: { wanxiangshu_prompt_key: successorKey },
        },
      ],
    }
    await hooks['chat.message']({ sessionID, messageID: successorPhysical }, successorOutput)

    const after = observeAuthority(runtime, sessionID)
    assert.ok(after.activeLogicalRun, 'successor must be physically accepted, not merely sent')
    assert.equal(
      after.activeLogicalRun.logicalRun,
      beforeRun,
      'successor continuation must keep the exact LogicalRun across successor-needed retirement',
    )
    assert.equal(
      after.activeLogicalRun.authorityRoot,
      beforeRoot,
      'successor continuation must keep the exact authority root/identity',
    )

    // Successor provider context is a new AuditPending incumbency, not a
    // resurrected retired run.
    const successorTurn = transformMessages(rootID, sessionID, [
      {
        info: { id: suicideRun, sessionID, role: 'assistant', parentID: rootID },
        role: 'assistant',
        parts: [{ type: 'tool-call', callID: suicideCall, tool: 'suicide', args: {} }],
      },
      {
        info: { id: successorPhysical, sessionID, role: 'user' },
        role: 'user',
        parts: [
          {
            type: 'text',
            text: 'successor continuation',
            metadata: { wanxiangshu_prompt_key: successorKey },
          },
        ],
      },
    ])
    await hooks['experimental.chat.messages.transform']({ sessionID }, successorTurn)
    assert.ok(successorTurn.messages.length > 0, 'successor provider turn must survive the cut')
    const rendered = JSON.stringify(successorTurn.messages)
    assert.match(rendered, /\[RelayContext\]/, 'successor context must be projected from current baton')
    assert.match(rendered, /AuditPending/, 'successor incumbency must start AuditPending')
  })
})

test('WHAT[INTERACTION-AUTHORITY-018] perfect HumanRoot retirement closes authority with no successor via real fold', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    const sessionID = 'ses-successor-perfect-close'
    const rootID = `root-${sessionID}`
    const reviewRun = `run-review-${sessionID}`
    const reviewCall = `call-review-${sessionID}`
    const suicideRun = `run-suicide-${sessionID}`
    const suicideCall = `call-suicide-${sessionID}`

    await admitHumanRootViaChat(hooks, sessionID, rootID)
    const before = observeAuthority(runtime, sessionID)
    assert.ok(before.activeLogicalRun, 'HumanRoot manager must be active before perfect review')

    pushReviewTranscript(runtime, sessionID, rootID, reviewRun, reviewCall, PERFECT, 'perfect narrative')
    const reviewResult = await hooks.tool.review.execute(
      { ...PERFECT },
      { sessionID, agent: 'manager', messageID: reviewRun, callID: reviewCall },
    )
    assert.match(reviewResult, /recorded/, 'perfect review must be recorded via exact transcript binding')
    assert.match(reviewResult, /PerfectAwaitingRetirement/, 'all-ten assessment must downgrade the phase')

    const suicideResult = await hooks.tool.suicide.execute(
      {},
      { sessionID, agent: 'manager', messageID: suicideRun, callID: suicideCall },
    )
    assert.match(suicideResult, /retired/, 'perfect suicide must retire via production tool')
    assert.match(suicideResult, /quality_candidate_accepted.*true/s, 'perfect retirement must accept the quality candidate')
    assert.match(suicideResult, /successor_requested.*false/s, 'perfect retirement must request no successor')

    // Real fold closure: active HumanRoot is gone, history retained.
    const closed = observeAuthority(runtime, sessionID)
    assert.equal(closed.activeLogicalRun, null, 'perfect retirement must close the HumanRoot authority')

    // No successor dispatch on the retired tail.
    const promptCountBefore = runtime.prompts.length
    const retiredOutput = transformMessages(rootID, sessionID, [
      {
        info: { id: reviewRun, sessionID, role: 'assistant', parentID: rootID },
        role: 'assistant',
        parts: [
          { type: 'text', text: 'perfect narrative' },
          { type: 'tool-call', callID: reviewCall, tool: 'review', args: { ...PERFECT } },
        ],
      },
      {
        info: { id: suicideRun, sessionID, role: 'assistant', parentID: rootID },
        role: 'assistant',
        parts: [{ type: 'tool-call', callID: suicideCall, tool: 'suicide', args: {} }],
      },
    ])
    await hooks['experimental.chat.messages.transform']({ sessionID }, retiredOutput)
    assert.equal(
      runtime.prompts.length,
      promptCountBefore,
      'perfect retirement must dispatch no successor prompt',
    )
    assert.deepEqual(retiredOutput.messages, [], 'retired tail without successor must still be pinched off')

    const ordinaryPhysical = `msg-ordinary-${sessionID}`
    const claimed = await dispatch.sendContinuation(
      {
        SubscribeTerminal: () => ({ Dispose: () => {} }),
        SendPrompt: async () => dispatch.admittedWithReceipt('historical-profile-receipt'),
      },
      runtime.journal, sessionID, 'ordinary after close', 'ManagerGuard',
      before.activeLogicalRun, 'manager', 'Await',
    )
    assert.equal(claimed.ok, true, claimed.error)
    const ordinaryOutput = {
      message: { id: ordinaryPhysical, sessionID, role: 'user' },
      parts: [
        {
          type: 'text',
          text: 'ordinary after close',
          metadata: { wanxiangshu_prompt_key: claimed.key },
        },
      ],
    }
    await assert.rejects(
      () => hooks['chat.message']({ sessionID, messageID: ordinaryPhysical }, ordinaryOutput),
      (error) => error.message.includes('Continuation managed intent requires an active logical run'),
      'a claimed continuation cannot reuse a closed historical profile',
    )
    const stillClosed = observeAuthority(runtime, sessionID)
    assert.equal(stillClosed.activeLogicalRun, null, 'rejected continuation must not resurrect the closed run')
  })
})

test('WHAT[INTERACTION-AUTHORITY-018] AgentOwnerRoot Manager retirement preserves owner-directed authority', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    commitWorkspace(directory)
    const parent = 'ses-owner-survives-parent'
    const child = 'ses-owner-survives-child'
    const parentRoot = `root-${parent}`

    await admitHumanRootViaChat(hooks, parent, parentRoot, 'parent work')
    const parentBefore = observeAuthority(runtime, parent)
    assert.ok(parentBefore.activeLogicalRun, 'parent HumanRoot must be active')
    assert.equal(parentBefore.activeLogicalRun.authorityKind, 'HumanRoot')

    // Child AgentOwnerRoot through the production dispatcher claim plus real
    // chat.message physical acceptance (same writer production ingress uses).
    const seedResult = authority.issueInheritedIdentitySeed('manager', parentBefore.activeLogicalRun)
    assert.equal(seedResult.ok, true, seedResult.ok ? '' : JSON.stringify(seedResult.error))
    const port = {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('accepted-owner-child'),
    }
    const sent = await dispatch.sendAgentOwnerRoot(port, runtime.journal, child, 'child work', seedResult.value)
    assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
    assert.ok(sent.key, 'child claim must return a PromptKey')

    const childPhysical = `msg-${child}-root`
    await hooks['chat.message'](
      { sessionID: child, messageID: childPhysical },
      {
        message: { id: childPhysical, sessionID: child, role: 'user' },
        parts: [
          {
            type: 'text',
            text: 'child work',
            metadata: { wanxiangshu_prompt_key: sent.key },
          },
        ],
      },
    )
    const childBefore = observeAuthority(runtime, child)
    assert.ok(childBefore.activeLogicalRun, 'child AgentOwnerRoot must be active before parent retirement')
    assert.equal(childBefore.activeLogicalRun.authorityKind, 'AgentOwnerRoot')
    const childRun = childBefore.activeLogicalRun.logicalRun

    const reviewRun = `run-review-${child}`
    const reviewCall = `call-review-${child}`
    const suicideRun = `run-suicide-${child}`
    const suicideCall = `call-suicide-${child}`
    pushReviewTranscript(runtime, child, childPhysical, reviewRun, reviewCall, PERFECT, 'child perfect')
    const reviewResult = await hooks.tool.review.execute(
      { ...PERFECT },
      { sessionID: child, agent: 'manager', messageID: reviewRun, callID: reviewCall },
    )
    assert.match(reviewResult, /PerfectAwaitingRetirement/)
    const suicideResult = await hooks.tool.suicide.execute(
      {},
      { sessionID: child, agent: 'manager', messageID: suicideRun, callID: suicideCall },
    )
    assert.match(suicideResult, /quality_candidate_accepted.*true/s)

    const childAfter = observeAuthority(runtime, child)
    assert.ok(childAfter.activeLogicalRun, 'Manager retirement must preserve AgentOwnerRoot for its owner')
    assert.equal(childAfter.activeLogicalRun.logicalRun, childRun, 'child must keep its exact LogicalRun')
    assert.equal(childAfter.activeLogicalRun.authorityKind, 'AgentOwnerRoot')
  })
})
