import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as calibration from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const comment = (text) => text.trim().split('\n').map((line) => line === '' ? '#' : `# ${line}`).join('\n') + '\n'

const readResource = (name, resource) =>
  readFileSync(new URL(`../../../resources/provider/host/${name}/${resource}.md`, import.meta.url), 'utf8').trim()

test('WHAT[guidance-delivery-012] actual composition preserves all provided dynamic fragments and omits absent ones', () => {
  const fragments = ['tip identity', 'elapsed calibration', 'remaining estimate', 'guideline body']
  assert.equal(calibration.composeWithElapsed(...fragments), comment(fragments.join('\n')))
  assert.equal(calibration.compose(undefined, undefined, fragments[3]), comment(fragments[3]))
})

for (const [locale, resource] of [['English', 'en'], ['SimplifiedChinese', 'zh-CN']]) {
  test(`WHAT[guidance-delivery-012] ${locale} calibration states the four numbers from the authored resource`, () => {
    const template = readResource('pair-programming-budget', resource)
    const cases = [
      [4, 6, 125000, '2 minutes 5 seconds', '3 minutes 7 seconds', '2 分钟 5 秒', '3 分钟 7 秒'],
      [0, 2, 90000, '1 minute 30 seconds', '3 minutes 0 seconds', '1 分钟 30 秒', '3 分钟 0 秒'],
    ]

    for (const [turnsUsed, turnsLeft, elapsedMs, enUsed, enLeft, zhUsed, zhLeft] of cases) {
      const used = locale === 'English' ? enUsed : zhUsed
      const left = locale === 'English' ? enLeft : zhLeft
      const expected = template
        .replaceAll('{{turns_used}}', String(turnsUsed))
        .replaceAll('{{turns_left}}', String(turnsLeft))
        .replaceAll('{{time_used}}', used)
        .replaceAll('{{time_left}}', left)
      assert.equal(calibration.renderBudget(locale, turnsUsed, turnsLeft, elapsedMs), expected)
    }
  })

  test(`WHAT[guidance-delivery-012] ${locale} token economy uses the complete authored calibration with the current numbers`, () => {
    const template = readResource('pair-programming-token-economy', resource)
    for (const [cached, uncached] of [[0, 0], [900, 120], [17, 4096]]) {
      assert.equal(
        calibration.renderTokenEconomy(locale, cached, uncached),
        template.replaceAll('{{cached}}', String(cached)).replaceAll('{{uncached}}', String(uncached)),
      )
    }
  })

  test(`WHAT[guidance-delivery-012] ${locale} context limit uses the complete authored calibration with the declared value`, () => {
    const template = readResource('pair-programming-context-limit', resource)
    for (const limit of [128000, 200000]) {
      assert.equal(calibration.renderContextLimit(locale, limit), template.replaceAll('{{limit}}', String(limit)))
    }
  })
}

integrationTest('WHAT[guidance-delivery-012] the marker discloses the provider billed token economy and the declared context limit', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'guidance-economy-012'
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'user-economy-root')

    const userRoot = {
      info: { id: 'user-economy-root', role: 'user', sessionID, agent: 'plan', time: { created: 1 } },
      parts: [{ id: 'p-economy-1', type: 'text', text: 'Deliver the caching layer.' }],
    }
    runtime.pushHostMessage(sessionID, userRoot)
    await hooks['chat.message']({ sessionID, agent: 'plan' }, { message: userRoot.info, parts: userRoot.parts })

    // The last row carries guidance, so this round has a placement to inject into.
    const tail = {
      info: {
        id: 'asst-economy-1',
        sessionID,
        parentID: 'user-economy-root',
        role: 'assistant',
        agent: 'plan',
        providerID: 'provider',
        modelID: 'plan-model',
        time: { created: 2 },
      },
      parts: [
        {
          id: 'p-economy-tool',
          type: 'tool',
          tool: 'bash',
          callID: 'call-economy-1',
          state: { status: 'completed', input: {}, output: 'ok', time: { start: 0, end: 0 } },
        },
      ],
    }
    runtime.pushHostMessage(sessionID, tail)

    // The provider's own billing of the previous assistant turn, as the host reports it.
    await hooks.event({
      event: {
        type: 'message.updated',
        properties: {
          info: {
            id: 'asst-economy-1',
            sessionID,
            role: 'assistant',
            tokens: { input: 120, output: 5, reasoning: 0, cache: { read: 900, write: 10 } },
          },
        },
      },
    })

    // The provider's declared context limit, resolved by the host for this model.
    await hooks['chat.params'](
      { sessionID, agent: 'plan', model: { limit: { context: 200000 } }, message: userRoot.info },
      {},
    )

    const out = { messages: [structuredClone(userRoot), structuredClone(tail)] }
    await hooks['experimental.chat.messages.transform']({ sessionID }, out)

    const marker = out.messages
      .map((message) => JSON.stringify(message))
      .filter((text) => /token economy|token 经济|context limit/i.test(text))
      .join('\n')

    assert.notEqual(marker, '', 'the marker carries the two state fragments')

    assert.match(marker, /900/, 'the last round cached token count is disclosed')
    assert.match(marker, /120/, 'the last round recomputed token count is disclosed')
    assert.match(marker, /200000/, 'the declared context limit is disclosed')
  })
})
