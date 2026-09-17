import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const here = dirname(fileURLToPath(import.meta.url))

const providerRoot = join(here, '../../../resources/provider')

const readLaw = (semanticPath, locale) => readFileSync(join(providerRoot, semanticPath, `${locale}.md`), 'utf8')

const LOCALES = ['en', 'zh-CN']

test('WHAT[REPOSITORY-INVESTIGATION-003] INVESTIGATE_inspector_role_law_layers_reasoning_below_evidence_acquisition', () => {
  for (const locale of LOCALES) {
    const law = readLaw('role/engineer', locale)
    // Reasoning/evidence layering: a mechanical trail of searches is not a
    // method — reasoning may decide WHAT to ask, it does not produce evidence.
    assert.match(
      law,
      /A mechanical trail of searches is not a method|一连串机械搜索不是方法/,
      `${locale} reasoning is not evidence acquisition`,
    )
  }
})
