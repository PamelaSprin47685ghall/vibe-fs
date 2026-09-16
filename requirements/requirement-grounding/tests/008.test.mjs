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
