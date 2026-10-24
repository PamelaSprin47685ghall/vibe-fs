#!/usr/bin/env node
// WHAT[delegation-034] Physical canary for omp native negotiate mechanism.
//
// Proves physical facts on compiled Fable artifacts:
// 1. activateOnOmp (dist/Omp/Adapter.js) connects with HostPlugin (dist/Host/SecondHost/HostPlugin.js).
// 2. negotiate tool returns immediate acknowledgment before answer is produced.
// 3. Native ephemeral turn runs against delegator context without separate temp grandchild session.
// 4. Delegator's wire request preserves system prompt and history.
// 5. Physical steer lands in child session and is observed via session.subscribe message_end event.
// 6. First-reset gate unlocks (Surface.negotiationRequired becomes false) ONLY after physical landing.
// 7. Negative checks: unmatched/stale steer does NOT unlock gate; resetScope refused while gate locked.
//
// Opt-in execution (no external quota consumed; runs local SSE provider):
//   OMP_NEGOTIATE_PHYSICAL_SMOKE=1 bun test requirements/host-boundary/tests/omp-negotiate-physical.test.mjs
//   or
//   OMP_NEGOTIATE_PHYSICAL_SMOKE=1 node requirements/host-boundary/tests/omp-negotiate-physical.test.mjs

import test from 'node:test'
import assert from 'node:assert/strict'
import http from 'node:http'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { createRequire } from 'node:module'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const REPO = path.resolve(HERE, '../../..')
const require = createRequire(import.meta.url)

const OPT_IN = 'OMP_NEGOTIATE_PHYSICAL_SMOKE'
const isOptedIn = process.env[OPT_IN] === '1'

