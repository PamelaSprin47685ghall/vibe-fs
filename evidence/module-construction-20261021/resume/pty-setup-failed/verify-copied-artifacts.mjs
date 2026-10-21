import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { createHash } from 'node:crypto'

const source = '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs'
const root = '/private/tmp/vibe-fs-pty-soak-gen106-20261021'
const sha = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex')
const manifest = source + '/.fable-build/build-manifest.json'
const expectedManifest = '331e32dd1401edcedccef5f93dc5d2a03676b96267b6e06c1e2ccf1705df4d17'
assert.equal(sha(manifest), expectedManifest)
assert.equal(sha(root + '/gen106-build-manifest.json'), expectedManifest)
assert.equal(JSON.parse(fs.readFileSync(manifest)).generation, 106)
const copied = []
const visit = (relative, original) => {
  const target = root + '/consumer/' + relative
  const info = fs.lstatSync(target)
  assert.equal(info.isSymbolicLink(), false)
  if (info.isDirectory()) {
    const names = fs.readdirSync(target).sort()
    assert.deepEqual(names, fs.readdirSync(original).sort())
    for (const name of names) visit(relative + '/' + name, path.join(original, name))
  } else {
    const hash = sha(original)
    assert.equal(sha(target), hash)
    copied.push({ path: relative, sha256: hash, bytes: info.size })
  }
}
visit('dist', source + '/dist')
visit('node_modules/bun-pty', source + '/node_modules/bun-pty')
for (const name of ['dist', 'resources', 'package.json']) visit('node_modules/wanxiangli/' + name, source + '/wanxiangli/' + name)
assert.equal(sha(manifest), expectedManifest)
const receipt = { generation: 106, manifestSha256: expectedManifest, currentMainHead: 'aad92fa0b476e5a8777c505a64656fec0d1fd2d0', sourceFreshness: 'Gen106 was built before later tracked pause evidence changed loop-detector corpus. This copied artifact receipt does not assert aad HEAD is build-fresh.', sourceRoot: source, consumer: root + '/consumer', at: new Date().toISOString(), byteEqualFiles: copied.length, symlinks: false, fallback: false, copied }
fs.writeFileSync(root + '/copied-artifact-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify({ generation: receipt.generation, manifestSha256: expectedManifest, byteEqualFiles: copied.length, at: receipt.at }))
