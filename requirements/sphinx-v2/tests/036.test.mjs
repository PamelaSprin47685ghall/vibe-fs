import assert from 'node:assert/strict'
import test from 'node:test'
import {randomUUID} from 'node:crypto'
import {accessSync, existsSync, mkdtempSync, readFileSync, readdirSync, realpathSync, rmSync, writeFileSync} from 'node:fs'
import {tmpdir} from 'node:os'
import {join} from 'node:path'
import {fileURLToPath} from 'node:url'
import {Client} from '@modelcontextprotocol/sdk/client/index.js'
import {StdioClientTransport} from '@modelcontextprotocol/sdk/client/stdio.js'
import {Watchdog} from '../../verification-system/tests/e2e/support/watchdog.js'
import {isTool} from '../../../dist/Sphinx/V2/Wire/Surface.js'
import * as Wire from '../../../dist/Sphinx/V2/Wire/Surface.js'
import {isOk, isError, errorValue} from '../../../dist/Sphinx/V2/Core/Surface.js'
import {
  Tool_decodeStart as decodeStart,
  Tool_decodeWorkSubmit as decodeWorkSubmit,
  Tool_decodeStatus as decodeStatus,
  Tool_decodeCancel as decodeCancel,
  Tool_decodeExport as decodeExport,
  Tool_refusalCode as refusalCode,
  Tool_refusalPath as refusalPath,
  Tool_refusalMessage as refusalMessage,
} from '../../../dist/Sphinx/V2/Hosts/Mcp/Tool.js'

const allowed = ['sphinx_inquiry_start', 'sphinx_work_submit', 'sphinx_inquiry_status', 'sphinx_inquiry_cancel', 'sphinx_inquiry_export']

const refusalOf = result => {
  assert.equal(isError(result), true, 'expected a refused decode')
  const refusal = errorValue(result)
  return {code: refusalCode(refusal), path: refusalPath(refusal), message: refusalMessage(refusal)}
}

test('WHAT[sphinx-v2-036] the actual tool contract admits five names and rejects retired stage aliases and unconnected tools', () => {
  for (const name of allowed) assert.equal(isTool(name), true, name)
  for (const name of ['sphinx_work_next', 'sphinx_goal_amend', 'sphinx_assess', 'sphinx_propose', 'sphinx_investigate', 'sphinx_synthesize', 'sphinx', 'sphinx_inquiry_start_extra', 'SPHINX_WORK_NEXT']) {
    assert.equal(isTool(name), false, name)
  }
})

test('WHAT[sphinx-v2-036] every registered tool decodes its own arguments instead of ignoring them', () => {
  const argumentsOf = {
    sphinx_inquiry_start: {commandId: 'c', goalText: 'g', constraints: [], materialRefs: [], authorizationRef: 'u', profileRef: 'p', steps: []},
    sphinx_work_submit: {commandId: 'c', inquiryId: 'i', workId: 'w', attempt: 1, fence: 'f', canonicalResult: 'r', outcome: 'succeeded'},
    sphinx_inquiry_status: {inquiryId: 'i'},
    sphinx_inquiry_cancel: {commandId: 'c', inquiryId: 'i', reason: 'r'},
    sphinx_inquiry_export: {inquiryId: 'i', mode: 'summary'},
  }

  const decoders = {
    sphinx_inquiry_start: decodeStart,
    sphinx_work_submit: decodeWorkSubmit,
    sphinx_inquiry_status: decodeStatus,
    sphinx_inquiry_cancel: decodeCancel,
    sphinx_inquiry_export: decodeExport,
  }

  assert.deepEqual(Object.keys(decoders).sort(), [...allowed].sort())
  assert.deepEqual(Object.keys(argumentsOf).sort(), [...allowed].sort())

  for (const name of allowed) {
    assert.equal(isOk(decoders[name](argumentsOf[name])), true, name)
    assert.equal(isError(decoders[name]({})), true, name)
  }
})

test('WHAT[sphinx-v2-036] a work submit validates outcome options and requires a failure reason on failure', () => {
  const base = {commandId: 'c', inquiryId: 'i', workId: 'w', attempt: 1, fence: 'w:1', canonicalResult: 'r'}
  assert.equal(isOk(decodeWorkSubmit({...base, outcome: 'succeeded'})), true)
  assert.equal(isOk(decodeWorkSubmit({...base})), true, 'omitted outcome defaults to succeeded')
  assert.equal(isOk(decodeWorkSubmit({...base, outcome: 'failed', reason: 'no data'})), true)
  assert.equal(refusalOf(decodeWorkSubmit({...base, outcome: 'failed'})).path, 'reason')
  assert.equal(refusalOf(decodeWorkSubmit({...base, outcome: 'maybe'})).path, 'outcome')
})

