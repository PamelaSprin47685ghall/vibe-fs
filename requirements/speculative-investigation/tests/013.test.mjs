import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readdirSync, readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const root = resolve(import.meta.dirname, '../../..')

test('WHAT[speculative-investigation-013] SPEC_INV_013_the_dry_run_entry_does_not_exist_on_the_live_surface', () => {
  // `replicaAttach` is live, not a DryRun leftover: it binds an already-live
  // replica binding into the real coordinator.
  for (const name of ['startDryRun', 'observeDryRun', 'closeDryRunAtPrimaryTerminal', 'replicaCloseDryRun', 'settingsDryRunBudget']) {
    assert.equal(Strength[name], undefined, `no ${name} entry may remain on the delegation surface`)
  }
})
test('WHAT[speculative-investigation-013] SPEC_INV_013_strength_source_carries_no_live_dry_run_branch', () => {
  const strength = resolve(root, 'src/Wanxiangshu/Strength')
  const walk = (directory) => readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = resolve(directory, entry.name)
    if (entry.isDirectory()) return walk(path)
    return /\.fsi?$/.test(entry.name) ? [path] : []
  })
  const offenders = []
  for (const path of walk(strength)) {
    for (const line of readFileSync(path, 'utf8').split('\n')) {
      const trimmed = line.trim()
      if (!/DryRun/.test(trimmed)) continue
      // Historical or documentation prose may mention the retired mechanism; no
      // executable line may.
      if (trimmed.startsWith('//') || trimmed.startsWith('///') || trimmed.startsWith('(*')) continue
      offenders.push(`${path}: ${trimmed}`)
    }
  }
  assert.deepEqual(offenders, [], 'a production DryRun branch must not remain in the Strength tree')
})
test('WHAT[speculative-investigation-013] SPEC_INV_013_a_replica_is_a_real_child_session_not_a_diagnostic_stub', () => {
  const runtime = readFileSync(resolve(root, 'src/Wanxiangshu/Strength/Replica/Runtime.fs'), 'utf8')
  assert.match(runtime, /sessions\.CreateChildSession/)
  assert.match(runtime, /dispatcher\.SendAgentOwnerRootWithTools/)
  assert.match(runtime, /StrengthReplicaBinding/)
  assert.doesNotMatch(runtime, /fake|simulate|synthetic replica/i)
})
// What still needs a real Host: enumerating the final provider-visible tool
// set on a live Host, observing owner/Replica provider/model and purpose on the
// provider wire, two owners' concurrent schema decoration on one live plugin
// instance, and rejecting the legacy three-argument routingProtocol on a live
// scheduler (host-boundary-032 and the execution-model-routing canaries).
//
// The unit-observable share of [013] now has carriers below and in
// execution-model-routing-010: per-owner authorization/budget/call-id/result
// isolation (DELEGATE 14.5) and same-provider capacity of one without
// parent/child deadlock.
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { ProtocolRevision } = await import("../../../dist/Strength/InvestigationEstimateContract.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })

// DELEGATE 14.5 / WHAT[013]: two owners run concurrently. Registries,
// authorizations, budgets, batches, call-id ordinals and projection state are
// per owner; nothing they touch is global.
const ownerBinding = (owner, replica, decision, rounds) =>
  Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, 'Engineer', rounds, `sem-${decision}`, [
    { role: 'user', parts: [{ kind: 'text', text: `${owner} mirror` }] },
  ])
const oneExchange = (replica, callId, tool, args, result) => ({ messages: [
  user('u1', replica, [hostText('Continue.')]),
  assistant('a1', replica, [hostResult(callId, tool, args, result)]),
] })

