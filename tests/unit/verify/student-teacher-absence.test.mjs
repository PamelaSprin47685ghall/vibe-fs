/**
 * G9 / Playbook §24.1 Student–Teacher absence ratchet.
 *
 * Pins scripts/checks/student-teacher-absence.mjs: FORBIDDEN_TOKENS + scanEntries.
 * Import-safe (CLI main guard). Full production scan of src/Wanxiangshu +
 * resources/prompts is run by `node scripts/checks/student-teacher-absence.mjs`
 * via scripts/check.mjs — this unit nails the token set and scanEntries
 * red/green, then samples src/Wanxiangshu.
 */
import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { walk } from '../../../scripts/lib/walk.mjs'
import {
  FORBIDDEN_TOKENS,
  scanEntries,
} from '../../../scripts/checks/student-teacher-absence.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname

const REQUIRED_TOKENS = [
  'Role.Student',
  'Role.Teacher',
  'fast-student',
  'deep-student',
  'fast-teacher',
  'deep-teacher',
  'StudentLearn',
  'StudentCompile',
  'StudentQaStore',
  'StudentTeacherRuntime',
  'StudentTeacherTools',
  'StudentSkill',
  'SatelliteKind.Teacher',
  'SatelliteKind.Replica',
]

test('student_teacher_absence_documents_forbidden_tokens', () => {
  for (const token of REQUIRED_TOKENS) {
    assert.ok(FORBIDDEN_TOKENS.includes(token), `missing token: ${token}`)
  }
})

test('student_teacher_absence_fixture_red_for_role_student_and_replica', () => {
  const text = [
    'module Fake',
    'let role = Role.Student',
    'let kind = SatelliteKind.Replica',
  ].join('\n')
  const hits = scanEntries([{ file: 'Domain/Fake.fs', text }])
  assert.ok(hits.some((h) => h.token === 'Role.Student' && h.line === 2))
  assert.ok(hits.some((h) => h.token === 'SatelliteKind.Replica' && h.line === 3))
  assert.equal(hits[0].file, 'Domain/Fake.fs')
})

test('student_teacher_absence_clean_text_is_green', () => {
  const text = [
    'module Roles',
    'type Role =',
    '    | Coder',
    '    | Inspector',
    'type SatelliteKind =',
    '    | Inspector',
    'type AttachmentKind =',
    '    | StrengthReplica',
  ].join('\n')
  assert.deepEqual(scanEntries([{ file: 'Kernel/Roles.fs', text }]), [])
})

test('student_teacher_absence_src_wanxiangshu_sample_is_green', () => {
  const production = join(ROOT, 'src/Wanxiangshu')
  assert.ok(existsSync(production), production)
  const entries = walk(production, ['.fs']).map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  assert.ok(entries.length > 0, 'expected production .fs files')
  const violations = scanEntries(entries)
  assert.deepEqual(
    violations,
    [],
    violations.map((v) => `${v.file}:${v.line} '${v.token}'`).join('\n'),
  )
})