test('WHAT[sphinx-v2-036] invalid inquiry identities are refused by the real JS reader instead of escaping as exceptions', () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-invalid-read-id-'))
  const handle = Wire.create(commonDir, 'invalid-read-owner', null)
  try {
    const before = journalBytes(commonDir)
    for (const inquiryId of ['a b', 'inq\nx', 'inq\0x']) {
      for (const result of [
        Wire.status(handle, {inquiryId}),
        Wire.exportInquiry(handle, {inquiryId, mode: 'summary'}),
        Wire.exportInquiry(handle, {inquiryId, mode: 'full'}),
      ]) {
        assert.equal(result.outcome, 'refused')
        assert.equal(result.refusal.code, 'INVALID_SCHEMA')
        assert.equal(result.refusal.path, 'inquiryId')
        assert.notEqual(result.refusal.message.trim(), '')
      }
    }
    assert.deepEqual(journalBytes(commonDir), before)
  } finally {
    Wire.dispose(handle)
    rmSync(commonDir, {recursive: true, force: true})
  }
})

test('WHAT[sphinx-v2-036] read ingress refuses explicit additional commands and state mutations by field', () => {
  const reads = [
    ['sphinx_inquiry_status', decodeStatus, {inquiryId: 'i'}],
    ['sphinx_inquiry_export', decodeExport, {inquiryId: 'i', mode: 'summary'}],
    ['sphinx_inquiry_export', decodeExport, {inquiryId: 'i', mode: 'full'}],
  ]
  const cancel = {tool: 'sphinx_inquiry_cancel', arguments: {commandId: 'c', inquiryId: 'i', reason: 'stop'}}
  const mutations = {
    command: cancel,
    commands: [cancel],
    certificatePatches: [{targetRef: 'n', expectedSlotRevision: '0'}],
    budgetDebit: {modelCalls: 1},
    events: [{tag: 'CancelRequested', payload: 'stop'}],
    goalRevision: '1',
    goalAmendment: {authorizedBy: 'u', addedConstraints: ['new constraint']},
  }

  for (const [name, decode, args] of reads) {
    assert.equal(isOk(decode(args)), true, name + '.valid-read')
    for (const [field, value] of Object.entries(mutations)) {
      const refusal = refusalOf(decode({...args, [field]: value}))
      assert.equal(refusal.code, 'INVALID_SCHEMA', name + '.' + field)
      assert.equal(refusal.path, field, name + '.' + field)
      assert.equal(typeof refusal.message, 'string')
      assert.notEqual(refusal.message.trim(), '', name + '.' + field)
    }
  }
})

test('WHAT[sphinx-v2-036] an export mode outside summary and full is refused by field', () => {
  assert.equal(refusalOf(decodeExport({inquiryId: 'i', mode: 'everything'})).path, 'mode')
  assert.equal(isOk(decodeExport({inquiryId: 'i', mode: 'full'})), true)
})

test('WHAT[sphinx-v2-036] actual JS start refuses malformed string lists without coercion or exceptions', async t => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-start-list-types-'))
  const handle = Wire.create(commonDir, 'start-list-owner', startConfig)
  const command = {goalText: 'original goal with spaces', constraints: ['a lawful constraint with spaces'],
    materialRefs: [], authorizationRef: 'user', profileRef: startConfig.profileRef}
  const cases = [
    ['missing', undefined], ['null', null], ['object', {}], ['string', 'reference'],
    ['numeric entry', [1]], ['object entry', [{}]], ['null entry', [null]], ['boolean entry', [true]],
  ]
  try {
    for (const field of ['constraints', 'materialRefs']) {
      for (const [index, [label, value]] of cases.entries()) {
        await t.test('WHAT[sphinx-v2-036] actual start rejects ' + field + ' ' + label, async () => {
          const args = {...command, commandId: field + '-invalid-' + index, [field]: value}
          if (value === undefined) delete args[field]
          const before = journalBytes(commonDir)
          const result = await Wire.start(handle, args)
          assert.equal(result.outcome, 'refused')
          assert.equal(result.refusal.code, 'INVALID_SCHEMA')
          assert.equal(result.refusal.path, field)
          assert.notEqual(result.refusal.message.trim(), '')
          assert.deepEqual(journalBytes(commonDir), before)
        })
      }
    }
    const before = journalBytes(commonDir)
    const created = await Wire.start(handle, {...command, commandId: 'lawful-list-positive-control'})
    assert.equal(created.outcome, 'created', 'text fields with interior spaces remain lawful')
    assert.notDeepEqual(journalBytes(commonDir), before, 'valid lists still cause an actual durable creation')
  } finally {
    Wire.dispose(handle)
    rmSync(commonDir, {recursive: true, force: true})
  }
})