test('WHAT[speculative-investigation-013] STRENGTH_013_two_owners_keep_their_own_single_flight_authorization_and_budget', () => {
  const runtime = Strength.runtimeCreate()

  // Both owners register into the one registry: single flight is per owner, so
  // owner-b registering must not be blocked by owner-a's live replica.
  assert.equal(Strength.runtimeRegister(runtime, ownerBinding('owner-a', 'replica-a', 'd-a', 3)).ok, true)
  assert.equal(Strength.runtimeRegister(runtime, ownerBinding('owner-b', 'replica-b', 'd-b', 1)).ok, true)

  const foundA = Strength.runtimeFindByReplica(runtime, 'replica-a')
  const foundB = Strength.runtimeFindByReplica(runtime, 'replica-b')
  assert.equal(foundA.decisionId, 'd-a')
  assert.equal(foundA.ownerSessionId, 'owner-a')
  assert.equal(foundA.requestedRounds, 3, 'each binding carries its own budget')
  assert.equal(foundB.decisionId, 'd-b')
  assert.equal(foundB.ownerSessionId, 'owner-b')
  assert.equal(foundB.requestedRounds, 1)

  // A second replica for the SAME owner is refused; the other owner is untouched.
  const duplicateA = Strength.runtimeRegister(runtime, ownerBinding('owner-a', 'replica-a2', 'd-a2', 3))
  assert.equal(duplicateA.ok, false)
  assert.equal(duplicateA.error, 'OwnerAlreadyHasReplica')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-b').decisionId, 'd-b')

  // Retiring owner-a's replica releases only owner-a's slot.
  assert.equal(Strength.runtimeRetire(runtime, 'replica-a').decisionId, 'd-a')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-a'), null)
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-b').requestedRounds, 1)
  assert.equal(
    Strength.runtimeRegister(runtime, ownerBinding('owner-a', 'replica-a3', 'd-a3', 3)).ok,
    true,
    'the freed slot belongs to owner-a alone',
  )
})
test('WHAT[speculative-investigation-013] STRENGTH_013_two_owners_transforms_keep_calls_batches_and_budget_apart', async () => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, ownerBinding('owner-a', 'replica-a', 'd-a', 3)).ok, true)
  assert.equal(Strength.runtimeRegister(runtime, ownerBinding('owner-b', 'replica-b', 'd-b', 1)).ok, true)

  // Interleaved transforms: each owner's request collects only its own calls.
  const a1 = await Strength.transformApply(H, runtime, oneExchange('replica-a', 'call-a1', 'read', { filePath: 'a.md' }, 'alpha'), true)
  assert.equal(a1.kind, 'Ready')
  const b1 = await Strength.transformApply(H, runtime, oneExchange('replica-b', 'call-b1', 'grep', { pattern: 'b' }, 'hit-b'), true)
  assert.equal(b1.kind, 'Ready')

  // Call ids and ordinals never leak across owners: each owner's first request
  // restarts its own ordinal and carries only its own arguments and result.
  assert.equal(a1.batches.length, 1)
  assert.equal(a1.batches[0].requestOrdinal, 1)
  assert.equal(a1.batches[0].exchanges[0].canonicalArguments, '{"filePath":"a.md"}')
  assert.equal(a1.batches[0].exchanges[0].canonicalResult, 'alpha')
  assert.equal(b1.batches.length, 1)
  assert.equal(b1.batches[0].requestOrdinal, 1, 'the second owner does not inherit a shared ordinal counter')
  assert.equal(b1.batches[0].exchanges[0].canonicalArguments, '{"pattern":"b"}')
  assert.equal(b1.batches[0].exchanges[0].canonicalResult, 'hit-b')

  // Budgets are per owner: owner-b's budget (1) is spent while owner-a (3) keeps
  // admitting its own rounds.
  const b2 = await Strength.transformApply(H, runtime, oneExchange('replica-b', 'call-b2', 'glob', { pattern: '**/*.fs' }, 'b.fs'), true)
  assert.equal(b2.kind, 'Retired')
  assert.equal(b2.reason, 'provider-request-budget-reached')
  assert.deepEqual(b2.aborted, ['replica-b'])

  const a2 = await Strength.transformApply(H, runtime, oneExchange('replica-a', 'call-a2', 'read', { filePath: 'a2.md' }, 'alpha2'), true)
  assert.equal(a2.kind, 'Ready', "owner-a's budget is not consumed by owner-b")
  assert.equal(a2.batches[0].requestOrdinal, 1)

  // Both physical identities still live until their own Host terminal.
  assert.notEqual(Strength.runtimeFindByReplica(runtime, 'replica-a'), null)
  assert.notEqual(Strength.runtimeFindByReplica(runtime, 'replica-b'), null)
})
test('WHAT[speculative-investigation-013] STRENGTH_013_two_owner_decisions_project_independently', () => {
  let projection = Strength.projectionEmpty()
  const apply = (event) => {
    const result = Strength.projectionApply(projection, event)
    assert.equal(result.ok, true, result.error)
    projection = result.value
  }
  const requested = (decision, owner, rounds) => Strength.eventRequested({
    decisionId: decision, ownerSessionId: owner,
    ownerLogicalRun: { logicalRunId: `${owner}-run`, authorityRootUserMessageId: `${owner}-root` },
    sourcePhysicalUserMessageId: `${owner}-msg`,
    sourceProviderRun: `run-${decision}`,
    sourceToolCallIds: [`call-${decision}`], requestedRounds: rounds, contractRevision: ProtocolRevision,
  })

  // Interleaved: owner-a requests, owner-b requests, owner-b binds, owner-a binds.
  apply(requested('d-a', 'owner-a', 2))
  apply(requested('d-b', 'owner-b', 5))
  apply(Strength.eventBound('d-b', 'run-d-b', 'replica-b', 'anchor-b'))
  apply(Strength.eventBound('d-a', 'run-d-a', 'replica-a', 'anchor-a'))

  const viewA = Strength.projectionCandidate('d-a', projection)
  const viewB = Strength.projectionCandidate('d-b', projection)
  assert.equal(viewA.request.ownerSessionId, 'owner-a')
  assert.equal(viewB.request.ownerSessionId, 'owner-b')
  assert.equal(viewA.state, 'Bound')
  assert.equal(viewB.state, 'Bound')
  assert.equal(viewA.binding.replicaSessionId, 'replica-a')
  assert.equal(viewB.binding.replicaSessionId, 'replica-b')
  assert.equal(Strength.projectionRequestedRounds('d-a', projection), 2)
  assert.equal(Strength.projectionRequestedRounds('d-b', projection), 5)
  assert.equal(Strength.projectionDecisionForTarget('run-d-a', projection), 'd-a')
  assert.equal(Strength.projectionDecisionForTarget('run-d-b', projection), 'd-b')

  // Closing one owner's authorization leaves the other owner's decision intact.
  apply(Strength.eventClosed('d-a', 'Bound', 'Cancelled'))
  assert.equal(Strength.projectionCandidate('d-a', projection).state, 'Closed')
  assert.equal(Strength.projectionCandidate('d-b', projection).state, 'Bound')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, readdirSync, readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const { acceptAuthorityRoot, notifyCompleted, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");
const Strength = await import("../../../dist/Strength/Surface.js");
const { ProtocolRevision } = await import("../../../dist/Strength/InvestigationEstimateContract.js");

const hostText = (text) => ({ type: 'text', text })
const hostToolCall = (callId, tool, input, output) => ({
  type: 'tool', tool, callID: callId, state: { status: 'completed', input, output },
})
const userMessage = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistantMessage = (id, sessionId, parentId, parts) => ({
  info: { id, role: 'assistant', sessionID: sessionId, parentID: parentId, time: { created: 1 } }, parts,
})
const budgetCall = (callId, rounds) =>
  hostToolCall(
    callId,
    'read',
    rounds > 0
      ? { filePath: 'a.md', estimated_readonly_rounds: rounds, self_note: 'inspect the file' }
      : { filePath: 'a.md', estimated_readonly_rounds: rounds },
    'alpha',
  )

// A tool call whose results have not all arrived: the wire view carries the
// call but no paired result, so the domain collector cannot complete a batch.
const pendingCall = (callId, rounds) => ({
  type: 'tool',
  tool: 'read',
  callID: callId,
  state: {
    status: 'pending',
    input:
      rounds > 0
        ? { filePath: 'a.md', estimated_readonly_rounds: rounds, self_note: 'inspect the file' }
        : { filePath: 'a.md', estimated_readonly_rounds: rounds },
  },
})

// The unified EventStore keeps writer NDJSON files under the workspace Git
// common directory; a DelegationRequested is a canonical JSON line whose
// event_type names the fact. Reading the durable line is the observation:
// the capture phase must have appended exactly the authorization it admitted.
const durableEventsOfType = (directory, eventType) => {
  const eventsDir = join(directory, '.git', 'wanxiang', 'events')
  if (!existsSync(eventsDir)) return []
  const events = []
  for (const name of readdirSync(eventsDir)) {
    if (!name.endsWith('.ndjson')) continue
    for (const line of readFileSync(join(eventsDir, name), 'utf8').split('\n')) {
      if (line.length === 0 || !line.includes(`"${eventType}"`)) continue
      events.push(JSON.parse(line))
    }
  }
  return events
}

const durableRequestedEvents = (directory) => durableEventsOfType(directory, 'DelegationRequested')

const tick = () => new Promise((resolve) => setImmediate(resolve))

const withTimeout = async (promise, message) => {
  let timer
  try {
    return await Promise.race([
      promise,
      new Promise((_resolve, reject) => {
        timer = setTimeout(() => reject(new Error(message)), 15000)
      }),
    ])
  } finally {
    clearTimeout(timer)
  }
}

const durableFactLineCount = (directory) => {
  const eventsDir = join(directory, '.git', 'wanxiang', 'events')
  if (!existsSync(eventsDir)) return 0
  let lines = 0
  for (const name of readdirSync(eventsDir)) {
    if (!name.endsWith('.ndjson')) continue
    lines += readFileSync(join(eventsDir, name), 'utf8').split('\n').filter((line) => line.length > 0).length
  }
  return lines
}

// WHAT[013] real-Host integration share of the delegation admission gate,
// exercised through the compiled plugin's real messages.transform hook over a
// real shared journal. One plugin incarnation serves every assertion: the
// fixture costs 275-331ms per incarnation and the runner budget is 1000ms per
// test, so the shared Predictor query is flipped mid-test instead of booting a
// second plugin.
test('WHAT[speculative-investigation-013] SPEC_INV_013_real_transform_wiring_holds_the_capture_gate_and_the_shared_predictor_query', async () => {
  globalThis.__wanxiangshu_test_predictor_state = 'configured'
  try {
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const sessionId = 'ses-delegate-capture'
      const physical = 'user-capture-1'
      // Production order: the managed admission hook runs before the transform
      // for the same physical user message.
      await acceptAuthorityRoot(runtime, sessionId, 'engineer', physical)
      const admissionMessage = userMessage(physical, sessionId, [hostText('inspect the file')])
      await hooks['chat.message']({ sessionID: sessionId, messageID: physical, agent: 'engineer' }, { message: admissionMessage, parts: admissionMessage.parts })

      // The process-shared Predictor existence query is observed per call, not
      // frozen at plugin construction: tool decoration sees the configured
      // state and decorates participating tools per DELEGATE_REVISE.md contract.
      const previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
      try {
        process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
        const schemaOutput = {
          description: 'read a file',
          parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
        }
        await hooks['tool.definition']({ toolID: 'read' }, schemaOutput)

        const props = schemaOutput.parameters.properties
        assert.equal(props.estimated_readonly_rounds?.type, 'integer')
        assert.equal(props.estimated_readonly_rounds?.minimum, 0)
        assert.equal(props.estimated_readonly_rounds?.maximum, 2147483647)
        assert.equal(
          props.estimated_readonly_rounds?.description,
          "Estimate how many consecutive read-only investigation rounds will still be needed after ALL tool calls in this response have completed, before a substantive change, a command, user clarification, a conclusion, or a consequential judgment that you must make yourself. One round is one model request and may contain several parallel tool calls; do not count the current batch. Routine choices about which reference or file to inspect are part of investigation. Use 0 when no such investigation remains or the next step already reaches one of those boundaries. Give your current best estimate; it need not be exact, and do not add work to match it.",
          'budget description must match English verbatim constant',
        )
        assert.ok(
          schemaOutput.parameters.required.includes('estimated_readonly_rounds'),
          'read must require estimated_readonly_rounds',
        )

        assert.equal(props.self_note?.type, 'string')
        assert.equal(
          props.self_note?.description,
          "Provide this field only when this call's estimated_readonly_rounds is greater than 0; otherwise omit the field entirely, without an empty string or null. For a positive estimate, leave a brief, non-empty outlook for the next investigation rounds: what evidence or relationships to inspect and what finding will make the next step possible. One to three sentences are enough. Do not provide a progress report, generic filler, instructions to another worker, or a full reasoning trace.",
          'self_note description must match English verbatim constant',
        )
        assert.equal('minLength' in props.self_note, false, 'self_note must not set minLength')
        assert.equal(
          schemaOutput.parameters.required.includes('self_note'),
          false,
          'self_note must not be required',
        )

        assert.equal(
          props.delegate_readonly_rounds,
          undefined,
          'legacy delegate_readonly_rounds must not be present',
        )
        assert.equal(
          schemaOutput.parameters.required.includes('delegate_readonly_rounds'),
          false,
          'legacy delegate_readonly_rounds must not be required',
        )
        assert.ok(schemaOutput.description.startsWith('read a file'))
        assert.ok(schemaOutput.description.includes("Investigation outlook: estimated_readonly_rounds estimates the consecutive read-only investigation rounds after the current batch. Include self_note only for a positive estimate, stating what to inspect next and what finding will make the next step possible; omit the note for 0."))
        assert.equal(schemaOutput.description.includes('delegate_readonly_rounds'), false)
        assert.equal(/companion|trust|retain control|同伴|信任|保留控制权/i.test(schemaOutput.description), false)

        // Idempotency: repeating decoration on the same definition does not duplicate required fields or stack prose
        await hooks['tool.definition']({ toolID: 'read' }, schemaOutput)
        assert.equal(
          schemaOutput.parameters.required.filter((x) => x === 'estimated_readonly_rounds').length,
          1,
          'estimated_readonly_rounds must appear exactly once in required after repeat decoration',
        )
        assert.equal(
          schemaOutput.description.split("Investigation outlook: estimated_readonly_rounds estimates the consecutive read-only investigation rounds after the current batch. Include self_note only for a positive estimate, stating what to inspect next and what finding will make the next step possible; omit the note for 0.").length - 1,
          1,
          'English collaboration prose must be appended exactly once and not stack',
        )

        // Chinese language binding test: verbatim Chinese descriptions from ReadonlyDelegationContract.fs
        process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'zh-CN'
        const schemaOutputZh = {
          description: '读取文件',
          parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
        }
        await hooks['tool.definition']({ toolID: 'read' }, schemaOutputZh)
        const zhProps = schemaOutputZh.parameters.properties
        assert.equal(zhProps.estimated_readonly_rounds?.type, 'integer')
        assert.equal(zhProps.estimated_readonly_rounds?.minimum, 0)
        assert.equal(zhProps.estimated_readonly_rounds?.maximum, 2147483647)
        assert.equal(
          zhProps.estimated_readonly_rounds?.description,
          "当前响应的全部工具执行完成后，预计还需要连续进行多少轮只读查证，才会到达实质修改、执行命令、向用户确认、给出结论，或必须亲自权衡的关键判断？一轮是一次模型请求，可以包含多个并行工具调用；当前这批不计入。选择接着查哪个文件或引用属于普通调查，不必一概当成关键判断。已经没有后续查证，或下一步就到达上述边界时，填 0。按当前材料估计即可，不要求精确，也不要为了符合估计增加调查。",
          'budget description must match Chinese verbatim constant',
        )
        assert.equal(zhProps.self_note?.type, 'string')
        assert.equal(
          zhProps.self_note?.description,
          "仅当本次调用的 estimated_readonly_rounds 大于 0 时填写；否则完全省略本字段，不填空串或 null。正数时，用一至三句话给自己留下后续调查的展望：准备核对哪些材料或关系，什么证据出现后可以进入下一步。不要写完成情况、泛泛感想、对其他执行者的指令或完整思考过程。",
          'self_note description must match Chinese verbatim constant',
        )
        assert.ok(schemaOutputZh.description.includes("调查展望：estimated_readonly_rounds 估计当前整批完成后的连续只读查证轮数。只在本次估计大于 0 时填写 self_note，简述接下来查什么、查到什么即可进入下一步；估计为 0 时省略短记。"))
        assert.equal(schemaOutputZh.description.includes("Investigation outlook: estimated_readonly_rounds estimates the consecutive read-only investigation rounds after the current batch. Include self_note only for a positive estimate, stating what to inspect next and what finding will make the next step possible; omit the note for 0."), false)
        if (previousLanguage === undefined) {
          delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
        } else {
          process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
        }

        // Per-tool verification: a non-participating tool (e.g. join) and unreviewed tool gain zero increment
        const joinOutput = {
          description: 'Original join description',
          parameters: { type: 'object', properties: {}, required: [] },
        }
        await hooks['tool.definition']({ toolID: 'join' }, joinOutput)
        assert.equal(joinOutput.parameters.properties?.estimated_readonly_rounds, undefined)
        assert.equal(joinOutput.parameters.properties?.self_note, undefined)
        assert.equal(joinOutput.parameters.required.includes('estimated_readonly_rounds'), false)
        assert.equal(joinOutput.description, 'Original join description')

        // chronicle (Blogger) is an explicit NoEstimate tool: verify zero increment through production hook
        const chronicleOutput = {
          description: 'Record narrative events into the chronicle',
          parameters: { type: 'object', properties: { note: { type: 'string' } }, required: ['note'] },
        }
        await hooks['tool.definition']({ toolID: 'chronicle' }, chronicleOutput)
        assert.ok(chronicleOutput, 'chronicle tool definition must exist')
        assert.equal(chronicleOutput.parameters.properties?.estimated_readonly_rounds, undefined, 'chronicle must not contain estimated_readonly_rounds')
        assert.equal(chronicleOutput.parameters.properties?.self_note, undefined, 'chronicle must not contain self_note')
        assert.equal(chronicleOutput.parameters.required?.includes('estimated_readonly_rounds'), false, 'chronicle required must not contain estimated_readonly_rounds')
        assert.equal(chronicleOutput.parameters.required?.includes('self_note'), false, 'chronicle required must not contain self_note')
        assert.equal(chronicleOutput.description, 'Record narrative events into the chronicle', 'chronicle description must strictly equal original description')

        const unreviewedOutput = {
          description: 'custom unreviewed tool',
          parameters: { type: 'object', properties: {}, required: [] },
        }
        await hooks['tool.definition']({ toolID: 'custom-unreviewed' }, unreviewedOutput)
        assert.equal(unreviewedOutput.parameters.properties?.estimated_readonly_rounds, undefined)
        assert.equal(unreviewedOutput.parameters.properties?.self_note, undefined)
        assert.equal(unreviewedOutput.parameters.required.includes('estimated_readonly_rounds'), false)
        assert.equal(unreviewedOutput.description, 'custom unreviewed tool')

        // js-manager participating tool: review contract and delegation protocol coexist without mutual interference
        const managerOutput = {
          description: 'Review workspace changes as manager',
          parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
        }
        await hooks['tool.definition']({ toolID: 'js-manager' }, managerOutput)
        const mgrProps = managerOutput.parameters.properties
        assert.equal(mgrProps.estimated_readonly_rounds?.type, 'integer')
        assert.equal(mgrProps.estimated_readonly_rounds?.minimum, 0)
        assert.equal(mgrProps.estimated_readonly_rounds?.maximum, 2147483647)
        assert.ok(managerOutput.parameters.required.includes('estimated_readonly_rounds'))
        assert.equal(
          managerOutput.parameters.required.filter((x) => x === 'estimated_readonly_rounds').length,
          1,
          'estimated_readonly_rounds must appear exactly once in required',
        )
        assert.equal(mgrProps.self_note?.type, 'string')
        assert.equal(managerOutput.parameters.required.includes('self_note'), false)
        assert.equal(mgrProps.contract?.type, 'string', 'review contract property must coexist with delegation protocol')
        assert.ok(managerOutput.parameters.required.includes('contract'), 'js-manager must require review contract beside delegation protocol')
        assert.equal(
          managerOutput.parameters.required.filter((x) => x === 'contract').length,
          1,
          'js-manager must require contract exactly once',
        )
        assert.ok(
          managerOutput.description.includes(
            'Investigation outlook: estimated_readonly_rounds estimates the consecutive read-only investigation rounds after the current batch. Include self_note only for a positive estimate, stating what to inspect next and what finding will make the next step possible; omit the note for 0.',
          ),
          'js-manager description must include collaboration prose',
        )
      } finally {
        if (previousLanguage === undefined) {
          delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
        } else {
          process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
        }
      }

      // Drive the real owner transform with a complete, fresh tool batch whose
      // only call requests one readonly round.
      const run = 'run-capture-1'
      const seedUser = userMessage(physical, sessionId, [hostText('inspect the file')])
      const seedAssistant = assistantMessage(run, sessionId, physical, [budgetCall('call-capture-1', 1)])
      runtime.pushHostMessage(sessionId, seedUser)
      runtime.pushHostMessage(sessionId, seedAssistant)
      const outObj = { messages: [seedUser, seedAssistant] }
      await hooks['experimental.chat.messages.transform']({}, outObj)

      // Positive control: the real pipeline appended durable facts (authority
      // root, manager road, XTrace capture, attempt plan freeze), so the
      // observation path below is live and the zero-count assertion cannot
      // pass vacuously against a wrong directory.
      assert.ok(
        durableFactLineCount(directory) > 0,
        'the real transform pipeline must leave durable facts behind, or the NDJSON observation path is wrong',
      )

      // The adapter fixture cannot produce a durable (Work, Root) session
      // association, so this session is not a proven root Work continuation and
      // the capture gate must refuse it: zero authorization, even with the
      // Predictor configured and a complete budget-1 batch on the wire.
      //
      // Why the positive set (exactly one DelegationRequested with the derived
      // DecisionId, RequestedRounds = 1 and the frozen call ids, idempotent on
      // repeat, conflicting on a changed budget) is asserted in the test below.
      //
      // 1. The association writer was traced: SatelliteRuntime.fs:149 builds
      //    AgentFact.Companion(CompanionBloggerLinked) (bridge: CompanionFact.fs:9),
      //    folded into AgentProjection.Associations (CompanionFactFold.fs →
      //    Composition/Durable/Projection.fs:80/136). classifyLegacy maps
      //    WorkSession → Work × Root (Association.fs:275) and the root session's
      //    link carries ParentSessionId = None, so it is a legal production
      //    precondition. Association.fs:88-91 documents the lazy-creation order:
      //    "no record yet" is the state the NEXT transform resolves — and the
      //    companion step (transform step 8) runs after the capture step (4.5),
      //    so one drive cannot both establish and use the association.
      // 2. A second drive would reach the start phase, which awaits
      //    preparation.Completion. Only the replica turn observation resolves it
      //    (Replica/Runtime.fs observeReplicaTurn). HandlePreTurn is captured
      //    EAGERLY in PluginStrengthPorts.create (PluginStrengthPorts.fs:28-33)
      //    BEFORE PluginSessionWiring.attach installs the replica runtime
      //    (SpikePlugin.fs:37-38), and HostSignalBootstrap.fs:144-152 passes the
      //    captured None straight into the turn observer. If that reading holds,
      //    replica terminals never reach the runtime in production either and
      //    the owner transform would hang after SendPreparedPrompt — a suspected
      // product defect, reported, not fixed here.
      //
      // Resolved: drive 1 establishes the association through the production
      // companion step, and the replica completion now resolves through the
      // late-bound HandlePreTurn (PluginStrengthPorts.fs reads the live scope
      // at event time instead of capturing the runtime before attach). The
      // positive set is asserted in the test below.
      assert.deepEqual(
        durableRequestedEvents(directory),
        [],
        'a session without a durable root Work association may never be authorized',
      )

      // Removing the Predictor configuration takes effect on the very next
      // observation of the shared query: no decoration, and still no
      // authorization from the same real wiring.
      globalThis.__wanxiangshu_test_predictor_state = 'unconfigured'
      const bareOutput = {
        description: 'read a file',
        parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
      }
      await hooks['tool.definition']({ toolID: 'read' }, bareOutput)
      assert.equal(
        bareOutput.parameters.properties.estimated_readonly_rounds,
        undefined,
        'an unconfigured Predictor decorates no estimated_readonly_rounds',
      )
      assert.equal(
        bareOutput.parameters.properties.self_note,
        undefined,
        'an unconfigured Predictor decorates no self_note',
      )
      assert.equal(
        bareOutput.parameters.properties.delegate_readonly_rounds,
        undefined,
        'legacy delegate_readonly_rounds must not be added',
      )
      assert.equal(
        bareOutput.parameters.required.includes('estimated_readonly_rounds'),
        false,
        'an unconfigured Predictor does not require estimated_readonly_rounds',
      )
      assert.equal(
        bareOutput.description,
        'read a file',
        'an unconfigured Predictor leaves tool description unmodified',
      )
      await hooks['experimental.chat.messages.transform']({}, { messages: [seedUser, seedAssistant] })
      assert.deepEqual(
        durableRequestedEvents(directory),
        [],
        'removing the Predictor configuration produces no new authorization',
      )
    })
  } finally {
    globalThis.__wanxiangshu_test_predictor_state = 'unconfigured'
  }
})

// WHAT[013] positive capture set through the same real wiring. Two drives on
// one session: drive 1 lets the production companion step write the durable
// (Work, Root) association; drive 2 is the drive the capture gate admits. The
// fixture has no provider, so the replica completion is unblocked through the
// same opaque terminal port the plugin subscribed to (`notifyCompleted`),
// which reaches the replica runtime through the now late-bound
// HandlePreTurn.
test('WHAT[speculative-investigation-013] SPEC_INV_013_real_transform_captures_one_delegation_request_for_a_root_work_session', async () => {
  globalThis.__wanxiangshu_test_predictor_state = 'configured'
  try {
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const sessionId = 'ses-delegate-positive'
      const physical = 'user-positive-1'
      await acceptAuthorityRoot(runtime, sessionId, 'engineer', physical)
      const admission = userMessage(physical, sessionId, [hostText('inspect the file')])
      await hooks['chat.message']({ sessionID: sessionId, messageID: physical, agent: 'engineer' }, { message: admission, parts: admission.parts })

      const run = 'run-positive-1'
      const callA = 'call-positive-1'
      const callB = 'call-positive-2'
      const seedUser = userMessage(physical, sessionId, [hostText('inspect the file')])
      const seedAssistant = assistantMessage(run, sessionId, physical, [budgetCall(callA, 1), budgetCall(callB, 1)])
      runtime.pushHostMessage(sessionId, seedUser)
      runtime.pushHostMessage(sessionId, seedAssistant)

      await withTimeout(
        hooks['experimental.chat.messages.transform']({}, { messages: [seedUser, seedAssistant] }),
        'transform drive 1 hung',
      )
      assert.deepEqual(
        durableRequestedEvents(directory),
        [],
        'drive 1 only establishes the root association; it authorizes nothing',
      )

      const promptsBefore = runtime.prompts.length
      const pending = hooks['experimental.chat.messages.transform']({}, { messages: [seedUser, seedAssistant] })

      let waited = 0
      while (runtime.prompts.length <= promptsBefore && waited < 5000) {
        await tick()
        waited += 1
      }
      assert.ok(runtime.prompts.length > promptsBefore, 'the start phase must dispatch the replica bootstrap prompt')

      const bootstrap = runtime.prompts[runtime.prompts.length - 1]
      const replicaSessionId = bootstrap?.path?.id ?? bootstrap?.sessionID ?? bootstrap?.sessionId
      assert.ok(replicaSessionId, 'the replica child session id must be observable from the dispatched prompt')

      await notifyCompleted(runtime, replicaSessionId, 'readonly investigation finished', 'finished', 7)
      await withTimeout(pending, 'transform drive 2 hung: the replica completion never resolved')

      const requested = durableRequestedEvents(directory)
      assert.equal(requested.length, 1, 'exactly one DelegationRequested may be captured for one source batch')
      const payload = requested[0].payload
      assert.equal(payload.owner_session_id, sessionId)
      assert.equal(payload.source_physical_user_message_id, physical)
      assert.equal(payload.source_provider_run, run)
      assert.deepEqual(
        payload.source_tool_call_ids,
        [callA, callB],
        'the frozen call set keeps the original batch order',
      )
      assert.equal(payload.requested_rounds, 1, 'the batch maximum is the authorized budget')
      assert.equal(payload.contract_revision, ProtocolRevision)

      const sha256 = (text) => createHash('sha256').update(text).digest('hex')
      const expectedDecisionId = Strength.delegationDeriveDecisionId(
        sha256,
        ProtocolRevision,
        payload.logical_run_id,
        payload.authority_root_user_message_id,
        payload.source_provider_run,
      )
      assert.equal(
        payload.decision_id,
        expectedDecisionId,
        'DecisionId must follow the deterministic derivation from contract revision, owner logical run and source provider run',
      )

      const bound = durableEventsOfType(directory, 'DelegationBound')
      assert.equal(bound.length, 1, 'the start phase must bind exactly one replica for the decision')
      assert.equal(bound[0].payload.decision_id, payload.decision_id)
      assert.equal(bound[0].payload.target_provider_run, run)
    })
  } finally {
    globalThis.__wanxiangshu_test_predictor_state = 'unconfigured'
  }
})

