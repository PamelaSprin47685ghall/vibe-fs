import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const walkMarkdown = (relDir) => {
  const out = []
  const walkDir = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const p = join(dir, entry.name)
      if (entry.isDirectory()) walkDir(p)
      else if (entry.name.endsWith('.md')) out.push(readFileSync(p, 'utf8'))
    }
  }
  walkDir(join(ROOT, relDir))
  return out
}

const ROLE_LAW_ROLES = Object.freeze([
  'manager',
  'coder',
  'inspector',
  'devops',
  'orchestrator',
  'blogger',
  'distiller',
  'bookkeeper',
])

const MIRRORED_BY_OFFICE_CAPABILITY = new Set(['entrust-by-consequence', 'choose-by-return', 'no-omnipotent-charge'])

const LANGUAGE = 'English'

test('WHAT[cognitive-environment-012] CE_012_relay_assessment_prompt_carries_ledger_without_process_mechanics', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/relay/quality-ledger/${locale}.md`)
    assert.match(text, /Ledger|judgment|acceptance/i, 'Relay assessment prompt carries Ledger guidance')
    assert.match(text, /independent|独立/, `${locale}: assessment prompt must teach independent judgement`)
    assert.doesNotMatch(
      text,
      /\bbarrier\b|\b2N\b|\bwitness\b|\bcohort\b|confirmation rounds|dedicated session|双 PERFECT|双完美/i,
      `${locale}: hidden PERFECT-process mechanics must not enter the assessment prompt`,
    )
  }
})