test('WHAT[sphinx-v2-036] invalid material identities receive named refusals over real stdio', async t => {
  await withSphinxStdio(t, async session => {
    const before = journalBytes(session.commonDir)
    for (const reference of ['ref a', 'ref\nx', 'ref\0x']) {
      await expectMcpRefusal(session, 'sphinx_inquiry_start', {
        commandId: 'invalid-material', goalText: 'goal with whitespace is lawful', constraints: [],
        materialRefs: [reference], authorizationRef: 'user', profileRef: startConfig.profileRef,
      }, 'INVALID_SCHEMA', 'materialRefs[0]', 'invalid material identity')
    }
    assert.deepEqual(journalBytes(session.commonDir), before)
  })
})

const serveEntry = fileURLToPath(new URL('../../../dist/Sphinx/V2/ServeEntry.js', import.meta.url))
const plainObject = value => value !== null && typeof value === 'object' &&
  !Array.isArray(value) && [Object.prototype, null].includes(Object.getPrototypeOf(value))
const nativeJson = value => {
  if (value === null || typeof value === 'string' || typeof value === 'boolean') return true
  if (typeof value === 'number') return Number.isFinite(value)
  if (Array.isArray(value)) return value.every(nativeJson)
  return plainObject(value) && Object.values(value).every(nativeJson)
}

function mcpRefusal(result, label) {
  const layout = 'MCP_RESPONSE_LAYOUT: ' + label
  assert.equal(plainObject(result) && nativeJson(result), true, layout + ' 必须返回原生 JSON 对象')
  assert.equal(result.isError, true, layout + ' 拒绝必须明确 isError=true')
  let payload
  if (Object.hasOwn(result, 'structuredContent')) {
    payload = result.structuredContent
  } else {
    assert.equal(Array.isArray(result.content), true, layout + ' 缺少 content')
    const text = result.content.filter(block => block.type === 'text')
    assert.equal(text.length, 1, layout + ' 必须有一份非空的可读 JSON 文本，空 content 不算拒绝')
    assert.equal(typeof text[0].text, 'string', layout + ' text 必须是字符串')
    assert.notEqual(text[0].text.trim(), '', layout + ' text 不得为空')
    try { payload = JSON.parse(text[0].text) }
    catch (cause) { throw new Error(layout + ' content 不是正式业务 JSON', {cause}) }
  }
  assert.equal(plainObject(payload) && nativeJson(payload), true, layout + ' 业务 payload 必须是原生对象')
  assert.equal(payload.apiVersion, '2', layout + ' Sphinx API 版本')
  assert.equal(payload.outcome, 'refused', layout + ' 不能把拒绝报告为业务成功')
  assert.equal(plainObject(payload.refusal), true, layout + ' 缺少具名 refusal')
  for (const field of ['code', 'path', 'message']) {
    assert.equal(typeof payload.refusal[field], 'string', layout + ' refusal.' + field)
    assert.notEqual(payload.refusal[field].trim(), '', layout + ' refusal.' + field + ' 不得为空')
  }
  return payload.refusal
}

async function expectMcpRefusal(session, name, args, code, path, label) {
  const result = await session.callTool(name, args, label)
  const refusal = mcpRefusal(result, label)
  assert.equal(refusal.code, code, label)
  assert.equal(refusal.path, path, label)
}

async function closeAndObserveExit(client, transport, exited, diagnostic) {
  let rejectSilence
  const silence = new Promise((_, reject) => { rejectSilence = reject })
  const watchdog = new Watchdog({
    label: 'sphinx-mcp cleanup',
    onTimeout: () => console.error(diagnostic('等待 SDK 子进程的真实 close 事件')),
    deps: {terminate: () => rejectSilence(new Error('MCP_SERVER_EXIT_UNOBSERVED: ' + diagnostic('未观察到真实退出')))},
  })
  const cleanup = (async () => {
    const errors = []
    try { await client.close() } catch (error) { errors.push(error) }
    try { await transport.close() } catch (error) { errors.push(error) }
    await exited
    assert.equal(transport.pid, null, 'MCP_SERVER_EXIT_UNOBSERVED: close 后仍有 SDK 子进程')
    if (errors.length) throw new AggregateError(errors, 'MCP_CLIENT_CLEANUP_FAILED')
  })()
  try { await Promise.race([cleanup, silence]) }
  finally { watchdog.stop() }
}

