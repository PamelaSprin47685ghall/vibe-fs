import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { gzipSync } from 'node:zlib'
import { Header } from 'tar'
import { fixturePhase } from './fixture-phase.mjs'

const parentName = 'wxs-fixture-parent'
const leafName = 'wxs-fixture-leaf'
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')

function selectedNpm() {
  const candidates = process.env.WXS_VERIFICATION_NPM_CLI
    ? [process.env.WXS_VERIFICATION_NPM_CLI]
    : [path.join(path.dirname(process.execPath), 'npm'), ...String(process.env.PATH ?? '').split(path.delimiter).map(directory => path.join(directory, 'npm'))]
  for (const candidate of candidates) {
    let npmCli
    try {
      npmCli = fs.realpathSync(candidate)
    } catch (error) {
      if (error.code === 'ENOENT') continue
      throw error
    }
    if (path.basename(npmCli) !== 'npm-cli.js') continue
    const manifest = JSON.parse(fs.readFileSync(path.resolve(npmCli, '../../package.json'), 'utf8'))
    assert.equal(manifest.name, 'npm')
    fixturePhase('npm:version-enter', { npmCli })
    const version = execFileSync(process.execPath, [npmCli, '--version'], { encoding: 'utf8' }).trim()
    fixturePhase('npm:version-completed', { npmCli, version })
    assert.equal(version, manifest.version)
    return { npmCli, expectedNpmVersion: version, npmCliSha256: sha256(fs.readFileSync(npmCli)) }
  }
  throw new Error('The real npm installation fixture requires an explicit available npm CLI')
}

function packageArchive(manifest, source) {
  const blocks = []
  for (const [name, content] of Object.entries({ 'package.json': JSON.stringify(manifest), 'index.js': source })) {
    const bytes = Buffer.from(content)
    const header = new Header({ path: `package/${name}`, type: 'File', mode: 0o644, size: bytes.length })
    header.encode()
    blocks.push(header.block, bytes, Buffer.alloc((512 - bytes.length % 512) % 512))
  }
  return gzipSync(Buffer.concat([...blocks, Buffer.alloc(1024)]))
}

