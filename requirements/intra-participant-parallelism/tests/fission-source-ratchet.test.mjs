import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '../../..')
const read = (p) => readFileSync(resolve(root, p), 'utf8')

const fissionProduction = () => [
  'src/Wanxiangshu/Execution/Fission/Model.fs',
  'src/Wanxiangshu/Execution/Fission/Admission.fs',
  'src/Wanxiangshu/Execution/Fission/Runtime.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
].map(read).join('\n')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors', () => {
  const code = fissionProduction()
  assert.doesNotMatch(code, /session\s*\.\s*fork|\/session\/[^"']*\/fork|CreateForkedSession|ForkSession/i)

  const facts = read('src/Wanxiangshu/Execution/Fission/Facts.fs')
  const fold = read('src/Wanxiangshu/Execution/Fission/Projection.fs') + read('src/Wanxiangshu/Execution/Fission/Fold.fs')
  assert.match(facts, /FissionAdmitted/)
  assert.match(facts, /FissionLaneMaterialized/)
  assert.match(facts, /FissionCompletionDelivered/)
  assert.match(facts, /FissionConverged/)
  assert.match(fold, /FissionAdmitted/)
  assert.match(fold, /FissionConverged/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility comes from ToolPermission.Fission for current office vocabulary', () => {
  const roles = read('src/Wanxiangshu/Foundation/Roles.fs')
  const registry = read('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs')
  assert.match(registry, /"fission"\s*->\s*fun r -> Roles\.isAllowed r ToolPermission\.Fission/)

  for (const role of ['Manager', 'Coder', 'Inspector', 'Browser', 'Inquiry']) {
    const block = new RegExp(`\\| Role\\.${role} ->[\\s\\S]{0,900}?ToolPermission\\.Fission`)
    assert.match(roles, block, `${role} must own the Fission consequence`)
  }
  for (const role of ['Orchestrator', 'DevOps', 'Reviewer', 'Blogger', 'Distiller']) {
    const arm = new RegExp(`\\| Role\\.${role} ->([^\\n]*(?:\\n(?!\\s*\\| Role\\.).*){0,16})`)
    const text = arm.exec(roles)?.[0] ?? ''
    assert.doesNotMatch(text, /ToolPermission\.Fission/, `${role} must not own Fission`)
  }
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-003] sibling creation is a distinct Host capability from managed-child creation', () => {
  const sessions = read('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const port = read('src/Wanxiangshu/OpenCode/Host/OpenCodePort.fs')
  assert.match(sessions, /CreateSiblingSession/)
  assert.match(sessions, /TryGetParentSession/)
  assert.match(port, /CreateSession/)
  assert.match(port, /parentID/)
})