async function withSphinxStdio(t, scenario, options = {}) {
  try { accessSync(serveEntry) }
  catch (cause) { throw new Error('MCP_ENTRY_UNAVAILABLE: 需要最终集成产物 ' + serveEntry, {cause}) }
  const ownsCommonDir = options.commonDir === undefined
  const commonDir = options.commonDir ?? mkdtempSync(join(tmpdir(), 'sphinx-mcp-contract-'))
  const entryPath = realpathSync(serveEntry)
  const transport = new StdioClientTransport({
    command: process.execPath,
    args: [...(options.importModule === undefined ? [] : ['--import', options.importModule]), entryPath],
    cwd: commonDir,
    env: {
      SPHINX_COMMON_DIR: commonDir,
      ...(options.startConfig === undefined ? {} : {SPHINX_START_CONFIG: JSON.stringify(options.startConfig)}),
    },
    stderr: 'pipe',
  })
  const client = new Client({name: 'sphinx-stdio-contract', version: '1'}, {capabilities: {}})
  const controller = new AbortController()
  const signal = AbortSignal.any([t.signal, controller.signal])
  let stderr = ''
  let transportError = null
  let waiting = 'initialize'
  let lastProgress = '监测开始，尚无协议完成'
  let replyNumber = 0
  let startAttempted = false
  let closed = false
  let resolveExit
  const exited = new Promise(resolve => { resolveExit = resolve })
  transport.onclose = () => { closed = true; resolveExit() }
  transport.onerror = error => { transportError = error }
  const onStderr = chunk => { stderr = (stderr + chunk).slice(-4096) }
  transport.stderr.setEncoding('utf8')
  transport.stderr.on('data', onStderr)
  const diagnostic = wait => '目标=ServeEntry；等待=' + wait + '；最近进展=' + lastProgress +
    '；transport error=' + (transportError?.message ?? '无') + '；stderr尾部=' + (stderr || '无')
  const unexpectedExit = exited.then(() => {
    throw new Error('MCP_SERVER_EXITED: ' + diagnostic(waiting))
  })
  unexpectedExit.catch(() => {})
  const watchdog = new Watchdog({
    label: 'sphinx-mcp protocol',
    onTimeout: () => console.error(diagnostic(waiting)),
    deps: {terminate: () => controller.abort(new Error('MCP_CAUSAL_SILENCE: ' + diagnostic(waiting)))},
  })
  let interrupt
  const interrupted = new Promise((_, reject) => {
    interrupt = () => reject(signal.reason)
    signal.addEventListener('abort', interrupt, {once: true})
    if (signal.aborted) interrupt()
  })
  interrupted.catch(() => {})
  const step = async (label, action, failureCode) => {
    waiting = label
    signal.throwIfAborted()
    try {
      const result = await Promise.race([action(), unexpectedExit, interrupted])
      lastProgress = label + ' 已完成'
      watchdog.advance({reason: lastProgress, lane: 'mcp-protocol', expectationId: String(++replyNumber)})
      return result
    } catch (cause) {
      throw new Error(failureCode + ': ' + diagnostic(label), {cause})
    }
  }
  let failure
  try {
    await step('initialize/initialized', () => {
      startAttempted = true
      return client.connect(transport, {signal})
    }, 'MCP_BOOT_OR_REGISTRATION_FAILED')
    const session = {
      serverInfo: client.getServerVersion(),
      listTools: () => step('tools/list', () => client.listTools(undefined, {signal}), 'MCP_DISCOVERY_FAILED'),
      callTool: (name, args, label) => step(label, () => client.callTool({name, arguments: args}, undefined, {signal}), 'MCP_PROTOCOL_RESPONSE_FAILED'),
      commonDir,
    }
    await scenario(session)
  } catch (error) { failure = error }
  finally {
    watchdog.stop()
    try {
      if (startAttempted) await closeAndObserveExit(client, transport, exited, diagnostic)
      else { await client.close(); await transport.close() }
    } catch (error) {
      failure = failure ? new AggregateError([failure, error], 'MCP_CONTRACT_AND_CLEANUP_FAILED') : error
    }
    signal.removeEventListener('abort', interrupt)
    transport.stderr.off('data', onStderr)
    if (closed || !startAttempted) {
      if (ownsCommonDir) rmSync(commonDir, {recursive: true, force: true})
    } else t.diagnostic('MCP_SERVER_EXIT_UNOBSERVED: 保留 ' + commonDir)
  }
  if (failure) throw failure
  assert.equal(closed, true, 'MCP_SERVER_EXIT_UNOBSERVED: fixture 未取得实际退出事件')
}

function mcpBusiness(result, outcome, label) {
  assert.equal(plainObject(result) && nativeJson(result), true, 'MCP_BUSINESS_LAYOUT: ' + label)
  const texts = result.content?.filter(block => block.type === 'text') ?? []
  assert.equal(texts.length, 1, 'MCP_BUSINESS_LAYOUT: one readable JSON payload, ' + label)
  const textPayload = JSON.parse(texts[0].text)
  const payload = result.structuredContent ?? textPayload
  assert.equal(plainObject(payload) && nativeJson(payload), true, 'MCP_BUSINESS_LAYOUT: native business JSON, ' + label)
  assert.deepEqual(textPayload, payload, 'MCP_BUSINESS_LAYOUT: structured and text payload agree, ' + label)
  assert.equal(payload.apiVersion, '2', label)
  assert.equal(payload.outcome, outcome, 'BUSINESS_OUTCOME: ' + label)
  assert.equal(result.isError, false, 'MCP_BUSINESS_LAYOUT: successful business outcome, ' + label)
  return payload
}

