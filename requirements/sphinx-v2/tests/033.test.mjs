import assert from 'node:assert/strict'
import {spawn} from 'node:child_process'
import {createHash} from 'node:crypto'
import {mkdtempSync, readFileSync, readdirSync, realpathSync, rmSync} from 'node:fs'
import {tmpdir} from 'node:os'
import {join} from 'node:path'
import {fileURLToPath} from 'node:url'
import test from 'node:test'
import * as Wire from '../../../dist/Sphinx/V2/Wire/Surface.js'
import {Watchdog} from '../../verification-system/tests/e2e/support/watchdog.js'

const configuration = {
  commandNamespace: 'test-owner', createdBy: 'authorized-controller', profileRef: 'sphinx.default@2',
  executionMode: 'delegated', resourceSpecs: [{name: 'calls', kind: {case: 'consumed', payload: 'calls'}, authorizedLimit: 0}],
  renderReserve: {calls: 0},
}
const command = {
  commandId: 'explicit-zero-start', goalText: '保留原文\r\n雪 😀 尾部  ', constraints: [],
  materialRefs: [], authorizationRef: 'authorized-user', profileRef: 'sphinx.default@2',
}

function durableBytes(directory, prefix = '') {
  return readdirSync(directory, {withFileTypes: true}).sort((left, right) => left.name.localeCompare(right.name)).flatMap(entry => {
    const relative = prefix + entry.name
    if (entry.isDirectory()) return durableBytes(join(directory, entry.name), relative + '/')
    assert.equal(entry.isFile(), true, relative)
    return [{path: relative, bytes: readFileSync(join(directory, entry.name)).toString('base64')}]
  })
}

test('WHAT[sphinx-v2-033] the public JS owner refuses missing durable storage instead of falling back to memory', () => {
  for (const commonDir of [null, undefined, '', '  \t\n']) {
    assert.throws(() => Wire.create(commonDir, 'durability-proof', configuration), /SPHINX_COMMON_DIR.*durable directory/)
  }
})

test('WHAT[sphinx-v2-033] explicit startup configuration rejects inadmissible authorization and quotas without a fallback or business writes', () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-invalid-start-config-'))
  const cases = [
    ['unsupported profile', {...configuration, profileRef: 'unregistered-profile'}],
    ['unsupported execution mode', {...configuration, executionMode: 'ambient'}],
    ['empty explicit quotas', {...configuration, resourceSpecs: []}],
    ['negative authorized limit', {...configuration, resourceSpecs: [{...configuration.resourceSpecs[0], authorizedLimit: -1}]}],
    ['infinite authorized limit', {...configuration, resourceSpecs: [{...configuration.resourceSpecs[0], authorizedLimit: Infinity}]}],
    ['NaN authorized limit', {...configuration, resourceSpecs: [{...configuration.resourceSpecs[0], authorizedLimit: NaN}]}],
    ['unknown reserve resource', {...configuration, renderReserve: {tokens: 0}}],
    ['reserve beyond authorization', {...configuration, renderReserve: {calls: 1}}],
    ['blank creator', {...configuration, createdBy: '  '}],
    ['blank command namespace', {...configuration, commandNamespace: '\t'}],
    ['opaque budget injection', {...configuration, budget: {unlimited: true}}],
  ]
  try {
    const before = durableBytes(commonDir)
    for (const [label, config] of cases) {
      assert.throws(() => Wire.create(commonDir, 'invalid-config-writer', config), /INVALID_START_CONFIGURATION at configuration:/, label)
      assert.deepEqual(durableBytes(commonDir), before, label + ' must not write business facts')
    }
  } finally { rmSync(commonDir, {recursive: true, force: true}) }
})

