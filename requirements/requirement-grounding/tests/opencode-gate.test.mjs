import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as grounding from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const pluginHooksSource = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs', import.meta.url), 'utf8')
const pluginTransformsSource = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs', import.meta.url), 'utf8')

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-opencode-'))
  mkdirSync(join(dir, 'requirements', 'alpha'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'ground truth\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'before\n', 'utf8')
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolBatch = (providerID, path) => [
  { info: { id: 'c1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'pending', input: { filePath: path }, time: { start: 0 } } }] },
  { info: { id: 'r1', role: 'assistant', providerID }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'completed', input: { filePath: path }, output: 'before\n', time: { start: 0, end: 0 } } }] },
]

test('WHAT[REQUIREMENT-GROUNDING-007] ordinary providers replay anchored read call-result pairs while Cursor appends NUL-BOM result-only bytes after the pseudo-skill with stable source-path attributes', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    const ordinaryJournal = await grounding.createJournal(dir)
    await grounding.requestPaths(ordinaryJournal.journal, dir, 'ordinary', [sourcePath])
    const paired = await pair.tryInject('ordinary', pair.text, toolBatch('anthropic', sourcePath))
    assert.equal(paired.ok, true)
    const ordinary = await grounding.projectWithJournal(ordinaryJournal.journal, 'ordinary', paired.value)
    const names = ordinary.value.map((m) => m.parts?.[0]?.tool).filter(Boolean)
    assert.ok(names.lastIndexOf('skill') < names.lastIndexOf('read'))
    grounding.disposeJournal(ordinaryJournal.journal)

    const cursorJournal = await grounding.createJournal(dir)
    await grounding.requestPaths(cursorJournal.journal, dir, 'cursor', [sourcePath])
    const cursorPair = await pair.tryInject('cursor', pair.text, toolBatch('cursor', sourcePath))
    const cursor = await grounding.projectWithJournal(cursorJournal.journal, 'cursor', cursorPair.value)
    const terminal = cursor.value.at(-1).parts[0].state.output
    const skillAt = terminal.indexOf('<skill_content name="">')
    const requirementAt = terminal.indexOf('<requirement_read path="requirements/alpha/WHAT.md">')
    assert.ok(skillAt >= 0 && requirementAt > skillAt)
    assert.ok(terminal.includes(`${grounding.cursorSeparator}<requirement_read`))
    assert.equal(cursor.value.some((m) => m.info?.source === grounding.source), false, 'Cursor has result-only grounding')

    const score = pluginTransformsSource.slice(
      pluginTransformsSource.indexOf('let normalTransform'),
      pluginTransformsSource.indexOf('let private ordinaryProviderTransform'),
    )
    const pairProjectionAt = score.indexOf('caps.InjectPairGuideline')
    const requirementProjectionAt = score.indexOf('caps.ProjectRequirementGrounding')
    assert.ok(pairProjectionAt >= 0 && requirementProjectionAt > pairProjectionAt, 'production transform fixes pair → grounding order')
    grounding.disposeJournal(cursorJournal.journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-007] grep match files do not trigger APPLIES-TO before an explicit read', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    const opened = await grounding.createJournal(dir)

    const grep = await grounding.observationDecision(
      opened.journal,
      dir,
      'grep-does-not-ground',
      'grep',
      { path: join(dir, 'src') },
      `${sourcePath}:1:before\n`,
    )
    assert.equal(grep.ok, true)
    assert.equal(grep.needsGrounding, false)
    assert.equal(grep.requested, 0)
    assert.deepEqual(grep.packages, [])

    const read = await grounding.observationDecision(
      opened.journal,
      dir,
      'grep-does-not-ground',
      'read',
      { filePath: sourcePath },
      'before\n',
    )
    assert.equal(read.ok, true)
    assert.equal(read.needsGrounding, true)
    assert.equal(read.requested, 1)
    assert.deepEqual(read.packages, ['alpha'])

    grounding.disposeJournal(opened.journal)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-008] mutation grounding is weak observation and never becomes tool admission', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const sourcePath = join(dir, 'src', 'main.fs')
    const opened = await grounding.createJournal(dir)
    const first = await grounding.mutationDecision(opened.journal, dir, 'mutation', [sourcePath])
    assert.equal(first.allowed, true)
    assert.equal(first.needsGrounding, true)
    assert.deepEqual(first.packages, ['alpha'])

    const gateAt = pluginHooksSource.indexOf('RequirementGroundingGate.before')
    const ordinaryBeforeWorkAt = pluginHooksSource.indexOf('ToolHostCodec.decodeContext', gateAt)
    assert.ok(gateAt >= 0 && ordinaryBeforeWorkAt > gateAt, 'grounding may observe before mutation without owning admission')
    assert.doesNotMatch(pluginHooksSource, /RequirementGroundingGate\.RequiredError/)
    assert.doesNotMatch(pluginHooksSource, /expectedRejectionHook[\s\S]*?requirement-grounding/i)

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
    grounding.disposeJournal(opened.journal)
  } finally { cleanup() }
})