function wireBusiness(payload, outcome, label) {
  assert.equal(plainObject(payload) && nativeJson(payload), true, 'JS_BUSINESS_LAYOUT: ' + label)
  assert.equal(payload.apiVersion, '2', label + '.apiVersion')
  assert.equal(payload.outcome, outcome, 'JS_BUSINESS_OUTCOME: ' + label)
  return payload
}

function journalBytes(directory, prefix = '') {
  return readdirSync(directory, {withFileTypes: true}).sort((left, right) => left.name.localeCompare(right.name)).flatMap(entry => {
    const relative = prefix + entry.name
    if (entry.isDirectory()) return journalBytes(join(directory, entry.name), relative + '/')
    assert.equal(entry.isFile(), true, 'journal fixture contains only owned directories and files: ' + relative)
    return [{path: relative, bytes: readFileSync(join(directory, entry.name)).toString('base64')}]
  })
}

function assertHashes(payload, label) {
  for (const field of ['traceHash', 'stateHash', 'semanticHash']) {
    assert.equal(typeof payload[field], 'string', label + '.' + field)
    assert.match(payload[field], /^[a-f0-9]{64}$/, label + '.' + field)
  }
}

const startConfig = {
  commandNamespace: 'test-owner', createdBy: 'authorized-controller', profileRef: 'sphinx.default@2',
  executionMode: 'delegated', resourceSpecs: [{name: 'calls', kind: {case: 'consumed', payload: 'calls'}, authorizedLimit: 10}],
  renderReserve: {calls: 1},
}

const twoStepPlan = [
  {id: 'step-a', title: 'collect facts', dependsOn: []},
  {id: 'step-b', title: 'write the answer', dependsOn: ['step-a']},
]

async function startInquiry(session, commandId, goalText, steps = []) {
  const command = {
    commandId, goalText, constraints: [], materialRefs: [], authorizationRef: 'authorized-user', profileRef: 'sphinx.default@2', steps,
  }
  return mcpBusiness(await session.callTool('sphinx_inquiry_start', command, 'start.' + commandId), 'created', 'start.' + commandId)
}

async function readInquiry(session, inquiryId, label) {
  return mcpBusiness(await session.callTool('sphinx_inquiry_status', {inquiryId}, label), 'read', label)
}

test('WHAT[sphinx-v2-036] one real SDK stdio session registers five tools and the single loop works through the production entry', async t => {
  await withSphinxStdio(t, async session => {
    assert.equal(session.serverInfo.name, 'sphinx', 'initialize must reach the production Sphinx server')
    const {tools} = await session.listTools()
    assert.equal(tools.length, 5, 'MCP_DISCOVERY_FAILED: exactly five real registered tools')
    assert.deepEqual(tools.map(tool => tool.name).sort(), [...allowed].sort())

    const inquiryId = 'protocol-only-absent-inquiry'
    await expectMcpRefusal(session, 'sphinx_inquiry_start', {
      commandId: 'unconfigured-start', goalText: 'an authorized goal', constraints: [],
      materialRefs: [], authorizationRef: 'user-authorizer', profileRef: 'sphinx.default@2',
    }, 'CONFIG_REQUIRED', 'configuration', 'unconfigured start')

    const cancel = {tool: 'sphinx_inquiry_cancel', arguments: {commandId: 'cancel-1', inquiryId, reason: 'stop'}}
    const mutations = {
      command: cancel,
      commands: [cancel],
      certificatePatches: [{targetRef: 'n', expectedSlotRevision: '0'}],
      budgetDebit: {modelCalls: 1},
      events: [{tag: 'CancelRequested', payload: 'stop'}],
      goalRevision: '1',
      goalAmendment: {authorizedBy: 'user-authorizer', expectedRevision: '0', addedConstraints: ['new constraint']},
    }
    const answer = {
      commandId: 'submit-1', inquiryId, workId: 'protocol-only-work', attempt: 1,
      fence: 'protocol-only-fence', canonicalResult: '{"answer":"42"}', outcome: 'succeeded',
    }
    await expectMcpRefusal(session, 'sphinx_work_submit', answer, 'UNKNOWN_INQUIRY', 'inquiryId', 'absent inquiry submit')
    for (const field of ['certificatePatches', 'budgetDebit', 'events', 'goalRevision', 'goalAmendment']) {
      await expectMcpRefusal(session, 'sphinx_work_submit', {...answer, [field]: mutations[field]}, 'WORK_RESULT_EXCEEDS_ROLE', field, 'work_submit.' + field)
    }
    const reads = [
      ['sphinx_inquiry_status', {inquiryId}],
      ['sphinx_inquiry_export', {inquiryId, mode: 'summary'}],
      ['sphinx_inquiry_export', {inquiryId, mode: 'full'}],
    ]
    for (const [name, args] of reads) {
      const refusal = mcpRefusal(await session.callTool(name, args, name + '.clean'), name + '.clean')
      assert.equal(refusal.path, 'inquiryId')
      assert.equal(refusal.code, 'UNKNOWN_INQUIRY', 'valid read/export must reach the actual absent-inquiry branch')
    }
  })
})

