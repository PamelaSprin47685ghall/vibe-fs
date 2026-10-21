import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createHash } from 'node:crypto'

const root = '/private/tmp/vibe-fs-pty-detach-diagnostic-20261021'
const sha = path => createHash('sha256').update(fs.readFileSync(path)).digest('hex')
const artifact = JSON.parse(fs.readFileSync(root + '/copied-artifact-receipt.json'))
for (const file of artifact.copied) assert.equal(sha(root + '/consumer/' + file.path), file.sha256)
assert.equal(sha(root + '/gen106-build-manifest.json'), '331e32dd1401edcedccef5f93dc5d2a03676b96267b6e06c1e2ccf1705df4d17')
const sources = ['controller.mjs', 'pty-soak.mjs', 'target.mjs'].map(name => ({ path: root + '/consumer/' + name, sha256: sha(root + '/consumer/' + name) }))
fs.writeFileSync(root + '/harness-sha256.txt', sources.map(record => record.sha256 + '  ' + record.path).join('\n') + '\n')
const receipt = { kind: 'One authorized finite detach-precondition diagnostic; not a new two-hour run or matrix', at: new Date().toISOString(), fixedGeneration: 106, consumerFilesRawEqualToOriginal: artifact.copied.length, sourceReceipt: root + '/copied-artifact-receipt.json', productionBytesModified: false, originalFailureUnchanged: '/private/tmp/vibe-fs-pty-soak-gen106-20261021-b', minimumActiveMs: 1000, maximumEnvelopeMs: 90000, controllerCancelAtMs: 60000, controllerForceAtMs: 80000, addedPrecondition: 'After creator.dispose and the original 100ms delay, require the original invalid-secret attach to observe credentials mismatch within 3 seconds before SIGSTOP. Record first and subsequent refusal. If not observed, fail and clean all owned resources; no retry.', keptAssertions: ['real EOF and target exit23', 'descendant retained scope', '5-second startup Unknown with terminal absent', 'independent 12-second SIGCONT fallback', 'CONT and stale recovery detach', 'final TERM/KILL drain', 'HMAC/exact exit/owned native cleanup', 'live-controller refusal'], completeHarnessSources: sources, command: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node ' + root + '/consumer/controller.mjs > ' + root + '/runner.log 2>&1' }
fs.writeFileSync(root + '/launch.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify(receipt, null, 2))