// WHAT[002] / DELEGATE 5.2: a source batch is real only when the completeness
// law holds — every call paired with exactly one result before the next
// request boundary. This is the regression for the capture completeness gate
// (`tailBatchIsComplete` in Strength/OpenCode/Delegate.fs): remove the gate
// and the in-flight batch below becomes the source and mints exactly one
// DelegationRequested, so this test goes red.
test('WHAT[speculative-investigation-013] SPEC_INV_013_real_transform_refuses_an_in_flight_source_batch', async () => {
  globalThis.__wanxiangshu_test_predictor_state = 'configured'
  try {
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const sessionId = 'ses-delegate-inflight'
      const physical = 'user-inflight-1'
      await acceptAuthorityRoot(runtime, sessionId, 'engineer', physical)
      const admission = userMessage(physical, sessionId, [hostText('inspect the file')])
      await hooks['chat.message']({ sessionID: sessionId, messageID: physical, agent: 'engineer' }, { message: admission, parts: admission.parts })

      const run = 'run-inflight-1'
      const seedUser = userMessage(physical, sessionId, [hostText('inspect the file')])
      const seedAssistant = assistantMessage(run, sessionId, physical, [
        pendingCall('call-inflight-1', 1),
        pendingCall('call-inflight-2', 1),
      ])
      runtime.pushHostMessage(sessionId, seedUser)
      runtime.pushHostMessage(sessionId, seedAssistant)

      // Drive 1: no root association exists yet at the capture step, so the
      // in-flight batch authorizes nothing; the companion step writes the
      // association. Drive 2 therefore meets an otherwise-admitted gate with
      // only the completeness law standing between the batch and an
      // authorization — and that is exactly the pair this test pins.
      await withTimeout(
        hooks['experimental.chat.messages.transform']({}, { messages: [seedUser, seedAssistant] }),
        'in-flight test drive 1 hung',
      )
      assert.deepEqual(durableRequestedEvents(directory), [], 'drive 1 authorizes nothing')

      await withTimeout(
        hooks['experimental.chat.messages.transform']({}, { messages: [seedUser, seedAssistant] }),
        'in-flight test drive 2 hung',
      )
      assert.deepEqual(
        durableRequestedEvents(directory),
        [],
        'an in-flight source batch (calls without every result paired) must never be authorized',
      )
    })
  } finally {
    globalThis.__wanxiangshu_test_predictor_state = 'unconfigured'
  }
})

