import assert from 'node:assert/strict'
import test from 'node:test'

import * as providerLanguage from '../../../dist/Participant/Provider/LanguageSurface.js'

test('WHAT[DG-011] LOOP_006_anomaly_resources_preserve_distinct_recovery_meanings', () => {
  assert.equal(
    providerLanguage.readText('SimplifiedChinese', 'runtime/degeneration-too-repetitive').trim(),
    '你的输出重复字符太多，建议更换表述方式。',
  )
  assert.equal(
    providerLanguage.readText('SimplifiedChinese', 'runtime/degeneration-too-random').trim(),
    '你的输出重复字符太少，不符合正常语料模式，建议更换表述方式。',
  )

  assert.match(
    providerLanguage.readText('English', 'runtime/degeneration-too-repetitive'),
    /too many repeated characters/i,
  )
  assert.match(
    providerLanguage.readText('English', 'runtime/degeneration-too-random'),
    /too few repeated characters/i,
  )
})