export async function createNpmInstallFixture() {
  const tools = selectedNpm()
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-npm-fixture-'))
  const sourceRoot = path.join(root, 'source')
  const parentDirectory = path.join(root, 'candidates')
  fs.mkdirSync(sourceRoot)
  fs.mkdirSync(parentDirectory)
  const manifests = {
    [parentName]: { name: parentName, version: '1.0.0', type: 'module', exports: './index.js', dependencies: { [leafName]: '1.0.0' }, scripts: { postinstall: 'node -e "process.exit(72)"' } },
    [leafName]: { name: leafName, version: '1.0.0', type: 'module', exports: './index.js', scripts: { postinstall: 'node -e "process.exit(73)"' } },
  }
  const archives = {
    [parentName]: packageArchive(manifests[parentName], `import value from '${leafName}'; export default value + 1\n`),
    [leafName]: packageArchive(manifests[leafName], 'export default 41\n'),
  }
  const requests = []
  const sockets = new Set()
  const leafRequest = Promise.withResolvers()
  const leafClosed = Promise.withResolvers()
  let holdLeaf = false
  let heldLeafResponse
  let corruptLeaf = false
  let registry
  const server = http.createServer((request, response) => {
    const requestPath = new URL(request.url, registry).pathname
    requests.push({ method: request.method, path: requestPath })
    fixturePhase('npm:registry-request', { root, method: request.method, path: requestPath })
    const name = [parentName, leafName].find(value => requestPath === `/${value}` || requestPath === `/${value}/-/${value}-1.0.0.tgz`)
    if (!name || request.method !== 'GET') {
      response.writeHead(404).end('Unexpected fixture registry request')
      return
    }
    if (requestPath.endsWith('.tgz')) {
      if (name === leafName && holdLeaf) {
        heldLeafResponse = response
        response.once('close', () => leafClosed.resolve())
        leafRequest.resolve()
        return
      }
      const bytes = name === leafName && corruptLeaf ? archives[parentName] : archives[name]
      response.writeHead(200, { 'content-type': 'application/octet-stream', 'content-length': bytes.length }).end(bytes)
      return
    }
    response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({
      name,
      'dist-tags': { latest: '1.0.0' },
      versions: { '1.0.0': { ...manifests[name], dist: { tarball: `${registry}/${name}/-/${name}-1.0.0.tgz`, integrity: `sha512-${createHash('sha512').update(archives[name]).digest('base64')}` } } },
    }))
  })
  server.on('connection', socket => {
    sockets.add(socket)
    socket.once('close', () => sockets.delete(socket))
  })
  try {
    await new Promise((resolve, reject) => {
      server.once('error', reject)
      server.listen(0, '127.0.0.1', resolve)
    })
    registry = `http://127.0.0.1:${server.address().port}`
    fixturePhase('npm:registry-listening', { root, registry })
    const packageJson = { name: 'verification-install-fixture', version: '1.0.0', private: true, packageManager: `npm@${tools.expectedNpmVersion}`, dependencies: { [parentName]: '1.0.0' }, scripts: { postinstall: 'node -e "process.exit(71)"' } }
    const lockfile = {
      name: packageJson.name,
      version: packageJson.version,
      lockfileVersion: 3,
      requires: true,
      packages: {
        '': { name: packageJson.name, version: packageJson.version, dependencies: packageJson.dependencies },
        ...Object.fromEntries(Object.entries(manifests).map(([name, manifest]) => [`node_modules/${name}`, {
          version: manifest.version,
          resolved: `${registry}/${name}/-/${name}-1.0.0.tgz`,
          integrity: `sha512-${createHash('sha512').update(archives[name]).digest('base64')}`,
          hasInstallScript: true,
          ...(manifest.dependencies ? { dependencies: manifest.dependencies } : {}),
        }])),
      },
    }
    fs.writeFileSync(path.join(sourceRoot, 'package.json'), JSON.stringify(packageJson))
    fs.writeFileSync(path.join(sourceRoot, 'package-lock.json'), JSON.stringify(lockfile))
    return {
      root, sourceRoot, parentDirectory, registry, requests, parentName, leafName,
      options: { sourceRoot, parentDirectory, nodeExecutable: process.execPath, nodeSha256: sha256(fs.readFileSync(process.execPath)), registry, ...tools },
      corruptLeaf() { corruptLeaf = true },
      holdLeaf() { holdLeaf = true },
      releaseLeaf() {
        assert.ok(heldLeafResponse && !heldLeafResponse.destroyed, 'A real leaf tarball request must be held before release')
        holdLeaf = false
        const bytes = corruptLeaf ? archives[parentName] : archives[leafName]
        heldLeafResponse.writeHead(200, { 'content-type': 'application/octet-stream', 'content-length': bytes.length }).end(bytes)
        heldLeafResponse = undefined
      },
      leafRequest: leafRequest.promise,
      leafClosed: leafClosed.promise,
      updateLock(change) {
        const lock = JSON.parse(fs.readFileSync(path.join(sourceRoot, 'package-lock.json'), 'utf8'))
        change(lock)
        fs.writeFileSync(path.join(sourceRoot, 'package-lock.json'), JSON.stringify(lock))
      },
      async dispose() {
        fixturePhase('npm:fixture-dispose-enter', { root })
        for (const socket of sockets) socket.destroy()
        await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()))
        fs.rmSync(root, { recursive: true, force: true })
        fixturePhase('npm:fixture-disposed', { root })
      },
    }
  } catch (error) {
    for (const socket of sockets) socket.destroy()
    if (server.listening) await new Promise(resolve => server.close(resolve))
    fs.rmSync(root, { recursive: true, force: true })
    throw error
  }
}
