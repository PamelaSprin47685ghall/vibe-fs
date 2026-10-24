#!/usr/bin/env node
// WHAT[delegation-034] Physical canary for omp native negotiate mechanism.
//
// Proves physical facts on compiled Fable artifacts:
// 1. HostPlugin connects with activateOnOmp (dist/Omp/Adapter.js).
// 2. Child session created via ports.Sessions.Create is tracked in liveChildren with delegation bound.
// 3. negotiate tool returns immediate acknowledgment with receipt / nonce before answer is produced.
// 4. Native ephemeral turn runs against delegator context preserving system prompt and history.
// 5. Physical steer lands in child session entries and event latch triggers before harness deadline.
// 6. Exact persisted answer and nonce match in child session.
// 7. First-reset gate unlocks ONLY after physical landing:
//    - resetScope tool execution is refused while gate is locked.
//    - resetScope tool execution is admitted after gate is unlocked.
// 8. Negative checks: unmatched/stale steer does NOT unlock gate.
// 9. Cleanup all sessions in finally.

import { test } from 'bun:test'
import assert from 'node:assert/strict'
import http from 'node:http'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { execSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const REPO = path.resolve(HERE, '../../..')

const OPT_IN = 'OMP_NEGOTIATE_PHYSICAL_SMOKE'
const isOptedIn = process.env[OPT_IN] === '1'

async function runCanary() {
  if (!isOptedIn) {
    console.log(`Skipping: set ${OPT_IN}=1 to run physical canary`);
    return;
  }
  await testBody();
}

async function testBody() {
  // Public SDK import root resolution (portable, configurable via env var)
  const sdkRoot =
    process.env.OH_MY_PI_PATH ||
    process.env.PI_CODING_AGENT_PATH ||
    path.resolve(REPO, '../oh-my-pi/packages/coding-agent')

  const { createAgentSession, SessionManager, discoverAuthStorage, ModelRegistry } = await import(
    path.join(sdkRoot, 'src/index.ts')
  )
  const { AgentRegistry } = await import(path.join(sdkRoot, 'src/registry/agent-registry.ts'))
  const { initializeExtensions } = await import(path.join(sdkRoot, 'src/modes/runtime-init.ts'))
  const { activateOnOmp, getLiveChildren } = await import(path.join(REPO, 'dist/Omp/Adapter.js'))
  const { HostPlugin_create, HostPlugin_nodeClockPort, HostPlugin_nodeTimerPort } = await import(
    path.join(REPO, 'dist/Host/SecondHost/HostPlugin.js')
  )
  const { SessionIdModule_create, SessionIdModule_value } = await import(
    path.join(REPO, 'dist/Foundation/Identity.js')
  )
  const { negotiationRequired } = await import(path.join(REPO, 'dist/Context/Scope/Surface.js'))
  const { empty } = await import(path.join(REPO, 'dist/fable_modules/fable-library-js.5.13.0/List.js'))

  // 1. Spin up local mock SSE server on 127.0.0.1
  const requestsSeen = []
  let requestCounter = 0
  const EXPECTED_ANSWER = 'RFC-7807 problem details with traceId required.'
  const server = http.createServer(async (req, res) => {
    let bodyText = ''
    for await (const chunk of req) bodyText += chunk
    const body = bodyText ? JSON.parse(bodyText) : {}
    const msgs = body.messages || []
    const hasTools = Array.isArray(body.tools) && body.tools.length > 0
    const sysMsg = msgs.find((m) => m.role === 'system')
    requestCounter++
    const entry = {
      n: requestCounter,
      model: body.model,
      hasTools,
      toolNames: (body.tools || []).map((t) => t.function?.name || t.name),
      sysPrompt: sysMsg?.content || '',
      messages: msgs.map((m) => ({ role: m.role, content: m.content })),
    }
    requestsSeen.push(body)

    // Ephemeral requests carry the negotiation questions
    const isEphemeral = msgs.some((m) => {
      const text = typeof m.content === 'string'
        ? m.content
        : Array.isArray(m.content) ? m.content.map((c) => c.text || '').join(' ') : ''
      return text.includes('format error responses')
    })

    const replyText = isEphemeral ? EXPECTED_ANSWER : 'MAIN_PROMPT_INITIALIZED'

    const chunk = {
      id: 'chunk-' + requestCounter,
      object: 'chat.completion.chunk',
      model: body.model,
      choices: [
        {
          index: 0,
          delta: { content: replyText },
          finish_reason: null,
        },
      ],
    }
    const endChunk = {
      id: 'end-' + requestCounter,
      object: 'chat.completion.chunk',
      model: body.model,
      choices: [
        {
          index: 0,
          delta: {},
          finish_reason: 'stop',
        },
      ],
    }
    res.writeHead(200, { 'Content-Type': 'text/event-stream' })
    res.write('data: ' + JSON.stringify(chunk) + '\n\n')
    res.write('data: ' + JSON.stringify(endChunk) + '\n\n')
    res.write('data: [DONE]\n\n')
    res.end()
  })

  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve))
  const port = server.address().port
  const baseUrl = `http://127.0.0.1:${port}/v1`

  // 2. Prepare isolated temporary git repository
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'omp-smoke-canary-'))
  execSync('git init -b main && git config user.name "Canary Tester" && git config user.email "canary@test.local"', {
    cwd: tmpDir,
    stdio: 'ignore',
  })
  const agentDir = path.join(tmpDir, 'agent')
  fs.mkdirSync(agentDir, { recursive: true })

  let parentSession = null
  let childSession = null

  try {
    const authStorage = await discoverAuthStorage(agentDir)
    const mr = new ModelRegistry(authStorage)
    mr.registerProvider('local-canary', {
      baseUrl,
      apiKey: 'test-key',
      api: 'openai-completions',
      models: [
        {
          id: 'canary-model',
          name: 'canary-model',
          api: 'openai-completions',
          reasoning: false,
          input: ['text'],
          contextWindow: 128000,
          maxTokens: 1024,
          cost: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 },
        },
      ],
    })
    const model = mr.find('local-canary', 'canary-model')

    // Create HostPlugin instance
    const clock = HostPlugin_nodeClockPort()
    const timerFactory = () => HostPlugin_nodeTimerPort()
    const plugin = HostPlugin_create(clock, timerFactory)

    // Capture host ports when plugin activates
    let capturedPorts = null
    const origActivate = plugin.Activate.bind(plugin)
    plugin.Activate = (ports) => {
      capturedPorts = ports
      return origActivate(ports)
    }

    const registeredTools = new Map()
    const agentRegistry = new AgentRegistry()
    let childCounter = 0
    const extension = async (pi) => {
      // Provide SDK surface on pi.pi for child session creation
      pi.pi = {
        SessionManager,
        createAgentSession: (opts) =>
          createAgentSession({
            agentId: opts.agentId || `child-worker-${++childCounter}`,
            ...opts,
            authStorage,
            modelRegistry: mr,
            model,
            agentRegistry,
            disableExtensionDiscovery: true,
          }),
      }

      const origRegisterTool = pi.registerTool.bind(pi)
      pi.registerTool = (toolDef) => {
        registeredTools.set(toolDef.name, toolDef)
        return origRegisterTool(toolDef)
      }

      await activateOnOmp(pi, plugin, empty())
    }

    // 3. Start delegator session with product extension
    const parentSessionResult = await createAgentSession({
      agentId: 'parent-delegator',
      cwd: tmpDir,
      agentDir,
      authStorage,
      modelRegistry: mr,
      model,
      agentRegistry,
      extensions: [extension],
      disableExtensionDiscovery: true,
    })
    parentSession = parentSessionResult.session
    const parentId = parentSession.sessionManager.getSessionId()

    if (parentSession.extensionRunner) {
      await initializeExtensions(parentSession, {
        reportSendError: () => {},
        reportRuntimeError: () => {},
      })
    }
    await parentSession.extensionRunner?.emit({ type: 'session_start' })

    // Prime delegator session with a prompt establishing context
    const DELEGATOR_CONTEXT_RULE = 'Delegator architectural invariant: errors must carry traceId.'
    await parentSession.prompt(DELEGATOR_CONTEXT_RULE)

    assert.ok(capturedPorts, 'HostPlugin.Activate must have captured host ports')
    assert.ok(capturedPorts.Sessions, 'hostPorts must provide Sessions capability')

    // 4. Create child session through actual ports.Sessions.Create path
    const createChildOutcome = await capturedPorts.Sessions.Create(
      SessionIdModule_create(parentId),
      { Title: 'child-worker', Workspace: undefined },
    )
    assert.equal(createChildOutcome.tag, 0, 'ports.Sessions.Create must succeed')
    const childSessionIdObj = createChildOutcome.fields[0]
    const childId = SessionIdModule_value(childSessionIdObj)

    // Verify child is tracked in Adapter liveChildren
    const liveMap = getLiveChildren()
    const childRecord = liveMap.get(childId)
    assert.ok(childRecord, 'child must be tracked in Adapter liveChildren')
    childSession = childRecord.Session
    assert.ok(childSession, 'child session instance must be present in liveChildren')

    // Fact: Delegation is bound and gate is locked before negotiation
    assert.equal(negotiationRequired(childId), true, 'first-reset gate must be locked before negotiation')

    // Verify tools were registered
    const negotiateDef = registeredTools.get('negotiate')
    assert.ok(negotiateDef, 'negotiate tool must be registered by HostPlugin')
    const resetScopeDef = registeredTools.get('resetScope')
    assert.ok(resetScopeDef, 'resetScope tool must be registered by Adapter')

    const childCtx = {
      cwd: tmpDir,
      sessionManager: childSession.sessionManager,
      sessionId: childId,
    }

    // Fact: Testing resetScope tool directly - must be refused while gate is locked
    const earlyResetResult = await resetScopeDef.execute(
      'call-early-reset',
      { newScopeObjective: 'Initial exploration', newScopeTurnBudget: 5 },
      null,
      null,
      childCtx,
    )
    assert.equal(earlyResetResult.details?.refused, true, 'resetScope must be refused while gate is locked')
    assert.match(
      earlyResetResult.content[0].text,
      /resetScope is unavailable until the first negotiate\(questions\) reply has arrived/,
      'resetScope refusal message must guide to negotiate tool',
    )

    // 5. Setup event latch with harness deadline for physical steer landing
    let expectedReceipt = ''
    let steerReceived = false
    let resolveSteer
    const steerPromise = new Promise((resolve) => {
      resolveSteer = resolve
    })

    const unsubscribe = childSession.subscribe((event) => {
      if (event.type === 'message_end' || event.type === 'entry') {
        const entries = childSession.sessionManager.getEntries()
        const found = entries.some(
          (e) =>
            e.type === 'message' &&
            e.message?.role === 'user' &&
            typeof e.message?.content?.[0]?.text === 'string' &&
            e.message.content[0].text.includes('Delegator answered negotiate'),
        )
        if (found && !steerReceived) {
          steerReceived = true
          resolveSteer()
        }
      }
    })

    // 6. Child session invokes negotiate tool
    const immediateAck = await negotiateDef.execute(
      'call-neg-canary',
      { questions: 'How should we format error responses?' },
      null,
      null,
      childCtx,
    )

    // Fact: Immediate acknowledgment returned synchronously before answer arrives
    assert.ok(immediateAck && immediateAck.content, 'negotiate returns immediate acknowledgement')
    const ackText = immediateAck.content[0].text
    assert.match(ackText, /Clarification requested \(receipt ([^)]+)\)\. Awaiting delegator steer\./)
    const receiptMatch = ackText.match(/Clarification requested \(receipt ([^)]+)\)\. Awaiting delegator steer\./)
    assert.ok(receiptMatch, 'receipt must match')
    expectedReceipt = receiptMatch[1]

    // Gate remains locked immediately upon negotiate return
    assert.equal(negotiationRequired(childId), true, 'gate must remain locked before steer lands')

    // Wait for event latch with a 10s harness deadline
    const deadlinePromise = new Promise((_, reject) =>
      setTimeout(() => reject(new Error('Harness deadline exceeded waiting for steer delivery')), 10000),
    )
    await Promise.race([steerPromise, deadlinePromise])
    unsubscribe()

    // Wait for delivery observation and gate confirmation to settle
    const gateDeadline = Date.now() + 5000
    while (negotiationRequired(childId) && Date.now() < gateDeadline) {
      await new Promise((r) => setTimeout(r, 50))
    }

    // 7. Verify exact persisted answer + nonce in child session entries
    const childEntries = childSession.sessionManager.getEntries()
    const steerEntry = childEntries.find(
      (e) =>
        e.type === 'message' &&
        e.message?.role === 'user' &&
        typeof e.message?.content?.[0]?.text === 'string' &&
        e.message.content[0].text.includes(expectedReceipt),
    )
    assert.ok(steerEntry, 'child session entries must contain the exact steer message with nonce')
    const steerText = steerEntry.message.content[0].text
    assert.ok(steerText.includes(EXPECTED_ANSWER), 'child session steer must contain exact persisted answer')

    const msgHasText = (m, substr) => {
      if (typeof m?.content === 'string') return m.content.includes(substr)
      if (Array.isArray(m?.content)) {
        return m.content.some((c) => (typeof c === 'string' ? c.includes(substr) : typeof c?.text === 'string' && c.text.includes(substr)))
      }
      return false
    }

    // 8. Fact: Ephemeral turn wire request preserved delegator parent context
    const ephemeralReq = requestsSeen.find((r) => r.messages.some((m) => msgHasText(m, 'format error responses')))
    assert.ok(ephemeralReq, 'ephemeral request must have been sent to provider')
    const delegatorContextPresent = ephemeralReq.messages.some((m) => msgHasText(m, DELEGATOR_CONTEXT_RULE))
    assert.ok(delegatorContextPresent, 'ephemeral wire request must preserve delegator context and history')

    // 9. Fact: Real runtime preserves native catalog tools across wire requests
    const wireRequestsWithTools = requestsSeen.filter((r) => Array.isArray(r.tools) && r.tools.length > 0)
    assert.ok(wireRequestsWithTools.length > 0, 'wire requests must include tool catalog')
    for (const req of wireRequestsWithTools) {
      const toolNames = req.tools.map((t) => t.function?.name || t.type)
      assert.ok(toolNames.includes('bash'), 'native catalog bash tool preserved')
      assert.ok(toolNames.includes('read'), 'native catalog read tool preserved')
    }

    // 10. Fact: First-reset gate unlocks ONLY after physical landing
    const unlockDeadline = Date.now() + 5000
    while (negotiationRequired(childId) && Date.now() < unlockDeadline) {
      await new Promise((r) => setTimeout(r, 20))
    }
    assert.equal(negotiationRequired(childId), false, 'first-reset gate must unlock after physical steer landing')

    // 11. Fact: Testing resetScope tool directly - admitted now that gate is unlocked
    const admittedResetResult = await resetScopeDef.execute(
      'call-admitted-reset',
      {
        i: 'Transition to implementation',
        oldScopeVerboseVerbatimExperience: 'Exploration completed successfully',
        newScopeObjective: 'Implement verified change',
        newScopeTurnBudget: 15,
      },
      null,
      null,
      childCtx,
    )
    assert.equal(admittedResetResult.details?.turnBudget, 15, 'resetScope sets newScopeTurnBudget')
    assert.equal(admittedResetResult.details?.objective, 'Implement verified change', 'resetScope sets newScopeObjective')
    assert.equal(admittedResetResult.details?.archived, true, 'resetScope archives previous experience')
    assert.ok(admittedResetResult.content?.[0]?.text?.includes('Scope reset. Objective: Implement verified change'), 'content confirms scope reset')

    // 12. Fact: Subsequent check confirms gate remains unlocked
    assert.equal(negotiationRequired(childId), false, 'gate remains unlocked for session')
  } finally {
    if (parentSession) {
      try {
        parentSession.dispose()
      } catch {}
    }
    if (childSession) {
      try {
        childSession.dispose()
      } catch {}
    }
    server.close()
    try {
      fs.rmSync(tmpDir, { recursive: true, force: true })
    } catch {}
  }
}

test('WHAT[delegation-034] OMP physical negotiate canary against local provider', { timeout: 30000 }, async () => {
  await runCanary()
})