test('WHAT[sphinx-v2-033] a durable readonly owner needs explicit start authorization, while an explicitly supplied zero quota persists and reopens', async () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-explicit-zero-start-'))
  const handles = new Set()
  const open = config => {
    const handle = Wire.create(commonDir, 'zero-start-writer-' + handles.size, config)
    handles.add(handle)
    return handle
  }
  const close = handle => { Wire.dispose(handle); handles.delete(handle) }
  try {
    const readonly = open(null)
    const beforeRefusal = durableBytes(commonDir)
    const refused = await Wire.start(readonly, command)
    assert.equal(refused.apiVersion, '2')
    assert.equal(refused.outcome, 'refused')
    assert.deepEqual(Object.keys(refused.refusal).sort(), ['code', 'message', 'path'])
    assert.equal(refused.refusal.code, 'CONFIG_REQUIRED')
    assert.equal(refused.refusal.path, 'configuration')
    assert.notEqual(refused.refusal.message.trim(), '')
    assert.deepEqual(durableBytes(commonDir), beforeRefusal, 'unconfigured start writes no business facts')
    close(readonly)
    const authorized = open(configuration)
    const beforeCreation = durableBytes(commonDir)
    const creation = await Wire.start(authorized, command)
    assert.equal(creation.outcome, 'created', 'explicit zero is authorized input, not a missing quota')
    assert.equal(creation.revision, '0')
    assert.equal(creation.status, 'active')
    assert.equal(creation.advance.outcome, 'no-runnable-work')
    assert.notEqual(creation.inquiryId.trim(), '')
    assert.notEqual(creation.eventId.trim(), '')
    assert.notDeepEqual(durableBytes(commonDir), beforeCreation, 'positive control: canonical creation writes actual bytes')
    const current = Wire.status(authorized, {inquiryId: creation.inquiryId})
    assert.equal(current.outcome, 'read')
    assert.equal(current.inquiry.goal.originalText, command.goalText)
    assert.deepEqual(Buffer.from(current.inquiry.goal.originalText), Buffer.from(command.goalText))
    const trace = Wire.exportInquiry(authorized, {inquiryId: creation.inquiryId, mode: 'full'})
    assert.equal(trace.outcome, 'exported')
    assert.equal(trace.events.length, 1)
    const created = trace.events[0].payload.events[0]
    assert.equal(created.case, 'InquiryCreated')
    assert.deepEqual(created.payload.resourceSpecs, [{
      name: 'calls', kind: {case: 'Consumed', unitName: 'calls'}, authorizedLimit: 0,
    }], 'canonical resource facts retain the explicit zero authorization')
    assert.deepEqual(created.payload.renderReserve, [{key: 'calls', value: 0}], 'canonical reserve retains the explicitly supplied zero')
    close(authorized)
    const reopened = open(null)
    const beforeReads = durableBytes(commonDir)
    assert.deepEqual(Wire.status(reopened, {inquiryId: creation.inquiryId}), current, 'readonly cold owner recovers the same canonical state')
    assert.deepEqual(Wire.exportInquiry(reopened, {inquiryId: creation.inquiryId, mode: 'full'}), trace, 'readonly cold owner recovers the actual original trace')
    assert.deepEqual(durableBytes(commonDir), beforeReads, 'cold reads append no business facts')
  } finally {
    for (const handle of handles) Wire.dispose(handle)
    rmSync(commonDir, {recursive: true, force: true})
  }
})

test('WHAT[sphinx-v2-033] creation receipts bind the exact authorized reserve for every legal resource name', async () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-named-reserve-'))
  const handles = []
  const configured = reserve => ({
    ...configuration,
    resourceSpecs: [{name: '__proto__', kind: {case: 'consumed', payload: 'calls'}, authorizedLimit: 1}],
    renderReserve: JSON.parse('{"__proto__":' + reserve + '}'),
  })
  try {
    const original = Wire.create(commonDir, 'original-reserve', configured(0))
    handles.push(original)
    const creation = await Wire.start(original, {...command, commandId: 'named-reserve'})
    assert.equal(creation.outcome, 'created')
    const trace = Wire.exportInquiry(original, {inquiryId: creation.inquiryId, mode: 'full'})
    assert.deepEqual(trace.events[0].payload.events[0].payload.renderReserve, [{key: '__proto__', value: 0}])
    const changed = Wire.create(commonDir, 'changed-reserve', configured(1))
    handles.push(changed)
    const before = durableBytes(commonDir)
    const conflict = await Wire.start(changed, {...command, commandId: 'named-reserve'})
    assert.equal(conflict.outcome, 'refused', 'changing only the legal reserve must change command content')
    assert.equal(conflict.refusal.code, 'COMMAND_CONFLICT')
    assert.equal(conflict.refusal.path, 'commandId')
    assert.deepEqual(durableBytes(commonDir), before, 'content conflict appends no business facts')
    assert.deepEqual(Wire.exportInquiry(original, {inquiryId: creation.inquiryId, mode: 'full'}), trace)
  } finally {
    for (const handle of handles) Wire.dispose(handle)
    rmSync(commonDir, {recursive: true, force: true})
  }
})

test('WHAT[sphinx-v2-033] a disposed public owner rejects valid operations without writing and can be disposed again', async () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'sphinx-disposed-runtime-'))
  const handle = Wire.create(commonDir, 'disposed-runtime-writer', configuration)
  try {
    const beforeCreation = durableBytes(commonDir)
    const creation = await Wire.start(handle, {...command, commandId: 'disposal-positive-control'})
    assert.equal(creation.outcome, 'created')
    assert.notDeepEqual(durableBytes(commonDir), beforeCreation, 'positive control: an authorized live owner writes actual creation facts')
    const args = {inquiryId: creation.inquiryId}
    assert.equal(Wire.status(handle, args).outcome, 'read')
    assert.equal(Wire.exportInquiry(handle, {...args, mode: 'full'}).outcome, 'exported')
    const submission = {
      commandId: 'valid-disposal-submission', inquiryId: creation.inquiryId,
      workId: 'not-yet-dispatched-work', attempt: 1, fence: 'explicit-fence',
      canonicalResult: '{}', clusterId: 'explicit-cluster',
      resultSchema: {id: 'disposal-control@1', hash: createHash('sha256').update('{"type":"object"}').digest('hex')},
    }
    const unconnected = Wire.submitResults(handle, submission)
    assert.equal(unconnected.outcome, 'refused')
    assert.equal(unconnected.refusal.code, 'RUNTIME_OPERATION_UNSUPPORTED', 'positive control: the valid submission reaches its unconnected owner, not a schema refusal')
    const beforeDispose = durableBytes(commonDir)
    Wire.dispose(handle)
    assert.doesNotThrow(() => Wire.dispose(handle), 'disposing again is the existing idempotent latch contract')
    await assert.rejects(async () => Wire.start(handle, {...command, commandId: 'valid-post-dispose-start'}), /EventStore handle is disposed/)
    assert.throws(() => Wire.status(handle, args), /EventStore handle is disposed/)
    for (const mode of ['summary', 'full']) {
      assert.throws(() => Wire.exportInquiry(handle, {...args, mode}), /EventStore handle is disposed/)
    }
    assert.throws(() => Wire.submitResults(handle, submission), /EventStore handle is disposed/)
    assert.deepEqual(durableBytes(commonDir), beforeDispose, 'disposal and rejected operations append no business facts')
  } finally {
    Wire.dispose(handle)
    rmSync(commonDir, {recursive: true, force: true})
  }
})