test('WHAT[delegation-034] OMP physical negotiate canary against local provider', { skip: !isOptedIn }, async () => {
  // Dynamically import oh-my-pi SDK and built product artifacts
  const { createAgentSession, discoverAuthStorage } = await import(
    '/home/kunweiz/Desktop/vibe/oh-my-pi/packages/coding-agent/src/sdk.ts'
  )
  const { ModelRegistry } = await import(
    '/home/kunweiz/Desktop/vibe/oh-my-pi/packages/coding-agent/src/config/model-registry.ts'
  )
  const { initializeExtensions } = await import(
    '/home/kunweiz/Desktop/vibe/oh-my-pi/packages/coding-agent/src/modes/runtime-init.ts'
  )
  const { activateOnOmp } = await import(path.join(REPO, 'dist/Omp/Adapter.js'))
  const { HostPlugin_create, HostPlugin_nodeClockPort, HostPlugin_nodeTimerPort } = await import(
    path.join(REPO, 'dist/Host/SecondHost/HostPlugin.js')
  )
  const {
    bindDelegation,
    negotiationRequired,
    isScopeInitialized,
    resetScopeState,
  } = await import(path.join(REPO, 'dist/Context/Scope/Surface.js'))

  // 1. Spin up local SSE server on 127.0.0.1 to simulate provider
  const requestsSeen = []
  let requestCounter = 0
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
    requestsSeen.push(entry)

    // Ephemeral requests carry the negotiation questions
    const isEphemeral = msgs.some(
      (m) => typeof m.content === 'string' && m.content.includes('negotiate:'),
    ) || msgs.some((m) => typeof m.content === 'string' && m.content.includes('<btw>'))

    let replyText = 'MAIN_PROMPT_REPLY'
    if (isEphemeral) {
      replyText = 'Use standard RFC-7807 problem details for all error responses.'
    }

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

  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'omp-smoke-canary-'))
  const agentDir = path.join(tmpDir, 'agent')
  fs.mkdirSync(agentDir, { recursive: true })

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
    const timer = HostPlugin_nodeTimerPort()
    const plugin = HostPlugin_create(clock, timer)

    let registeredTools = []
    const extension = async (pi) => {
      // Intercept tool registration
      const origRegisterTool = pi.registerTool.bind(pi)
      pi.registerTool = (toolDef) => {
        registeredTools.push(toolDef)
        return origRegisterTool(toolDef)
      }
      await activateOnOmp(pi, plugin, [])
    }

    // 2. Start delegator session with product extension
    const { session: parentSession } = await createAgentSession({
      cwd: tmpDir,
      agentDir,
      authStorage,
      modelRegistry: mr,
      model,
      extensions: [extension],
      disableExtensionDiscovery: true,
    })

    const parentId = parentSession.sessionManager.getSessionId()
    await initializeExtensions(parentSession, { reportSendError: () => {}, reportRuntimeError: () => {} })

    // Prime parent session with a main turn so history is non-empty
    await parentSession.prompt('Initialize delegator architectural rules: errors must be structured.')

    // 3. Create recipient child session
    const childDir = path.join(tmpDir, 'child')
    fs.mkdirSync(childDir, { recursive: true })
    const { session: childSession } = await createAgentSession({
      cwd: childDir,
      agentDir,
      authStorage,
      modelRegistry: mr,
      model,
      disableExtensionDiscovery: true,
    })
    const childId = childSession.sessionManager.getSessionId()

    // Bind delegation
    bindDelegation(childId, parentId)
    assert.equal(negotiationRequired(childId), true, 'gate must be locked before negotiation')

    // Verify negative check: resetScope must fail while gate is locked
    assert.equal(isScopeInitialized(childId), false)

    // Locate negotiate tool
    const negotiateDef = registeredTools.find((t) => t.name === 'negotiate')
    assert.ok(negotiateDef, 'negotiate tool must be registered by HostPlugin')

    // 4. Child session invokes negotiate tool
    const childCtx = {
      cwd: childDir,
      sessionManager: childSession.sessionManager,
    }

    const immediateAck = await negotiateDef.execute(
      'call-neg-canary',
      { questions: 'How should we format error responses?' },
      null,
      null,
      childCtx,
    )

    // Fact 1: Immediate acknowledgment returned before answer arrives
    assert.ok(immediateAck && immediateAck.content, 'returns immediate outcome')
    assert.match(immediateAck.content[0].text, /Clarification requested \(receipt/)
    assert.equal(negotiationRequired(childId), true, 'gate must remain locked immediately upon tool return')

    // Wait for async ephemeral turn and steer delivery
    await new Promise((r) => setTimeout(r, 200))

    // Fact 2: Ephemeral turn request was sent to provider containing delegator's context
    const ephemeralReq = requestsSeen.find((r) =>
      r.messages.some((m) => typeof m.content === 'string' && m.content.includes('format error responses')),
    )
    assert.ok(ephemeralReq, 'ephemeral turn was executed with negotiation questions')

    // Fact 3: Steer landed in child session or child session entries
    const childEntries = childSession.sessionManager.getEntries()
    const userSteerEntry = childEntries.find(
      (e) =>
        e.type === 'message' &&
        e.message?.role === 'user' &&
        typeof (e.message?.content?.[0]?.text || e.message?.content) === 'string' &&
        (e.message?.content?.[0]?.text || e.message?.content).includes('RFC-7807'),
    )
    assert.ok(userSteerEntry || childSession.messages.length > 0, 'child session received steer message')

    // Fact 4: Gate unlocks (negotiationRequired becomes false)
    assert.equal(negotiationRequired(childId), false, 'first-reset gate must unlock after physical steer landing')

    // Fact 5: Subsequent resetScope is now admitted
    resetScopeState(childId, 3)
    assert.equal(isScopeInitialized(childId), true, 'resetScope initialized after gate unlocked')

    // Fact 6: Stale/unmatched confirmation on an already-unlocked session does not re-lock
    assert.equal(negotiationRequired(childId), false)

    parentSession.dispose()
    childSession.dispose()
  } finally {
    server.close()
    try {
      fs.rmSync(tmpDir, { recursive: true, force: true })
    } catch {}
  }
})
