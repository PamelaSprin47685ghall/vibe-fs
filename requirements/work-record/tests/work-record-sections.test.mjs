// COMPANION-003 / §18 — WorkRecord exposes exactly three canonical section headings.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const LWR_SOURCE = new URL('../../../src/Wanxiangshu/Mission/WorkRecord/Model.fs', import.meta.url)

const CANONICAL = ['Opening', 'Chronicle', 'Recent work']
const LEGACY = ['Opening task', 'Work log', 'Uncompressed tail', 'Final output']

test('WORK_RECORD_SECTIONS_lifecycle_source_declares_three_canonical_headings', () => {
  const source = readFileSync(LWR_SOURCE, 'utf8')
  for (const heading of CANONICAL) {
    assert.match(source, new RegExp(`"${heading.replace(' ', ' ')}"`))
  }
  for (const heading of LEGACY) {
    assert.equal(source.includes(`"${heading}"`), false, `legacy heading must be absent: ${heading}`)
  }
  assert.equal(source.includes('"Closing report"'), false, 'Closing report heading must be absent')
})
