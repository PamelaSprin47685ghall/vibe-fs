import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

test('WHAT[DURABLE-EVENTS-025] persistence cut stores have no optional fatal hook and composition is sole fatal owner after cut settles', async () => {
  // 1. Static boundary check: StrengthDurability and CasebookStore must not retain module-global fatalTripHandler
  const durabilitySource = readFileSync('src/Wanxiangshu/Strength/Persistence/Durability.fs', 'utf8')
  const casebookSource = readFileSync('src/Wanxiangshu/Repository/Knowledge/Casebook/Store.fs', 'utf8')

  assert.doesNotMatch(durabilitySource, /fatalTripHandler/, 'StrengthDurability must not contain fatalTripHandler')
  assert.doesNotMatch(durabilitySource, /FatalProcess/, 'StrengthDurability must not directly reference FatalProcess')
  assert.doesNotMatch(casebookSource, /fatalTripHandler/, 'CasebookStore must not contain fatalTripHandler')
  assert.doesNotMatch(casebookSource, /FatalProcess/, 'CasebookStore must not directly reference FatalProcess')

  // 2. Dynamic behavior: store returns typed rejection on semantic cut without raising process fatal
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const H = (text) => `H(${text})`
    const frame = Strength.frameTryBuild(H, 10000, [{
      requestOrdinal: 1,
      exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }]
    }]).value

    const firstReq = {
      ownerSessionId: 'owner-de025',
      decisionId: 'd-de025',
      targetProviderRun: 'run-1',
      replicaSessionId: 'rep-1',
      budget: 'K1',
      anchorDigest: 'anchor-a',
      bundle: frame,
    }

    const firstPublished = await Strength.durabilityPublishPrepared(durability, firstReq)
    assert.equal(firstPublished.kind, 'Published')

    // Conflicting request with different replica/parameters for the same decisionId
    const conflictFrame = Strength.frameTryBuild(H, 10000, [{
      requestOrdinal: 1,
      exchanges: [{ toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }]
    }]).value
    const conflictReq = {
      ownerSessionId: 'owner-de025',
      decisionId: 'd-de025',
      targetProviderRun: 'run-1',
      replicaSessionId: 'rep-conflict',
      budget: 'K1',
      anchorDigest: 'anchor-b',
      bundle: conflictFrame,
    }

    // Semantic cut branch returns typed result (StorageInvalid / Rejected), not an unhandled process termination
    const conflictOutcome = await Strength.durabilityPublishPrepared(durability, conflictReq)
    assert.equal(conflictOutcome.kind, 'StorageInvalid', 'conflicting prepared identity must return typed rejection')

    // 3. Composition layer ownership: sole fatal owner resides in composition
    const portsSource = readFileSync('src/Wanxiangshu/OpenCode/Plugin/PluginStrengthPorts.fs', 'utf8')
    assert.match(portsSource, /Diagnostic\.fatal "strength-semantic-cut"/, 'composition layer owns semantic cut fatal decision')

    const txStoreSource = readFileSync('src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs', 'utf8')
    assert.match(txStoreSource, /FatalProcess\.trip "js-transaction-semantic-cut"/, 'JsToolsTransactionStore trips only after durable cut settlement')
  } finally {
    local.close()
  }
})
