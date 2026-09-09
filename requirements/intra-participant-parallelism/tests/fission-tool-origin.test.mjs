import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  acceptAuthorityRoot,
  bindManagedChild,
  grantWorkOwned,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

import * as sessionBinding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'

const withRoutingHome = async (body) => {
  const previousHome = process.env.HOME
  const root = mkdtempSync(join(tmpdir(), 'wxs-fission-origin-home-'))
  const home = join(root, 'home')
  const configDir = join(home, '.config', 'opencode')
  mkdirSync(configDir, { recursive: true })
  writeFileSync(
    join(configDir, 'wanxiangshu.mjs'),
    "export default function route() { return { model: 'fixture/root-model', reasoning: 'none' } }\n",
    'utf8',
  )
  process.env.HOME = home

  try {
    await body()
  } finally {
    process.env.HOME = previousHome
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-013] real root chat message carries a request-local fission deny', async () => {
  await withRoutingHome(async () => {
    await withExecutablePlugin(async (hooks) => {
      const sessionID = 'fission-root-provider-surface'
      const output = {
        message: {
          id: 'msg-fission-root-provider-surface',
          role: 'user',
          sessionID,
          agent: 'manager',
          model: { providerID: 'host', modelID: 'placeholder' },
          tools: { fork: true, join: true, horizon: true, suicide: true },
        },
        parts: [{ type: 'text', text: 'root work' }],
      }

      await hooks['chat.message']({ sessionID, agent: 'manager' }, output)

      assert.equal(output.message.tools?.fission, false)
      assert.equal(output.message.tools?.fork, true)
      assert.equal(output.message.tools?.join, true)
      assert.equal(output.message.tools?.horizon, true)
      assert.equal(output.message.tools?.suicide, true)
    })
  })
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-013] a bound child retains fission when the physical parent cache is empty', async () => {
  await withExecutablePlugin(async (hooks) => {
    const sessionID = 'fission-bound-child-provider-surface'
    bindManagedChild('fission-binding-parent', sessionID, 'manager')
    const output = {
      message: {
        id: 'msg-fission-bound-child-provider-surface',
        role: 'user',
        sessionID,
        agent: 'manager',
        model: { providerID: 'host', modelID: 'placeholder' },
        tools: { fork: true, fission: true },
      },
      parts: [{ type: 'text', text: 'Interrupt the active join.' }],
    }

    try {
      await hooks['chat.message']({ sessionID, agent: 'manager' }, output)
      assert.equal(output.message.tools.fission, true)
      assert.equal(output.message.tools.fork, true)
    } finally {
      sessionBinding.drop(sessionID)
    }
  })
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-013] forced root fission rejects origin before parsing prompts', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'fission-root-origin'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await grantWorkOwned(runtime, sessionID)

    const result = await hooks.tool.fission.execute(
      { prompts: 'only one lane' },
      {
        sessionID,
        agent: 'manager',
        callID: 'call-root-fission',
        messageID: 'run-root-fission',
      },
    )

    assert.match(result, /user-facing\/root/i)
    assert.doesNotMatch(result, /at least two|至少需要两条/i)
  })
})
