import assert from 'node:assert/strict'
import test from 'node:test'

import { ProviderRequestKind } from '../../../dist/Domain/PrefixCandidate.js'
import {
  appendEntry,
  hasOpening,
  teacherAgentFor,
  toolsFor,
} from '../../../dist/Domain/StudentTeacher.js'
import { AgentTier, Role } from '../../../dist/Kernel/Roles.js'
import {
  StaticTools_requestToolMap as requestToolMap,
  StaticTools_toolName as toolName,
} from '../../../dist/Tools/StaticTools.js'
import { toArray as setToArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { toArray as mapToArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'

const names = (permissions) => setToArray(permissions).map(toolName).sort()
const schema = (role, requestKind) => Object.fromEntries(mapToArray(requestToolMap(toolsFor(role, requestKind))))

test('AGENT_021_StudentLearn_and_StudentCompile_have_complete_disjoint_tool_maps', () => {
  const learn = schema(Role.Student, ProviderRequestKind.StudentLearn)
  const compile = schema(Role.Student, ProviderRequestKind.StudentCompile)

  assert.deepEqual(Object.entries(learn).filter(([, allowed]) => allowed).map(([name]) => name), ['teacher'])
  assert.deepEqual(
    Object.entries(compile).filter(([, allowed]) => allowed).map(([name]) => name).sort(),
    ['edit', 'glob', 'grep', 'read', 'return', 'write'],
  )
  assert.equal(learn.read, false)
  assert.equal(learn.return, false)
  assert.equal(compile.teacher, false)
  assert.equal(Object.keys(learn).length, Object.keys(compile).length)
  assert.equal(Object.keys(learn).length >= 20, true, 'request maps must explicitly deny the known complement')
})

test('AGENT_020_Teacher_has_execution_tools_but_no_fork_list_join_or_PTY', () => {
  const allowed = names(toolsFor(Role.Teacher, ProviderRequestKind.StudentLearn))

  assert.deepEqual(allowed, [
    'coder',
    'edit',
    'executor',
    'glob',
    'grep',
    'inspector',
    'mv',
    'network',
    'read',
    'return',
    'rm',
    'write',
  ])
  for (const forbidden of ['fork', 'fork-manager', 'fork-pty', 'join', 'list']) {
    assert.equal(allowed.includes(forbidden), false)
  }
})

test('AGENT_020_Student_and_Teacher_keep_the_same_tier', () => {
  assert.equal(teacherAgentFor(AgentTier.Fast), 'fast-teacher')
  assert.equal(teacherAgentFor(AgentTier.Deep), 'deep-teacher')
})

test('PERSIST_011_framework_separator_does_not_trim_verbatim_input_bytes', () => {
  assert.equal(appendEntry('first\n', 'second'), 'first\n\n\nsecond')
})

test('PERSIST_011_replayed_HumanRoot_is_proved_from_the_exact_QA_opening', () => {
  assert.equal(hasOpening('root\n\nquestion\n\nanswer', 'root'), true)
  assert.equal(hasOpening('root suffix\n\nquestion', 'root'), false)
  assert.equal(hasOpening('other\n\nroot', 'root'), false)
})
