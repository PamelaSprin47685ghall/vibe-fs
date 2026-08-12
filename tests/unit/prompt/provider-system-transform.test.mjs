import assert from 'node:assert/strict'
import test from 'node:test'

import { create as transformSystem } from '../../../dist/Infrastructure/OpenCode/Host/ProviderSystemTransform.js'
import {
  BookkeeperRuntime_bindSession as bindBookkeeper,
  BookkeeperRuntime_unbindSession as unbindBookkeeper,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'
import { promptResources, providerLanguage, sessionId } from '../support/domain.mjs'

const SID = 'provider-system-i18n-bookkeeper'

test.beforeEach(() => providerLanguage.clearAllForTests())
test.afterEach(() => {
  unbindBookkeeper(SID)
  providerLanguage.clearAllForTests()
})

test('PROMPT_017_system_transform_localizes_only_wanxiangshu_owned_segment', async () => {
  const sid = sessionId(SID)
  assert.equal(providerLanguage.bindOnce(sid, providerLanguage.simplifiedChinese).ok, true)
  bindBookkeeper(SID, 'tx-i18n', 'owner-i18n')

  const english = promptResources.loadBookkeeperSystemFor(providerLanguage.english)
  const chinese = promptResources.loadBookkeeperSystemFor(providerLanguage.simplifiedChinese)
  const hostOwned = 'HOST-OWNED-SYSTEM-BYTES'
  const output = { system: [english, hostOwned] }

  await transformSystem(undefined, { sessionID: SID, model: {} }, output)

  assert.equal(output.system[0], chinese)
  assert.equal(output.system[1], hostOwned)
  assert.match(output.system[0], /^# 共同法/)
})

test('PROMPT_017_system_transform_is_stable_for_english_session', async () => {
  const sid = sessionId(SID)
  assert.equal(providerLanguage.bindOnce(sid, providerLanguage.english).ok, true)
  bindBookkeeper(SID, 'tx-i18n', 'owner-i18n')

  const english = promptResources.loadBookkeeperSystemFor(providerLanguage.english)
  const output = { system: [english, 'OTHER'] }
  await transformSystem(undefined, { sessionID: SID, model: {} }, output)

  assert.deepEqual(output.system, [english, 'OTHER'])
})