test('WHAT[sphinx-v2-036] start plans and dispatches only satisfied steps, submit commits the answer, duplicates are not booked twice', async t => {
  await withSphinxStdio(t, async session => {
    const created = await startInquiry(session, 'loop-two-step', '第一目标\r\n雪 😀 尾部  ', twoStepPlan)
    assert.equal(created.status, 'active')
    assert.equal(created.advance.outcome, 'awaiting-results', 'the first step was dispatched')

    const first = await readInquiry(session, created.inquiryId, 'first.status')
    assert.equal(first.status, 'active')
    assert.equal(first.nextStep.workId, 'step-a', 'only the root step is dispatchable')
    assert.equal(first.nextStep.attempt, '1')
    assert.equal(first.nextStep.fence, 'step-a:1')
    assert.equal(first.nextStep.title, 'collect facts')

    // Submit the root successfully; the follow-up must dispatch the child exactly once.
    const submittedA = mcpBusiness(await session.callTool('sphinx_work_submit', {
      commandId: 'submit-a', inquiryId: created.inquiryId, workId: 'step-a', attempt: 1,
      fence: 'step-a:1', canonicalResult: '{"facts":"ready"}', outcome: 'succeeded',
    }, 'submit.a'), 'applied', 'submit.a')
    assert.equal(submittedA.status, 'active', 'the child is dispatched, not yet answered')

    const second = await readInquiry(session, created.inquiryId, 'second.status')
    assert.equal(second.nextStep.workId, 'step-b', 'the child is now dispatchable')
    assert.equal(second.nextStep.fence, 'step-b:1')

    // Submit the answer step; the final step's result becomes the answer.
    const submittedB = mcpBusiness(await session.callTool('sphinx_work_submit', {
      commandId: 'submit-b', inquiryId: created.inquiryId, workId: 'step-b', attempt: 1,
      fence: 'step-b:1', canonicalResult: '{"answer":"42"}', outcome: 'succeeded',
    }, 'submit.b'), 'applied', 'submit.b')
    assert.equal(submittedB.status, 'completed')
    assert.equal(JSON.parse(submittedB.answer).answer, '42')

    const final = await readInquiry(session, created.inquiryId, 'final.status')
    assert.equal(final.status, 'completed')
    assert.equal(final.nextStep, null, 'nothing is left to dispatch')
    assert.equal(JSON.parse(final.answer).answer, '42')

    // A repeated delivery of the same result returns the original receipt and does not
    // book a second observation.
    const replay = mcpBusiness(await session.callTool('sphinx_work_submit', {
      commandId: 'submit-b-again', inquiryId: created.inquiryId, workId: 'step-b', attempt: 1,
      fence: 'step-b:1', canonicalResult: '{"answer":"42"}', outcome: 'succeeded',
    }, 'submit.b.repeat'), 'replayed', 'submit.b.repeat')
    assert.equal(replay.observationId, submittedB.observationId)

    // The same result identity with different bytes is a conflict, not a second answer.
    await expectMcpRefusal(session, 'sphinx_work_submit', {
      commandId: 'submit-b-different', inquiryId: created.inquiryId, workId: 'step-b', attempt: 1,
      fence: 'step-b:1', canonicalResult: '{"answer":"43"}', outcome: 'succeeded',
    }, 'RESULT_PAYLOAD_CONFLICT', 'canonicalResult', 'submit.b.conflict')

    const full = mcpBusiness(await session.callTool('sphinx_inquiry_export', {inquiryId: created.inquiryId, mode: 'full'}, 'final.full'), 'exported', 'final.full')
    const observations = full.inquiry.observations.filter(obs => obs.work === 'step-b')
    assert.equal(observations.length, 1, 'the answer step is booked exactly once')
  }, {startConfig})
})

