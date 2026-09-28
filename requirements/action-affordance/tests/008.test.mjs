import assert from 'node:assert/strict'
import test from 'node:test'
import { extractToolSpecNames, scanEntries } from '../../../scripts/checks/tool-referential-integrity.mjs'

const entry = (owner, name) => ({
  file: `src/Wanxiangshu/OpenCode/Tools/${owner}.fs`,
  text: `module ${owner} =\n    let spec factory =\n        { Name = "${name}"\n          Description = "fixture"\n          Arguments = []\n          Execute = fun _ _ -> task { return "" } }`,
})

test('WHAT[action-affordance-008] the actual ownership scanner accepts distinct names and rejects two detected owners of one name', () => {
  const fork = entry('AlphaTool', 'fork')
  assert.deepEqual(extractToolSpecNames(fork.file, fork.text).map((item) => item.name), ['fork'])
  assert.deepEqual(scanEntries([fork, entry('BetaTool', 'join')]), [])
  assert.ok(scanEntries([fork, entry('BetaTool', 'fork')]).some((item) => item.code === 'duplicate-tool-owner'))
})

test.todo('WHAT[action-affordance-008] same-name schemas, lifecycle and outcomes have identical meaning in all projections; detecting owner names alone does not establish this (GAP-078)')
