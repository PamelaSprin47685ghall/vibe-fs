import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[host-provider-failure-ownership-001] actual plugin config hook forces zero despite existing values and legacy environment override', async () => {
  const previous = process.env.WANXIANGSHU_CHAT_MAX_RETRIES
  process.env.WANXIANGSHU_CHAT_MAX_RETRIES = '9'
  try {
    await withExecutablePlugin(async hooks => {
      for (const experimental of [undefined, {}, { chatMaxRetries: 9 }, { chatMaxRetries: -1 }, { chatMaxRetries: 0 }]) {
        const config = { agent: {}, experimental }
        await hooks.config(config)
        assert.equal(config.experimental.chatMaxRetries, 0)
      }
    })
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_CHAT_MAX_RETRIES
    else process.env.WANXIANGSHU_CHAT_MAX_RETRIES = previous
  }
})
