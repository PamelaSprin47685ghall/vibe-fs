import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const root = resolve(join(dirname(fileURLToPath(import.meta.url)), '../../..'))
const read = (path) => readFileSync(join(root, path), 'utf8')

test('WHAT[PROVIDER-PROJECTION-013] LLM_FACING_single_representation_owner_is_hard_gated', () => {
  const result = spawnSync(process.execPath, [join(root, 'scripts/checks/llm-facing-format-gate.mjs')], {
    cwd: root,
    encoding: 'utf8',
  })

  assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`)
  const owner = read('src/Wanxiangshu/Foundation/LlmFacing.fs')
  assert.match(owner, /type Document =/)
  assert.match(owner, /let render \(document: Document\)/)
})

test('WHAT[PROVIDER-PROJECTION-014] LLM_FACING_composition_stays_typed_until_the_final_render', () => {
  const syncStore = read('src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs')
  const warmStart = read('src/Wanxiangshu/Repository/Investigation/WarmStart/Prompt.fs')
  const joinRenderer = read('src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinResultRenderer.fs')
  const narrative = read('src/Wanxiangshu/Mission/Manager/Narrative.fs')

  assert.match(syncStore, /PrepareProviderPrompt: unit -> Task<LlmFacing\.Document>/)
  assert.match(warmStart, /baseDocument: LlmFacing\.Document/)
  assert.doesNotMatch(warmStart, /basePrompt\.TrimEnd/)
  assert.doesNotMatch(joinRenderer, /String\.concat "\\n\\n"/)
  assert.doesNotMatch(narrative, /SyntheticToml\.comment|header \+ "\\n\\n"/)
})
