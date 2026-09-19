import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const here = dirname(fileURLToPath(import.meta.url))

const providerRoot = join(here, '../../../resources/provider')

const readLaw = (semanticPath, locale) => readFileSync(join(providerRoot, semanticPath, `${locale}.md`), 'utf8')

const LOCALES = ['en', 'zh-CN']

test('WHAT[repository-investigation-005] INVESTIGATE_inspector_role_law_pins_observe_without_changing', () => {
  for (const locale of LOCALES) {
    const law = readLaw('role/engineer', locale)
    assert.match(law, /observe without changing|只观察，不改变/, `${locale} observe without changing`)
  }
})