async function rejectedProductionStartup(t, {label, startConfigText, missingCommonDir = false, stderrPattern}) {
  const directory = mkdtempSync(join(tmpdir(), 'sphinx-rejected-startup-'))
  const serveEntry = realpathSync(fileURLToPath(new URL('../../../dist/Sphinx/V2/ServeEntry.js', import.meta.url)))
  const env = {
    ...(missingCommonDir ? {} : {SPHINX_COMMON_DIR: directory}),
    ...(startConfigText === undefined ? {} : {SPHINX_START_CONFIG: startConfigText}),
  }
  const child = spawn(process.execPath, [serveEntry], {cwd: directory, env, stdio: ['ignore', 'pipe', 'pipe']})
  let stdout = ''
  let stderr = ''
  let spawnError
  let watchdogFailure
  let closeObserved = false
  child.stdout.setEncoding('utf8')
  child.stderr.setEncoding('utf8')
  child.stdout.on('data', chunk => { stdout += chunk })
  child.stderr.on('data', chunk => { stderr += chunk })
  child.on('error', error => { spawnError = error })
  const closed = new Promise(resolve => child.once('close', (code, signal) => {
    closeObserved = true
    resolve({code, signal})
  }))
  const watchdog = new Watchdog({
    label: 'sphinx rejected startup: ' + label,
    onTimeout: () => console.error('ServeEntry startup wait; case=' + label + '; child=' + child.pid + '; stderr=' + stderr.slice(-4096)),
    deps: {terminate: () => { watchdogFailure = new Error('SPHINX_STARTUP_CLOSE_UNOBSERVED'); child.kill('SIGKILL') }},
  })
  const abort = () => child.kill('SIGKILL')
  t.signal.addEventListener('abort', abort, {once: true})
  try {
    const exit = await closed
    if (spawnError) throw spawnError
    if (watchdogFailure) throw watchdogFailure
    t.signal.throwIfAborted()
    assert.equal(closeObserved, true, 'actual ChildProcess close must be observed')
    assert.equal(exit.code, 1, label + ' is a production startup failure')
    assert.equal(exit.signal, null)
    assert.equal(stdout, '', 'no MCP success or protocol payload precedes the startup refusal')
    assert.notEqual(stderr.trim(), '', 'startup failure has an explicit diagnostic')
    assert.match(stderr, stderrPattern)
    assert.deepEqual(durableBytes(directory), [], 'startup refusal creates no memory substitute or journal')
  } finally {
    watchdog.stop()
    t.signal.removeEventListener('abort', abort)
    if (closeObserved) rmSync(directory, {recursive: true, force: true})
    else t.diagnostic('SPHINX_STARTUP_CLOSE_UNOBSERVED: retained ' + directory)
  }
}

test('WHAT[sphinx-v2-033] the real production ServeEntry exits before emitting MCP protocol when durable storage is absent', async t => {
  await rejectedProductionStartup(t, {
    label: 'missing durable directory', missingCommonDir: true,
    stderrPattern: /SPHINX_COMMON_DIR.*required.*durable workspace/,
  })
})

for (const [label, startConfigText, stderrPattern] of [
  ['explicit blank startup configuration', ' \t\n', /configuration boot failed: SPHINX_START_CONFIG must be valid JSON:/],
  ['invalid startup JSON', '{"commandNamespace":', /configuration boot failed: SPHINX_START_CONFIG must be valid JSON:/],
  ['explicit JSON null configuration', 'null', /configuration rejected: INVALID_START_CONFIGURATION at configuration:/],
  ['semantically invalid startup configuration', JSON.stringify({...configuration, executionMode: 'ambient'}), /configuration rejected: INVALID_START_CONFIGURATION at configuration:[\s\S]*executionMode[\s\S]*delegated or independent/],
]) {
  test('WHAT[sphinx-v2-033] the real production ServeEntry refuses ' + label + ' before protocol or business writes', async t => {
    await rejectedProductionStartup(t, {label, startConfigText, stderrPattern})
  })
}