// WHAT[002] / DELEGATE 10.3: when the wire contains both an earlier complete
// batch and a later incomplete batch carrying calls with identical tool name
// and arguments whose results have not arrived yet, source resolution must not
// misidentify the incomplete trailing batch as a completed source.
test('WHAT[speculative-investigation-013] SPEC_INV_013_real_transform_refuses_to_misidentify_incomplete_trailing_batch_matching_earlier_batch', async () => {
  globalThis.__wanxiangshu_test_predictor_state = 'configured'
  try {
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const sessionId = 'ses-delegate-tail-mismatch'
      const physical = 'user-mismatch-1'
      await acceptAuthorityRoot(runtime, sessionId, 'engineer', physical)
      const admission = userMessage(physical, sessionId, [hostText('inspect the file')])
      await hooks['chat.message']({ sessionID: sessionId, messageID: physical, agent: 'engineer' }, { message: admission, parts: admission.parts })

      // Earlier complete batch with budget 1
      const run1 = 'run-complete-1'
      const call1 = 'call-complete-1'
      const seedUser1 = userMessage(physical, sessionId, [hostText('inspect the file')])
      const seedAssistant1 = assistantMessage(run1, sessionId, physical, [budgetCall(call1, 1)])

      // Later incomplete batch with identical tool name and arguments, but different call ID and pending result
      const run2 = 'run-inflight-2'
      const call2 = 'call-inflight-2'
      const physical2 = 'user-mismatch-2'
      const admission2 = userMessage(physical2, sessionId, [hostText('inspect the file again')])
      await hooks['chat.message']({ sessionID: sessionId, messageID: physical2, agent: 'engineer' }, { message: admission2, parts: admission2.parts })
      const seedUser2 = userMessage(physical2, sessionId, [hostText('inspect the file again')])
      const seedAssistant2 = assistantMessage(run2, sessionId, physical2, [pendingCall(call2, 1)])

      runtime.pushHostMessage(sessionId, seedUser1)
      runtime.pushHostMessage(sessionId, seedAssistant1)
      runtime.pushHostMessage(sessionId, seedUser2)
      runtime.pushHostMessage(sessionId, seedAssistant2)

      const messages = [seedUser1, seedAssistant1, seedUser2, seedAssistant2]

      // Drive 1: establishes root association, authorizes nothing
      await withTimeout(
        hooks['experimental.chat.messages.transform']({}, { messages }),
        'trailing mismatch test drive 1 hung',
      )
      assert.deepEqual(durableRequestedEvents(directory), [], 'drive 1 authorizes nothing')

      // Drive 2: the trailing assistant batch is incomplete (result not arrived),
      // even though its tool name and arguments match the earlier completed batch.
      // Source capture must not misidentify the incomplete batch as completed,
      // and must not fall back to the earlier batch.
      await withTimeout(
        hooks['experimental.chat.messages.transform']({}, { messages }),
        'trailing mismatch test drive 2 hung',
      )
      assert.deepEqual(
        durableRequestedEvents(directory),
        [],
        'an incomplete trailing assistant batch with duplicate call arguments must not be captured as completed source',
      )
    })
  } finally {
    globalThis.__wanxiangshu_test_predictor_state = 'unconfigured'
  }
})
}

import { spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { OPENCODE_BIN } from '../../verification-system/tests/e2e/support/process-host-utils.js'

{
const { default: assert } = await import('node:assert/strict')
const { parseParticipatingArguments, ProtocolRevision, EstimatedReadonlyRoundsField } = await import('../../../dist/Strength/InvestigationEstimateContract.js')
const PluginHooksSurface = await import('../../../dist/OpenCode/Host/PluginHooksSurface.js')

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../..')
const runnerPath = path.join(here, '../../host-boundary/tests/support/run-readonly-delegation-schema-canary.mjs')

const builtins = ['read', 'glob', 'grep', 'edit', 'write']

const PARTICIPATING_TOOLS = new Set([
  'read', 'glob', 'grep', 'js-manager', 'js-engineer', 'js-devops',
  'edit', 'write', 'mv', 'rm', 'fetch', 'run',
])

const readonlyRoundsDescriptionEn =
  "Estimate how many consecutive read-only investigation rounds will still be needed after ALL tool calls in this response have completed, before a substantive change, a command, user clarification, a conclusion, or a consequential judgment that you must make yourself. One round is one model request and may contain several parallel tool calls; do not count the current batch. Routine choices about which reference or file to inspect are part of investigation. Use 0 when no such investigation remains or the next step already reaches one of those boundaries. Give your current best estimate; it need not be exact, and do not add work to match it."

const selfNoteDescriptionEn =
  "Provide this field only when this call's estimated_readonly_rounds is greater than 0; otherwise omit the field entirely, without an empty string or null. For a positive estimate, leave a brief, non-empty outlook for the next investigation rounds: what evidence or relationships to inspect and what finding will make the next step possible. One to three sentences are enough. Do not provide a progress report, generic filler, instructions to another worker, or a full reasoning trace."

const englishCollaboration =
  "Investigation outlook: estimated_readonly_rounds estimates the consecutive read-only investigation rounds after the current batch. Include self_note only for a positive estimate, stating what to inspect next and what finding will make the next step possible; omit the note for 0."

const readonlyRoundsDescriptionZh =
  "当前响应的全部工具执行完成后，预计还需要连续进行多少轮只读查证，才会到达实质修改、执行命令、向用户确认、给出结论，或必须亲自权衡的关键判断？一轮是一次模型请求，可以包含多个并行工具调用；当前这批不计入。选择接着查哪个文件或引用属于普通调查，不必一概当成关键判断。已经没有后续查证，或下一步就到达上述边界时，填 0。按当前材料估计即可，不要求精确，也不要为了符合估计增加调查。"

const selfNoteDescriptionZh =
  "仅当本次调用的 estimated_readonly_rounds 大于 0 时填写；否则完全省略本字段，不填空串或 null。正数时，用一至三句话给自己留下后续调查的展望：准备核对哪些材料或关系，什么证据出现后可以进入下一步。不要写完成情况、泛泛感想、对其他执行者的指令或完整思考过程。"

const chineseCollaboration =
  "调查展望：estimated_readonly_rounds 估计当前整批完成后的连续只读查证轮数。只在本次估计大于 0 时填写 self_note，简述接下来查什么、查到什么即可进入下一步；估计为 0 时省略短记。"

const checkOpencodeExecutable = () => {
  if (process.env.OPENCODE_BIN && existsSync(process.env.OPENCODE_BIN)) return true
  const localBin = path.join(repoRoot, 'node_modules/.bin/opencode')
  if (existsSync(localBin)) return true
  return existsSync(OPENCODE_BIN)
}

/// WHAT[speculative-investigation-013]: with a Predictor model configured, the
/// plugin decorates the definitions the real Host hands it. A Host built-in
/// tool carries an Effect argument schema and no JSON schema, so the protocol
/// must be published through the provider-visible JSON schema the plugin
/// renders itself — and the built-in must still execute with its original
/// arguments, with the wire history keeping the call the model actually made.
integrationTest(
  'WHAT[speculative-investigation-013] SPEC_INV_013_configured_predictor_decorates_builtin_tools_on_real_host',
  async (t) => {
    if (!checkOpencodeExecutable()) {
      return t.skip(`OpenCode binary not executable at ${OPENCODE_BIN}; requires a real OpenCode host`)
    }

    const launched = spawnSync(process.execPath, [runnerPath], {
      cwd: repoRoot,
      encoding: 'utf8',
      timeout: 180000,
      env: { ...process.env },
    })

    if (launched.status !== 0) {
      console.error(launched.stdout)
      console.error(launched.stderr)
    }
    assert.equal(launched.status, 0, 'the readonly delegation schema canary must run to completion')

    const marker = 'READONLY_DELEGATION_SCHEMA_CANARY '
    const line = launched.stdout
      .split('\n')
      .find((entry) => entry.startsWith(marker))

    assert.ok(line, `the canary must report a summary line; stdout was: ${launched.stdout.slice(-2000)}`)

    const summary = JSON.parse(line.slice(marker.length))

    for (const tool of builtins) {
      assert.ok(
        summary.visibleTools.includes(tool),
        `built-in ${tool} must be visible on the provider wire; observed ${summary.visibleTools.join(', ')}`,
      )
    }

    for (const [name, view] of Object.entries(summary.tools)) {
      // 1. Legacy protocol field delegate_readonly_rounds must never appear on any tool
      assert.equal(
        view.properties.includes('delegate_readonly_rounds'),
        false,
        `${name} must not expose legacy delegate_readonly_rounds`,
      )
      assert.equal(
        view.required.includes('delegate_readonly_rounds'),
        false,
        `${name} must not require legacy delegate_readonly_rounds`,
      )

      // 2. Companion/trust narrative must never appear in any description
      assert.equal(
        /companion|trust|retain control|同伴|信任|保留控制权/i.test(view.description),
        false,
        `${name} description must not contain companion or trust narrative`,
      )

      if (PARTICIPATING_TOOLS.has(name)) {
        // Participating tools (12 tools): must carry estimated_readonly_rounds & self_note
        assert.ok(
          view.properties.includes('estimated_readonly_rounds'),
          `participating tool ${name} must expose estimated_readonly_rounds on the provider wire`,
        )
        assert.ok(
          view.required.includes('estimated_readonly_rounds'),
          `participating tool ${name} must require estimated_readonly_rounds`,
        )
        assert.equal(
          view.required.filter((entry) => entry === 'estimated_readonly_rounds').length,
          1,
          `participating tool ${name} must require estimated_readonly_rounds exactly once`,
        )

        assert.ok(
          view.properties.includes('self_note'),
          `participating tool ${name} must expose self_note on the provider wire`,
        )
        assert.equal(
          view.required.includes('self_note'),
          false,
          `participating tool ${name} must not require self_note`,
        )
        assert.equal(
          'minLength' in (view.note ?? {}),
          false,
          `participating tool ${name} self_note must not set minLength`,
        )

        assert.equal(view.budget?.type, 'integer', `${name} estimated_readonly_rounds must be an integer`)
        assert.equal(view.budget?.minimum, 0, `${name} estimated_readonly_rounds minimum must be 0`)
        assert.equal(view.budget?.maximum, 2147483647, `${name} estimated_readonly_rounds maximum must be 2147483647`)
        assert.equal(view.note?.type, 'string', `${name} self_note must be a string`)

        // Verbatim description checks for participating tools matching ReadonlyDelegationContract.fs
        const isChinese =
          view.budget?.description === readonlyRoundsDescriptionZh ||
          view.description.includes('调查展望：estimated_readonly_rounds')
        if (isChinese) {
          assert.equal(
            view.budget?.description,
            readonlyRoundsDescriptionZh,
            `${name} budget description must match Chinese verbatim constant`,
          )
          assert.equal(
            view.note?.description,
            selfNoteDescriptionZh,
            `${name} self_note description must match Chinese verbatim constant`,
          )
          assert.ok(
            view.description.includes(chineseCollaboration),
            `${name} description must contain Chinese collaboration prose`,
          )
          assert.equal(
            view.description.split(chineseCollaboration).length - 1,
            1,
            `${name} Chinese collaboration prose must be appended exactly once (idempotent)`,
          )
        } else {
          assert.equal(
            view.budget?.description,
            readonlyRoundsDescriptionEn,
            `${name} budget description must match English verbatim constant`,
          )
          assert.equal(
            view.note?.description,
            selfNoteDescriptionEn,
            `${name} self_note description must match English verbatim constant`,
          )
          assert.ok(
            view.description.includes(englishCollaboration),
            `${name} description must contain English collaboration prose`,
          )
          assert.equal(
            view.description.split(englishCollaboration).length - 1,
            1,
            `${name} English collaboration prose must be appended exactly once (idempotent)`,
          )
        }

        // Idempotent and stable prose: no volatile tokens, remaining counts, timestamps, or tiers
        assert.equal(
          /remaining rounds|\b\d{4}-\d{2}-\d{2}\b|model tier/i.test(view.description),
          false,
          `${name} description must not contain volatile counters, timestamps, or tiers`,
        )

        // Note: js-manager is a manager review tool not issued on the engineer provider wire,
        // so summary.tools contains the engineer session tools. Its review contract and delegation
        // coexistence invariant is verified via direct decoration below.
      } else {
        // Non-participating tools (including 28 NoEstimate tools and unreviewed Host tools such as question/todo/web):
        // Must carry ZERO increment: no protocol properties, not required, no collaboration prose.
        assert.equal(
          view.properties.includes('estimated_readonly_rounds'),
          false,
          `non-participating/unreviewed tool ${name} must not gain estimated_readonly_rounds`,
        )
        assert.equal(
          view.properties.includes('self_note'),
          false,
          `non-participating/unreviewed tool ${name} must not gain self_note`,
        )
        assert.equal(
          view.required.includes('estimated_readonly_rounds'),
          false,
          `non-participating/unreviewed tool ${name} must not require estimated_readonly_rounds`,
        )
        assert.equal(
          view.required.includes('self_note'),
          false,
          `non-participating/unreviewed tool ${name} must not require self_note`,
        )
        assert.equal(view.budget, null, `non-participating tool ${name} budget view must be null`)
        assert.equal(view.note, null, `non-participating tool ${name} note view must be null`)
        assert.equal(
          view.description.includes(englishCollaboration),
          false,
          `non-participating tool ${name} must not append English collaboration prose`,
        )
        assert.equal(
          view.description.includes(chineseCollaboration),
          false,
          `non-participating tool ${name} must not append Chinese collaboration prose`,
        )
      }
    }

    // For js-manager: review contract and delegation protocol coexist without mutual interference.
    // Because the canary runner session runs as engineer, js-manager (a manager review tool) is not present
    // on the engineer wire tools. We directly execute the real tool definition decoration path for js-manager.
    const managerDef = {
      description: 'Review workspace changes as manager',
      parameters: {
        type: 'object',
        properties: { path: { type: 'string' } },
        required: ['path'],
      },
    }
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('js-manager', managerDef)
    PluginHooksSurface.decorateReviewToolDefinition('js-manager', managerDef)

    assert.ok(
      managerDef.parameters.properties.contract,
      'js-manager must expose review contract property beside delegation protocol',
    )
    assert.equal(managerDef.parameters.properties.contract.type, 'string')
    assert.ok(
      managerDef.parameters.required.includes('contract'),
      'js-manager must require review contract beside delegation protocol',
    )
    assert.equal(
      managerDef.parameters.required.filter((entry) => entry === 'contract').length,
      1,
      'js-manager must require contract exactly once',
    )
    assert.ok(
      managerDef.parameters.properties.estimated_readonly_rounds,
      'js-manager must expose estimated_readonly_rounds beside review contract',
    )
    assert.equal(managerDef.parameters.properties.estimated_readonly_rounds.type, 'integer')
    assert.ok(
      managerDef.parameters.required.includes('estimated_readonly_rounds'),
      'js-manager must require estimated_readonly_rounds',
    )
    assert.equal(
      managerDef.parameters.required.filter((entry) => entry === 'estimated_readonly_rounds').length,
      1,
      'js-manager must require estimated_readonly_rounds exactly once',
    )
    assert.ok(
      managerDef.parameters.properties.self_note,
      'js-manager must expose self_note',
    )
    assert.equal(
      managerDef.parameters.required.includes('self_note'),
      false,
      'js-manager must not require self_note',
    )
    assert.ok(
      managerDef.description.includes(englishCollaboration) || managerDef.description.includes(chineseCollaboration),
      'js-manager description must include collaboration prose',
    )

    // Decoration extends the schema; it never replaces the tool's own contract.
    assert.ok(summary.tools.read.required.includes('filePath'), 'read must still require filePath')
    assert.ok(summary.tools.write.required.includes('content'), 'write must still require content')
    assert.ok(summary.tools.edit.required.includes('oldString'), 'edit must still require oldString')
    assert.ok(summary.tools.grep.required.includes('pattern'), 'grep must still require pattern')
    assert.ok(
      summary.tools.read.description.includes(englishCollaboration) ||
        summary.tools.read.description.includes(chineseCollaboration),
      'built-in descriptions must carry the new collaboration prose',
    )
    assert.equal(
      summary.tools.read.description.includes('delegate_readonly_rounds on every tool call'),
      false,
      'legacy delegation prose must not appear',
    )
    assert.equal(
      /companion|trust|retain control|同伴|信任|保留控制权/i.test(summary.tools.read.description),
      false,
      'the collaboration prose must not carry companion narrative',
    )

    // The decorated call really executed with the model's own arguments, and the
    // provider-wire history kept them.
    assert.equal(summary.followUpObserved, true, 'the built-in tool call must settle into a follow-up request')
    assert.equal(summary.historicalArguments?.filePath, 'canary-sample.txt')
    assert.equal(summary.historicalArguments?.estimated_readonly_rounds, 2)
    assert.equal(summary.historicalArguments?.self_note, 'checking the canary fixture')
    assert.equal(
      summary.historicalArguments?.delegate_readonly_rounds,
      undefined,
      'legacy delegate_readonly_rounds must not be present in historical arguments',
    )
    assert.match(
      summary.toolResultPreview ?? '',
      /readonly delegation schema canary/,
      'the built-in tool must execute and return its real result',
    )

    // Contract validation: legal combinations vs historical anti-pattern
    // 1. Legal positive estimate: non-blank self_note paired with rounds > 0
    const validPositive = parseParticipatingArguments({
      filePath: 'canary-sample.txt',
      [EstimatedReadonlyRoundsField]: 2,
      self_note: 'checking the canary fixture',
    })
    assert.equal(validPositive.tag, 0, 'positive estimate with non-empty note is valid')

    // 2. Legal zero estimate: self_note is omitted
    const validZero = parseParticipatingArguments({
      filePath: 'canary-sample.txt',
      [EstimatedReadonlyRoundsField]: 0,
    })
    assert.equal(validZero.tag, 0, 'zero estimate omitting self_note is valid')
    assert.equal(validZero.fields[0][1], undefined, 'parsed note must be None/undefined for zero estimate')

    // 3. Historical anti-pattern (0 with note): must be rejected under new contract
    const invalidZeroWithNote = parseParticipatingArguments({
      filePath: 'canary-sample.txt',
      [EstimatedReadonlyRoundsField]: 0,
      self_note: 'checking the canary fixture',
    })
    assert.equal(invalidZeroWithNote.tag, 1, '0 with self_note is an illegal combination and must be rejected')

    // 4. Legacy field rejection: delegate_readonly_rounds must be rejected
    const legacyAttempt = parseParticipatingArguments({
      filePath: 'canary-sample.txt',
      delegate_readonly_rounds: 0,
    })
    assert.equal(legacyAttempt.tag, 1, 'legacy delegate_readonly_rounds field must be rejected')
  },
)
}
