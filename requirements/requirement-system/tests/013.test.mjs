import assert from 'node:assert/strict'
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  activeBodyViolations,
  frozenOriginViolations,
} from '../../../scripts/lib/spec-rules.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const SMALL_FIX = /普通小型修复[、,].{0,40}不要求创建 Change/

const BLOCKER_STEPS = [
  /停止受影响的\s*产品语义修改/,
  /Blockers/,
  /报告用户/,
  /Amendment/,
]

const COMPLETED_NOT_CURRENT = /Completed.{0,40}不解释当前产品行为/

const ACTIVE_ORIGIN = /Original proposal|Work origin|用户已冻结的裁决/

test('WHAT[REQUIREMENT-SYSTEM-013] Completed is not current product behavior', () => {
  const what = read('requirements/requirement-system/WHAT.md')
  const section = what.slice(what.indexOf('## REQUIREMENT-SYSTEM-013'))
  const body = section.slice(0, section.indexOf('## REQUIREMENT-SYSTEM-014'))
  assert.match(body, COMPLETED_NOT_CURRENT)
  const dropped = body.replace(COMPLETED_NOT_CURRENT, '')
  assert.doesNotMatch(dropped, COMPLETED_NOT_CURRENT)
})

test('WHAT[REQUIREMENT-SYSTEM-013] live Active files declare frozen origin', () => {
  const live = join(ROOT, 'changes/active')
  if (!existsSync(live)) return
  const files = readdirSync(live).filter((name) => name.endsWith('.md'))
  for (const name of files) {
    const path = join(live, name)
    if (!statSync(path).isFile()) continue
    assert.match(
      readFileSync(path, 'utf8'),
      ACTIVE_ORIGIN,
      `${name}: live Active must carry Original proposal / Work origin / 冻结裁决`,
    )
  }
})

test('WHAT[REQUIREMENT-SYSTEM-013] activeBodyViolations accepts frozen origin with all allowed sections', () => {
  const valid = [
    '# CHG-042: some change',
    '',
    '## Original proposal',
    'Frozen text that must not be rewritten.',
    '',
    '## Remaining work',
    '- item A',
    '',
    '## Completion criteria',
    '- criteria X',
    '',
    '## Blockers',
    '- blocker Y',
    '',
    '## Amendments',
    '- amendment Z (user-approved)',
    '',
    '## Final outcome',
    'Done.',
  ].join('\n')
  assert.deepEqual(activeBodyViolations(valid), [])
})

test('WHAT[REQUIREMENT-SYSTEM-013] activeBodyViolations accepts Work origin as frozen boundary', () => {
  const valid = [
    '## Work origin',
    'Frozen.',
    '',
    '## Remaining work',
    '- item A',
  ].join('\n')
  assert.deepEqual(activeBodyViolations(valid), [])
})

test('WHAT[REQUIREMENT-SYSTEM-013] activeBodyViolations rejects missing frozen origin', () => {
  const noOrigin = [
    '# CHG-042: some change',
    '',
    '## Remaining work',
    '- item A',
  ].join('\n')
  const findings = activeBodyViolations(noOrigin)
  assert.equal(findings.length, 1)
  assert.equal(findings[0].rule, 'frozen-origin')
})

test('WHAT[REQUIREMENT-SYSTEM-013] activeBodyViolations fails closed on forbidden progress/commit/code-snapshot sections', () => {
  const forbidden = [
    '## Original proposal',
    'Frozen.',
    '',
    '## Progress',
    '90% complete',
    '',
    '## Commits',
    '- abc123',
    '',
    '## Code snapshot',
    '```js',
    "console.log(1)",
    '```',
    '',
    '## Completion percentage',
    '95%',
    '',
    '## Diff',
    '...',
    '',
    '## Changelog',
    '- v1.2.3',
  ].join('\n')
  const findings = activeBodyViolations(forbidden)
  const forbiddenRules = findings.filter((f) => f.rule === 'forbidden-section')
  assert.equal(forbiddenRules.length, 6)
  assert.ok(
    findings.every((f) => f.rule === 'forbidden-section'),
    'no unknown-section leakage — forbidden names are recognized, not generic',
  )
})

test('WHAT[REQUIREMENT-SYSTEM-013] activeBodyViolations rejects non-whitelisted sections', () => {
  const unknown = [
    '## Work origin',
    'Frozen.',
    '',
    '## Random notes',
    'something',
  ].join('\n')
  assert.deepEqual(activeBodyViolations(unknown), [
    {
      rule: 'unknown-section',
      line: 4,
      msg: 'Active section "Random notes" is not in the allowed whitelist',
    },
  ])
})

test('WHAT[REQUIREMENT-SYSTEM-013] activeBodyViolations ignores CHG-NNN document title as a section', () => {
  const withChgTitle = [
    '# CHG-099: my change',
    '',
    '## Original proposal',
    'Frozen.',
    '',
    '## Remaining work',
    '- item A',
  ].join('\n')
  assert.deepEqual(activeBodyViolations(withChgTitle), [])
})

test('WHAT[REQUIREMENT-SYSTEM-013] frozen origin permits append-only Active changes', () => {
  const before = ['# CHG-099: my change', '', '## Original proposal', 'Frozen.'].join('\n')
  const after = [
    before,
    '',
    '## Remaining work',
    '- item A',
    '',
    '## Amendments',
    '- approved amendment',
  ].join('\n')
  assert.deepEqual(frozenOriginViolations(before, after), [])
})

test('WHAT[REQUIREMENT-SYSTEM-013] frozen origin rejects rewritten proposal text', () => {
  const before = ['# CHG-099: my change', '', '## Original proposal', 'Frozen.'].join('\n')
  const after = ['# CHG-099: my change', '', '## Original proposal', 'Rewritten.'].join('\n')
  assert.deepEqual(frozenOriginViolations(before, after), [
    {
      rule: 'frozen-origin-mutated',
      line: 3,
      msg: 'Active Original proposal / Work origin text must remain byte-identical',
    },
  ])
})

test('WHAT[REQUIREMENT-SYSTEM-013] frozen origin requires both revisions to declare origin', () => {
  const before = '## Original proposal\nFrozen.'
  const after = '## Remaining work\n- item A'
  assert.deepEqual(frozenOriginViolations(before, after), [
    {
      rule: 'frozen-origin-missing',
      line: 0,
      msg: 'Both Active revisions must carry an Original proposal / Work origin section',
    },
  ])
})