test('WHAT[sphinx-v2-036] a failed step leaves its dependents honestly unexecuted', async t => {
  await withSphinxStdio(t, async session => {
    const created = await startInquiry(session, 'loop-failure', 'goal needing a failing step', twoStepPlan)
    const first = await readInquiry(session, created.inquiryId, 'first.status')
    assert.equal(first.nextStep.workId, 'step-a')

    const failed = mcpBusiness(await session.callTool('sphinx_work_submit', {
      commandId: 'submit-a-failed', inquiryId: created.inquiryId, workId: 'step-a', attempt: 1,
      fence: 'step-a:1', canonicalResult: '', outcome: 'failed', reason: 'source is unavailable',
    }, 'submit.a.failed'), 'applied', 'submit.a.failed')
    assert.equal(failed.status, 'active', 'a failed step does not fabricate completion')

    const after = await readInquiry(session, created.inquiryId, 'after.failure')
    assert.equal(after.nextStep, null, 'the child is not dispatched after its dependency failed')
    assert.equal(after.advance.outcome, 'no-runnable-work')
    assert.match(after.advance.detail, /blocked by failed dependency step-a/)
    assert.equal(after.answer, null)
  }, {startConfig})
})

test('WHAT[sphinx-v2-036] user cancel stops new dispatch and closes existing work', async t => {
  await withSphinxStdio(t, async session => {
    const created = await startInquiry(session, 'loop-cancel', 'goal that will be cancelled', twoStepPlan)
    const before = journalBytes(session.commonDir)
    const cancelled = mcpBusiness(await session.callTool('sphinx_inquiry_cancel', {
      commandId: 'cancel-1', inquiryId: created.inquiryId, reason: 'authorized stop',
    }, 'cancel'), 'applied', 'cancel')
    assert.equal(cancelled.status, 'cancelling', 'an unconfirmed cancel is not a drained fact')
    assert.notDeepEqual(journalBytes(session.commonDir), before, 'cancellation is a durable change')

    const after = await readInquiry(session, created.inquiryId, 'after.cancel')
    assert.equal(after.status, 'cancelling')
    assert.equal(after.nextStep, null, 'no new work is dispatched after cancel')
    assert.match(after.advance.detail, /cancellation requested/)
    const workStates = after.inquiry.work.map(item => item.state)
    assert.ok(workStates.every(state => state === 'Cancelled'), 'outstanding work is closed: ' + workStates.join(','))
  }, {startConfig})
})

test('WHAT[sphinx-v2-036] configured real stdio start persists byte-exact goals, answers and native reads survive a new OS server', async t => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-mcp-durable-start-'))
  const goalText = '第一目标\r\n空值:\u0000；café 😀 尾部  '
  const command = {
    commandId: 'stdio-create-0', goalText, constraints: ['keep the supplied text'],
    materialRefs: ['material:原始-0'], authorizationRef: 'authorized-user', profileRef: 'sphinx.default@2',
    steps: [{id: 'answer', title: 'answer the goal', dependsOn: []}],
  }
  let accepted
  let sharedHandle
  let completed = false
  try {
    await withSphinxStdio(t, async session => {
      const before = journalBytes(commonDir)
      const creation = mcpBusiness(await session.callTool('sphinx_inquiry_start', command, 'start.0'), 'created', 'start.0')
      assert.equal(creation.revision, '0')
      assert.equal(creation.status, 'active')
      assert.equal(creation.advance.outcome, 'awaiting-results', 'the default answer step is dispatched')
      assert.notDeepEqual(journalBytes(commonDir), before, 'positive control: actual creation changes journal bytes')

      const read = await readInquiry(session, creation.inquiryId, 'read.0')
      assert.equal(read.inquiry.goal.originalText, goalText, 'original goal bytes')
      assert.deepEqual(Buffer.from(read.inquiry.goal.originalText), Buffer.from(goalText), 'UTF8 goal bytes')
      assert.equal(read.nextStep.workId, 'answer', 'the default single step is the answer step')

      // Submit the answer, then read it back through export.
      const answer = '{"answer":"字节精确 雪 😀 尾部  "}'
      const submitted = mcpBusiness(await session.callTool('sphinx_work_submit', {
        commandId: 'submit-answer', inquiryId: creation.inquiryId, workId: 'answer', attempt: 1,
        fence: 'answer:1', canonicalResult: answer, outcome: 'succeeded',
      }, 'submit.answer'), 'applied', 'submit.answer')
      assert.equal(submitted.status, 'completed')

      const full = mcpBusiness(await session.callTool('sphinx_inquiry_export', {inquiryId: creation.inquiryId, mode: 'full'}, 'full.0'), 'exported', 'full.0')
      assertHashes(full, 'full.0')
      assert.equal(full.inquiry.observations.find(o => o.work === 'answer').result, answer, 'answer bytes are kept exactly')
      accepted = {creation, full}
      sharedHandle = Wire.create(commonDir, 'stdio-js-writer', startConfig)
      const jsRead = wireBusiness(Wire.status(sharedHandle, {inquiryId: creation.inquiryId}), 'read', 'JS reads MCP creation')
      assert.equal(JSON.parse(jsRead.answer).answer, JSON.parse(answer).answer, 'JS sees the committed answer')
      assert.deepEqual(Wire.exportInquiry(sharedHandle, {inquiryId: creation.inquiryId, mode: 'full'}), full, 'JS and MCP export the same accepted full')
    }, {commonDir, startConfig})
    await withSphinxStdio(t, async session => {
      const read = await readInquiry(session, accepted.creation.inquiryId, 'cold.0')
      assert.equal(read.inquiry.goal.originalText, goalText, 'cold reopen preserves goal bytes')
      assert.equal(read.status, 'completed')
      assert.equal(JSON.parse(read.answer).answer, JSON.parse(accepted.full.inquiry.observations.find(o => o.work === 'answer').result).answer)
    }, {commonDir, startConfig})
    completed = true
  } finally {
    if (sharedHandle) Wire.dispose(sharedHandle)
    if (completed) rmSync(commonDir, {recursive: true, force: true})
    else t.diagnostic('N06_A_FAILURE_EVIDENCE: shared durable directory retained at ' + commonDir)
  }
})

