import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { createHash } from 'node:crypto'

const root = '/private/tmp/vibe-fs-physical-soak-20261021-b'
const before = '/private/tmp/vibe-fs-physical-soak-20261021-a/consumer/node_modules/wanxiangli'
const installed = root + '/consumer/node_modules/wanxiangli'
const tarball = '/private/tmp/vibe-fs-routing-public-pack/wanxiangli-0.11.0.tgz'
const hash = text => createHash('sha256').update(text).digest('hex')
const sha = file => hash(fs.readFileSync(file))
assert.equal(sha(tarball), '687cf81f702a2f229f7924442b9c454d9a96576e61a3ccd819323e0e716d3b93')
assert.equal(fs.lstatSync(installed).isSymbolicLink(), false)
assert.equal(JSON.parse(fs.readFileSync(installed + '/package.json')).version, '0.11.0')
const modules = ['process', 'supervision', 'time', 'time-node']
const normalize = text => text.replace(/^(\/\/ \.build\/[a-z-]+)-\d+(\/.*)$/gm, '$1-PID$2')
const artifacts = []
for (const module of modules) {
  const walk = relative => {
    const file = path.join(installed, relative)
    if (fs.statSync(file).isDirectory()) {
      for (const name of fs.readdirSync(file).sort()) walk(relative + '/' + name)
    } else {
      const old = fs.readFileSync(path.join(before, relative), 'utf8')
      const current = fs.readFileSync(file, 'utf8')
      assert.equal(normalize(current), normalize(old), relative + ' changed beyond generated .build PID comments')
      artifacts.push({ path: relative, rawSha256: sha(file), previousRawSha256: sha(path.join(before, relative)), normalizedSha256: hash(normalize(current)), bytes: fs.statSync(file).size })
    }
  }
  walk('dist/' + module)
}
const sources = ['coordinator.mjs', 'lane.mjs', 'worker.mjs']
const harness = sources.map(name => ({ path: root + '/consumer/' + name, sha256: sha(root + '/consumer/' + name) }))
fs.writeFileSync(root + '/harness-sha256.txt', harness.map(item => item.sha256 + '  consumer/' + path.basename(item.path)).join('\n') + '\n')
const receipt = { at: new Date().toISOString(), sourceCommit: '45849f51778f6a12dcafbc12ce1c8c7f7f65502b', previousSourceCommit: '3db7b439283779f6037244d57cf05e6ec3e7ab31', sourceContinuity: { command: 'git diff --exit-code 3db7b439283779f6037244d57cf05e6ec3e7ab31 -- src/process src/supervision src/time src/time-node src/native-process', exitCode: 0, output: 'source-continuity.diff', bytes: fs.statSync(root + '/source-continuity.diff').size }, package: tarball, packageSha256: sha(tarball), packageVersion: '0.11.0', installedPackage: installed, symlink: false, fallback: false, installCommand: 'npm install --ignore-scripts --offline --cache ' + root + '/npm-cache', installExitCode: 0, normalizedArtifactContinuity: 'Only exact esbuild .build/<module>-<decimal PID>/ comment prefixes normalized; all checked runtime artifacts equal. Not a raw-byte equality claim.', artifacts, harness, node: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node v22.13.0', bun: '/opt/homebrew/bin/bun 1.3.14' }
fs.writeFileSync(root + '/preflight-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify({ packageSha256: receipt.packageSha256, sourceDiffBytes: receipt.sourceContinuity.bytes, normalizedArtifacts: artifacts.length, harness }))
