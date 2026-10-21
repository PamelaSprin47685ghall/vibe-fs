import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createHash } from 'node:crypto'

const root = '/private/tmp/vibe-fs-pty-soak-gen106-20261021-b'
const original = '/private/tmp/vibe-fs-pty-soak-gen106-20261021'
const sha = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex')
const copied = JSON.parse(fs.readFileSync(root + '/copied-artifact-receipt.json'))
for (const record of copied.copied) {
  const target = root + '/consumer/' + record.path
  assert.equal(fs.lstatSync(target).isSymbolicLink(), false)
  assert.equal(sha(target), record.sha256)
}
assert.equal(sha(root + '/gen106-build-manifest.json'), copied.manifestSha256)
assert.equal(fs.statSync(root + '/physical').isDirectory(), true)
assert.equal(fs.statSync(root + '/tmp').isDirectory(), true)
const sources = ['controller.mjs', 'pty-soak.mjs', 'target.mjs']
const harness = sources.map(name => ({ path: root + '/consumer/' + name, sha256: sha(root + '/consumer/' + name) }))
fs.writeFileSync(root + '/harness-sha256.txt', harness.map(record => record.sha256 + '  consumer/' + record.path.split('/').at(-1)).join('\n') + '\n')
const receipt = { at: new Date().toISOString(), root, generation: 106, manifestSha256: copied.manifestSha256, originalCopiedArtifactReceipt: original + '/copied-artifact-receipt.json', originalReceiptSha256: sha(original + '/copied-artifact-receipt.json'), copiedFrom: original + '/consumer', byteEqualFiles: copied.copied.length, sourceFreshness: copied.sourceFreshness, symlinks: false, fallback: false, harness, firstSetupAttempt: { directory: original, outcome: 'Failed before creating owner: missing physical cwd; requests=0, ownedPids=[]; all original logs retained.', originalStatusSha256: sha(original + '/status.json'), originalControllerExitSha256: sha(original + '/controller-exit.json') }, node: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node v22.13.0', bun: '/opt/homebrew/bin/bun 1.3.14', packageVersion: JSON.parse(fs.readFileSync(root + '/consumer/node_modules/wanxiangli/package.json')).version, nativePtyVersion: JSON.parse(fs.readFileSync(root + '/consumer/node_modules/bun-pty/package.json')).version }
fs.writeFileSync(root + '/preflight-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify(receipt, null, 2))
