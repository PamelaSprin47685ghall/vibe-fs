import assert from 'node:assert/strict'
import test from 'node:test'

import { ProviderRequestKind } from '../../../dist/Domain/PrefixCandidate.js'
import {
  appendEntry,
  hasOpening,
  mayInvokeTeacher,
  teacherAgentFor,
  toolsFor,
} from '../../../dist/Domain/StudentTeacher.js'
import { targetName, validateDocument } from '../../../dist/Domain/StudentSkill.js'
import { compile, compileNudge, teacherQuestion } from '../../../dist/Domain/StudentTeacherPrompt.js'
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

test('AGENT_021_completed_teacher_return_closes_teacher_invocation_window', () => {
  assert.equal(mayInvokeTeacher({ tag: 0 }), true)

  // CompileDispatching is the state immediately after a completed Teacher
  // return; it must remain ineligible until the compile handoff is complete.
  assert.equal(mayInvokeTeacher({ tag: 2 }), false)

  // TeacherWaiting and the remaining terminal handoff states are also closed.
  for (const state of [{ tag: 1 }, { tag: 3 }, { tag: 4 }]) {
    assert.equal(mayInvokeTeacher(state), false)
  }
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

test('AGENT_022_StudentCompile_accepts_only_the_OpenCode_SKILL_document_layout', () => {
  const accepted = targetName('.agent/skills/blogger-blog-nudge/SKILL.md')
  assert.equal(accepted.tag, 0)
  assert.equal(accepted.fields[0], 'blogger-blog-nudge')

  for (const rejected of [
    '.agent/skills/blogger-blog-nudge.md',
    '.agent\\skills\\blogger-blog-nudge\\SKILL.md',
    '.agent/skills/.hidden/SKILL.md',
    '.agents/skills/blogger-blog-nudge/SKILL.md',
    '.agent/skills/blogger-blog-nudge/notes.md',
    '.agent/skills/nested/blogger-blog-nudge/SKILL.md',
    '../.agent/skills/blogger-blog-nudge/SKILL.md',
    '/tmp/.agent/skills/blogger-blog-nudge/SKILL.md',
  ]) {
    assert.equal(targetName(rejected).tag, 1, `${rejected} must be rejected`)
  }
})

test('AGENT_022_SKILL_requires_matching_name_description_and_non_empty_body', () => {
  const valid = [
    '---',
    'name: blogger-blog-nudge',
    'description: Diagnose and repair Blogger nudge behavior.',
    '---',
    '',
    '# Blogger blog nudge',
    '',
    'Preserve the causal boundary.',
    '',
  ].join('\n')

  assert.equal(validateDocument('blogger-blog-nudge', valid).tag, 0)

  const failures = [
    '# no frontmatter',
    valid.replace('name: blogger-blog-nudge', 'name: other'),
    valid.replace('description: Diagnose and repair Blogger nudge behavior.\n', ''),
    '---\nname: blogger-blog-nudge\ndescription: empty body\n---\n',
  ]

  for (const content of failures) {
    assert.equal(validateDocument('blogger-blog-nudge', content).tag, 1)
  }
})

test('AGENT_022_StudentCompile_prompt_states_the_loadable_layout_and_restart_boundary', () => {
  for (const prompt of [compile('/private/QA.md'), compileNudge]) {
    assert.match(prompt, /\.agent\/skills\/<skill-name>\/SKILL\.md/)
    assert.match(prompt, /frontmatter/)
    assert.match(prompt, /name/)
    assert.match(prompt, /description/)
    assert.match(prompt, /非空.*正文/)
  }
  assert.match(compile('/private/QA.md'), /重启 OpenCode/)
})

test('AGENT_020_Teacher_prompt_carries_commented_question_without_qa_path_or_data_fields', () => {
  const prompt = teacherQuestion('What is the smallest principle?', false)
  assert.equal(prompt.includes('qa_path'), false)
  assert.equal(prompt.includes('question ='), false)
  assert.match(prompt, /^# Answer the Student's current question/m)
  assert.match(prompt, /^# What is the smallest principle\?$/m)

  const replacementPrompt = teacherQuestion('What is the smallest principle?', true)
  assert.match(replacementPrompt, /disaster-recovery replacement Teacher/)
  assert.match(replacementPrompt, /^# What is the smallest principle\?$/m)
})
