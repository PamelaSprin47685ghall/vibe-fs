// tests-mjs/StudentTeacher/student-teacher.test.mjs — SSOT/16 LEARN-015/016/017/
// 024/032/033/035/036/050/051/075. LEARN-089 pure-logic tests: Student tool-face
// selection, Teacher tier mapping, QA path generation, atomic content append,
// temp-path ignore, return cleanup result, nudge choice.

import assert from 'node:assert/strict'
import test from 'node:test'
import { studentTeacher, requestKind } from '../domain.mjs'

// ── tool face selection (LEARN-050/051) ─────────────────────────────────────

test('LEARN_050_student_learn_tool_face_is_exactly_teacher', () => {
  assert.deepEqual(studentTeacher.toolsFor('StudentLearn'), ['Teacher'])
})

test('LEARN_050_student_compile_tool_face_is_read_glob_grep_write_edit_return', () => {
  assert.deepEqual(studentTeacher.toolsFor('StudentCompile'), [
    'Edit',
    'Glob',
    'Grep',
    'Read',
    'Return',
    'Write',
  ])
})

test('LEARN_050_compile_face_excludes_teacher', () => {
  const tools = studentTeacher.toolsFor('StudentCompile')
  assert.ok(!tools.includes('Teacher'), 'compile face must not include teacher')
})

test('LEARN_051_non_student_request_kinds_get_an_empty_tool_face', () => {
  for (const kind of [requestKind.workMain, requestKind.bloggerMain, requestKind.interactionRepair]) {
    assert.deepEqual(studentTeacher.toolsForKind(kind), [], `kind ${requestKind.nameOf(kind)} must fail closed`)
  }
})

test('LEARN_050_tool_face_is_determined_by_request_kind_not_runtime_flags', () => {
  // The same Student canonical role with two request kinds has two faces; there
  // is no third "switch" flag to consult.
  assert.deepEqual(studentTeacher.toolsFor('StudentLearn'), ['Teacher'])
  assert.deepEqual(studentTeacher.toolsFor('StudentCompile').sort(), ['Edit', 'Glob', 'Grep', 'Read', 'Return', 'Write'])
})

// ── tier mapping (LEARN-017) ────────────────────────────────────────────────

test('LEARN_017_teacher_tier_matches_student_tier', () => {
  assert.equal(studentTeacher.teacherTier('Fast'), 'Fast')
  assert.equal(studentTeacher.teacherTier('Deep'), 'Deep')
})

test('LEARN_016_student_is_public_teacher_is_internal_agent_names', () => {
  assert.equal(studentTeacher.studentAgent('Fast'), 'fast-student')
  assert.equal(studentTeacher.studentAgent('Deep'), 'deep-student')
  assert.equal(studentTeacher.teacherAgent('Fast'), 'fast-teacher')
  assert.equal(studentTeacher.teacherAgent('Deep'), 'deep-teacher')
})

// ── QA.md（LEARN-032/033/035/036）──────────────────────────────────────────

test('LEARN_032_tmp_paths_are_recognized_as_git_ignored', () => {
  assert.equal(studentTeacher.isIgnoredTmpPath('.agent/.tmp/student/s1/run1/QA.md'), true)
  assert.equal(studentTeacher.isIgnoredTmpPath('.agent/.tmp'), true)
  assert.equal(studentTeacher.isIgnoredTmpPath('src/QA.md'), false)
})

test('LEARN_032_neighbor_paths_do_not_false_positive', () => {
  // The separator-bound match must not treat `.agent/.tmpx` (a sibling
  // directory) as the QA tmp root.
  assert.equal(studentTeacher.isIgnoredTmpPath('.agent/.tmpx/QA.md'), false)
  assert.equal(studentTeacher.isIgnoredTmpPath('.agent/.tmpfoo/QA.md'), false)
})

test('LEARN_033_append_joins_with_minimal_separator_not_markup', () => {
  const first = studentTeacher.append('', 'What does the system need?')
  assert.equal(first, 'What does the system need?')
  const second = studentTeacher.append(first, 'Teacher: it needs a durable fold.')
  assert.equal(second, 'What does the system need?\n\nTeacher: it needs a durable fold.')
})

test('LEARN_033_no_separator_lines_or_titles_are_inserted', () => {
  const appended = studentTeacher.append('first', 'second')
  assert.ok(!appended.includes('---'), 'no divider lines')
  assert.ok(!appended.includes('# '), 'no headers injected by the framework')
})

test('LEARN_035_first_entry_is_the_raw_user_request', () => {
  const qa = studentTeacher.append('', 'Read this repo and tell me what to learn.')
  assert.equal(qa, 'Read this repo and tell me what to learn.')
})

test('LEARN_037_duplicate_tail_is_not_rewritten', () => {
  const qa = studentTeacher.append('first', 'second')
  const deduped = studentTeacher.dedupeTail(qa, 'second')
  assert.equal(deduped, qa)
  const different = studentTeacher.dedupeTail(qa, 'third')
  assert.equal(different, 'first\n\nsecond\n\nthird')
})

// ── single-flight concurrency (LEARN-075) ───────────────────────────────────

test('LEARN_075_teacher_call_requires_idle', () => {
  assert.equal(studentTeacher.mayStartTeacherCall('Idle'), true)
  assert.equal(studentTeacher.mayStartTeacherCall('TeacherInFlight'), false)
  assert.equal(studentTeacher.mayStartTeacherCall('CompileInFlight'), false)
})

// ── final return delete order (LEARN-024) ───────────────────────────────────

test('LEARN_024_return_proceeds_only_after_delete_or_absent', () => {
  assert.equal(studentTeacher.returnMayProceed('Deleted'), true)
  assert.equal(studentTeacher.returnMayProceed('AlreadyAbsent'), true)
  assert.equal(studentTeacher.returnMayProceed('DeleteFailed'), false)
})
