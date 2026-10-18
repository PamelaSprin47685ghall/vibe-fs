import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as grounding from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'
import * as catalog from '../../../dist/Requirement/Grounding/Surface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const prepareWorkspace = (dir) => {
  mkdirSync(join(dir, 'requirements', 'alpha'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'ground truth\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'before\n', 'utf8')
}

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-opencode-'))
  prepareWorkspace(dir)
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolBatch = (providerID, path) => [
  { info: { id: 'c1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'todowrite-for-manager', callID: 'source', state: { status: 'pending', input: { program: `class Js extends JsProgram { async run() { return await this.cat(${JSON.stringify(path)}); } }` }, time: { start: 0 } } }] },
  { info: { id: 'r1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'todowrite-for-manager', callID: 'source', state: { status: 'completed', input: { program: `class Js extends JsProgram { async run() { return await this.cat(${JSON.stringify(path)}); } }` }, output: 'before\n', time: { start: 0, end: 0 } } }] },
]

test('WHAT[requirement-grounding-008] mutation grounding is weak observation and never becomes tool admission', async () => {
  const { dir, cleanup } = sandbox()
  let journal
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    const opened = await grounding.createJournal(dir)
    assert.equal(opened.ok, true)
    journal = opened.journal
    const first = await grounding.mutationDecision(opened.journal, dir, 'mutation', [sourcePath])
    assert.equal(first.allowed, true)
    assert.equal(first.needsGrounding, true)
    assert.deepEqual(first.packages, ['alpha'])

    await grounding.projectWithJournal(opened.journal, 'mutation', toolBatch('anthropic', sourcePath))
    const second = await grounding.mutationDecision(opened.journal, dir, 'mutation', [sourcePath])
    assert.equal(second.allowed, true)
    assert.equal(second.needsGrounding, false)

    writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '[\n', 'utf8')
    assert.equal(
      await grounding.weakMutationObservation(opened.journal, dir, 'mutation-broken-grounding', sourcePath),
      true,
      'broken grounding metadata is fail-open and cannot turn into a write failure',
    )
  } finally {
    try {
      if (journal !== undefined) grounding.disposeJournal(journal)
    } finally { cleanup() }
  }
})

const assertRegisteredMutationContinues = async (brokenMapping) => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    prepareWorkspace(directory)
    const sourcePath = join(directory, 'src', 'main.fs')
    const mappingPath = join(directory, 'requirements', 'alpha', 'APPLIES-TO')
    assert.deepEqual(catalog.resolvePackages(directory, sourcePath), ['alpha'])
    if (brokenMapping) {
      writeFileSync(mappingPath, '[\n', 'utf8')
      assert.throws(() => catalog.resolvePackages(directory, sourcePath), /invalid APPLIES-TO pattern/)
    }

    const sessionID = brokenMapping ? 'mutation-broken-mapping' : 'mutation-not-grounded'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    await openIncumbency(runtime, sessionID)
    const expectedContent = 'changed by controlled Host continuation\n'
    const input = { tool: 'todowrite-for-engineer', sessionID, callID: 'write-once' }
    const args = { program: `class Js extends JsProgram { async run() { await this.write('src/main.fs', ${JSON.stringify(expectedContent)}); return 'file written'; } }` }
    const originalInput = structuredClone(input)
    const originalArgs = structuredClone(args)
    const keys = Reflect.ownKeys(args)
    const output = { args }

    await hooks['tool.execute.before'](input, output)
    assert.deepEqual(input, originalInput)
    assert.equal(output.args, args)
    assert.deepEqual(args, originalArgs)
    assert.deepEqual(Reflect.ownKeys(args), keys)
    assert.equal(readFileSync(sourcePath, 'utf8'), 'before\n')

    const execResult = await hooks.tool['todowrite-for-engineer'].execute(output.args, { sessionID, agent: 'engineer' })
    assert.match(String(execResult), /file written/)
    await hooks['tool.execute.after'](
      { ...input, args },
      { title: 'todowrite-for-engineer', output: execResult, metadata: {} },
    )
    assert.equal(readFileSync(sourcePath, 'utf8'), expectedContent)
    assert.equal(output.args, args)
    assert.deepEqual(args, originalArgs)
    assert.deepEqual(Reflect.ownKeys(args), keys)
    assert.equal(readFileSync(mappingPath, 'utf8'), brokenMapping ? '[\n' : '/src/**\n')
  })
}

test('WHAT[requirement-grounding-008] registered hooks allow a controlled Host write to an ungrounded covered path', () =>
  assertRegisteredMutationContinues(false))

test('WHAT[requirement-grounding-008] registered hooks allow a controlled Host write despite invalid APPLIES-TO metadata', () =>
  assertRegisteredMutationContinues(true))
