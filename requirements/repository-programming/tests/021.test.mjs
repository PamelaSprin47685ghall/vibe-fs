import assert from 'node:assert/strict'
import test from 'node:test'
import {
  HANDWRITTEN_ROLE_TOOL_TOKENS,
  scanEntries,
} from '../../../scripts/checks/js-surface-gate.mjs'



test('WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_handwritten_tokens_use_inquiry_not_meditator', () => {
  assert.ok(HANDWRITTEN_ROLE_TOOL_TOKENS.includes('js-inquiry'))
  assert.ok(!HANDWRITTEN_ROLE_TOOL_TOKENS.includes('js-meditator'))
})

test('WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_rejects_handwritten_js_coder_outside_permission_matrix', () => {
  const hits = scanEntries([
    {
      file: 'src/Wanxiangshu/Tools/Fake.fs',
      text: 'let x = "js-coder"\n',
    },
  ])
  assert.equal(hits.length, 1)
  assert.equal(hits[0].kind, 'handwritten-role-tool')
})

test('WHAT[REPOSITORY-PROGRAMMING-021] JS_SURFACE_GATE_allows_permission_matrix_enumeration', () => {
  const hits = scanEntries([
    {
      file: 'src/Wanxiangshu/OpenCode/Tools/StaticTools.fs',
      text: 'let knownToolNames = ["js-engineer"; "js-devops"; "js-bookkeeper"; "js-manager"; "js-orchestrator"; "js-blogger"]\n',
    },
  ])
  assert.deepEqual(hits, [])
})