for (const operation of ['start', 'cancel']) {
  test(`WHAT[sphinx-v2-036] actual_${operation}_release_failure_is_a_terminal_unknown_with_its_original_command`, async t => {
    const commonDir = realpathSync(mkdtempSync(join(tmpdir(), 'sphinx-settlement-')))
    const loader = fileURLToPath(new URL('./settlement-release-loader.mjs', import.meta.url))
    const command = {commandId: 'positive-start', goalText: '初始事实\r\n雪 尾部  ', constraints: [],
      materialRefs: [], authorizationRef: 'user', profileRef: startConfig.profileRef, steps: []}
    let failedReply
    let evidence
    let exported
    let last
    let closed = false
    try {
      await withSphinxStdio(t, async session => {
        const first = mcpBusiness(await session.callTool('sphinx_inquiry_start', command, 'positive.start'), 'created', 'positive.start')
        const commandId = 'uncertain-' + operation
        writeFileSync(join(commonDir, 'settlement-arm.json'), JSON.stringify({commandId}))
        failedReply = await session.callTool(operation === 'start' ? 'sphinx_inquiry_start' : 'sphinx_inquiry_cancel',
          operation === 'start' ? {...command, commandId, goalText: '新的真实事实\r\n尾部  '}
            : {commandId, inquiryId: first.inquiryId, reason: 'authorized stop'}, 'uncertain.' + operation)
        evidence = JSON.parse(readFileSync(join(commonDir, 'settlement-release.json'), 'utf8'))
        assert.equal(evidence.releaseCalls, 1)
        assert.equal(evidence.lockReleased, true)
        assert.notEqual(evidence.pid, process.pid)
        assert.equal(existsSync(join(commonDir, 'wanxiangshu.lock')), false)
        assert.equal(readFileSync(evidence.sourceFile, 'base64'), evidence.bytes)
        const lines = Buffer.from(evidence.bytes, 'base64').toString('utf8').trimEnd().split('\n').map(JSON.parse)
        assert.equal(lines.length, 2, 'Only the positive control and one failed invocation append; no automatic retry')
        last = lines.at(-1)
        assert.equal(last.payload.commandId, commandId)
        assert.equal(last.payload.events[0].case, operation === 'start' ? 'InquiryCreated' : 'CancelRequested')
        exported = mcpBusiness(await session.callTool('sphinx_inquiry_export', {inquiryId: last.payload.inquiry, mode: 'full'},
          'uncertain.export'), 'exported', 'uncertain.export')
        assert.equal(exported.events.at(-1).event_id, last.event_id)
      }, {commonDir, startConfig, importModule: loader})
      closed = true
      const coldWriter = randomUUID()
      const cold = Wire.create(commonDir, coldWriter, null)
      try {
        assert.deepEqual(Wire.exportInquiry(cold, {inquiryId: last.payload.inquiry, mode: 'full'}), exported)
        assert.equal(existsSync(join(commonDir, 'wanxiangshu', 'events', `${coldWriter}.ndjson`)), false)
        assert.equal(readFileSync(evidence.sourceFile, 'base64'), evidence.bytes)
      } finally { Wire.dispose(cold) }
      const refusal = mcpRefusal(failedReply, 'actual Release uncertainty')
      assert.equal(refusal.code, 'COMMIT_UNKNOWN')
      assert.equal(refusal.path, 'inquiryId')
      for (const identity of [last.payload.inquiry, last.payload.commandId, last.event_id, evidence.cause]) {
        assert.ok(refusal.message.includes(identity), identity)
      }
      assert.match(refusal.message, /Reconcile the durable record before any retry/)
    } finally {
      if (closed) rmSync(commonDir, {recursive: true, force: true})
      else t.diagnostic('SPHINX_SETTLEMENT_FAILURE_EVIDENCE: retained ' + commonDir)
    }
  })
}
