import { execFile } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { allocateVerificationDirectory } from './verification-directory-owner.mjs'

const views = new WeakMap()
const invalid = message => Object.assign(new Error(message), { code: 'verification-readonly-inputs-invalid' })
const identity = directory => {
  const stat = fs.lstatSync(directory, { bigint: true })
  if (!stat.isDirectory()) throw invalid(`Readonly input requires an ordinary directory: ${directory}`)
  return { dev: stat.dev, ino: stat.ino }
}
const sameIdentity = (a, b) => a.dev === b.dev && a.ino === b.ino
const selection = owner => JSON.stringify(Object.fromEntries(Object.entries(owner).filter(([, value]) => typeof value !== 'function')))

function inventory(root) {
  const entries = []
  const visit = relative => {
    const file = path.join(root, relative)
    const stat = fs.lstatSync(file)
    const entry = { path: relative, mode: stat.mode & 0o777 }
    if (stat.isDirectory()) {
      entries.push({ ...entry, type: 'Directory' })
      for (const name of fs.readdirSync(file).sort()) visit(relative === '.' ? name : `${relative}/${name}`)
    } else if (stat.isFile()) {
      entries.push({ ...entry, type: 'File', size: stat.size, sha256: createHash('sha256').update(fs.readFileSync(file)).digest('hex') })
    } else if (stat.isSymbolicLink()) {
      const resolved = fs.realpathSync(file)
      if (!resolved.startsWith(root + path.sep)) throw invalid(`Readonly input link leaves its selected root: ${file}`)
      entries.push({ ...entry, type: 'SymbolicLink', target: fs.readlinkSync(file) })
    } else throw invalid(`Readonly input contains an unsupported member: ${file}`)
  }
  visit('.')
  return JSON.stringify(entries)
}

function run(file, args, signal, input) {
  signal?.throwIfAborted()
  return new Promise((resolve, reject) => {
    const child = execFile(file, args, { encoding: 'utf8', timeout: 30000, maxBuffer: 2097152, signal }, (error, stdout, stderr) => {
      if (signal?.aborted) reject(signal.reason)
      else if (error) reject(Object.assign(error, { stdout, stderr }))
      else resolve(stdout)
    })
    child.stdin.on('error', reject)
    child.stdin.end(input)
  })
}
const plist = async xml => JSON.parse(await run('/usr/bin/plutil', ['-convert', 'json', '-o', '-', '-'], undefined, xml))
const attachedImages = async () => (await plist(await run('/usr/bin/hdiutil', ['info', '-plist']))).images ?? []

function exactMount(images, image, mountPoint, device) {
  const matches = images.filter(entry => entry['image-path'] === image)
  if (matches.length !== 1) throw invalid('Readonly view requires exactly one owned attached image')
  if (matches[0].writeable !== false || matches[0]['image-type'] !== 'UDIF read-only') throw invalid('Readonly view requires the final UDRO image mounted read-only')
  const entities = matches[0]['system-entities'].filter(entry => entry['mount-point'] === mountPoint)
  if (entities.length !== 1 || !/^\/dev\/disk\d+(s\d+)?$/.test(entities[0]['dev-entry'] ?? '')) throw invalid('Readonly view requires one exact mounted device')
  if (device !== undefined && entities[0]['dev-entry'] !== device) throw invalid('Readonly mounted device changed')
  return entities[0]['dev-entry']
}

export function assertReadonlyVerificationInputs(view, owners, outputParent) {
  const record = views.get(view)
  if (!record?.active || owners.some((owner, index) => owner !== record.owners[index]) || owners.length !== record.owners.length) throw invalid('Readonly capability is not active for these exact prepared owners')
  const output = fs.realpathSync(outputParent)
  if (output === record.mountPoint || output.startsWith(record.mountPoint + path.sep)) throw invalid('Compilation output must remain outside readonly inputs')
  record.revalidate()
}

export function consumeReadonlyVerificationInputs(view, operation) {
  const record = views.get(view)
  if (!record?.active) throw invalid('Readonly view no longer admits consumers')
  record.revalidate()
  const consumer = Promise.resolve().then(() => operation(record.signal))
  record.consumers.push(consumer)
  consumer.catch(() => {})
  return consumer
}

export async function withReadonlyVerificationInputs({ source, sdk, tools, project, parentDirectory, signal }, action) {
  signal?.throwIfAborted()
  if (process.platform !== 'darwin') throw invalid('Readonly input mounting currently requires macOS')
  const owners = [source, sdk, tools, project]
  if (owners.some(owner => typeof owner?.revalidate !== 'function')) throw invalid('Readonly inputs require four prepared owners')
  owners.forEach(owner => owner.revalidate())
  const roots = [source.sourceRoot, sdk.toolRoot, tools.toolRoot, project.projectRoot]
  if (roots.some(root => typeof root !== 'string' || !path.isAbsolute(root) || fs.realpathSync(root) !== root)) throw invalid('Readonly inputs require original absolute root paths')
  const mountPoint = path.dirname(roots[0])
  if (new Set(roots).size !== 4 || roots.some(root => path.dirname(root) !== mountPoint)) throw invalid('Readonly roots must be four distinct siblings under one parent')
  const expectedNames = roots.map(root => path.basename(root)).sort()
  const assertSiblings = () => {
    if (JSON.stringify(fs.readdirSync(mountPoint).sort()) !== JSON.stringify(expectedNames)) throw invalid('Readonly mount parent contains unowned siblings')
  }
  assertSiblings()
  const externalParent = fs.realpathSync(parentDirectory)
  if (externalParent === mountPoint || externalParent.startsWith(mountPoint + path.sep)) throw invalid('Readonly image allocation must remain outside input mount')
  const originalIdentities = [mountPoint, ...roots].map(identity)
  const selections = owners.map(selection)
  const inventories = roots.map(inventory)
  const imageOwner = allocateVerificationDirectory(externalParent, 'verification-readonly-inputs-', invalid)
  const capture = path.join(imageOwner.root, 'capture.dmg')
  const image = path.join(imageOwner.root, 'inputs.dmg')
  const artifacts = new Map()
  const captureArtifact = file => {
    imageOwner.assertOwned()
    const stat = fs.lstatSync(file, { bigint: true })
    if (!stat.isFile()) throw invalid('Readonly image phase requires an ordinary owned output')
    const owned = { dev: stat.dev, ino: stat.ino }
    artifacts.set(file, owned)
    return owned
  }
  const assertArtifacts = () => {
    imageOwner.assertOwned()
    if (JSON.stringify(fs.readdirSync(imageOwner.root).sort()) !== JSON.stringify([...artifacts.keys()].map(file => path.basename(file)).sort())) throw invalid('Readonly image phase contains unconfirmed or foreign artifacts')
    for (const [file, owned] of artifacts) {
      const stat = fs.lstatSync(file, { bigint: true })
      if (!stat.isFile() || !sameIdentity(stat, owned)) throw invalid('Readonly image phase output ownership changed')
    }
  }
  let device
  let imageIdentity
  let mountedIdentity
  let attachAttempted = false
  let attachConfirmed = false
  let view
  let result
  let operationFailure
  const cleanupErrors = []
  try {
    await run('/usr/bin/hdiutil', ['create', '-srcfolder', mountPoint, '-format', 'UDRW', '-fs', 'Case-sensitive HFS+', '-volname', 'VerificationInputs', capture], signal)
    captureArtifact(capture)
    assertArtifacts()
    await run('/usr/bin/hdiutil', ['convert', capture, '-format', 'UDRO', '-o', image], signal)
    imageIdentity = captureArtifact(image)
    assertArtifacts()
    owners.forEach(owner => owner.revalidate())
    assertSiblings()
    signal?.throwIfAborted()
    attachAttempted = true
    const receipt = await plist(await run('/usr/bin/hdiutil', ['attach', image, '-readonly', '-nobrowse', '-noautoopen', '-mountpoint', mountPoint, '-plist'], signal))
    const entities = receipt['system-entities'].filter(entry => entry['mount-point'] === mountPoint)
    if (entities.length !== 1 || !/^\/dev\/disk\d+(s\d+)?$/.test(entities[0]['dev-entry'] ?? '')) throw invalid('Readonly attach did not confirm an exact owned device')
    device = entities[0]['dev-entry']
    exactMount(await attachedImages(), image, mountPoint, device)
    mountedIdentity = identity(mountPoint)
    if (sameIdentity(mountedIdentity, originalIdentities[0])) throw invalid('Readonly attach did not replace the original directory view')
    attachConfirmed = true
    const mountedRootIdentities = roots.map(identity)
    const revalidate = () => {
      imageOwner.assertOwned()
      const actualImage = fs.lstatSync(image, { bigint: true })
      if (!actualImage.isFile() || !sameIdentity(actualImage, imageIdentity)) throw invalid('Readonly backing image ownership changed')
      if (!sameIdentity(identity(mountPoint), mountedIdentity)) throw invalid('Readonly mounted root identity changed')
      owners.forEach((owner, index) => {
        if (!sameIdentity(identity(roots[index]), mountedRootIdentities[index]) || selection(owner) !== selections[index] || inventory(roots[index]) !== inventories[index]) throw invalid('Readonly view differs from its prepared selection or complete members')
      })
    }
    view = Object.freeze({
      revalidate() {
        if (!views.get(view)?.active) throw invalid('Readonly capability is not active')
        revalidate()
      },
    })
    const controller = new AbortController()
    views.set(view, { active: true, owners, mountPoint, revalidate, controller, signal: signal ? AbortSignal.any([signal, controller.signal]) : controller.signal, consumers: [] })
    revalidate()
    signal?.throwIfAborted()
    result = await action(view)
    signal?.throwIfAborted()
    revalidate()
    exactMount(await attachedImages(), image, mountPoint, device)
  } catch (error) { operationFailure = { error } }
  finally {
    if (view) {
      const record = views.get(view)
      record.active = false
      record.controller.abort(operationFailure ? operationFailure.error : invalid('Readonly scope ended before its consumers settled'))
      const settled = await Promise.allSettled(record.consumers)
      if (!operationFailure) {
        const failed = settled.find(entry => entry.status === 'rejected')
        if (failed) operationFailure = { error: failed.reason }
      }
      for (const entry of settled) {
        if (entry.status === 'rejected' && entry.reason !== operationFailure?.error && entry.reason !== record.controller.signal.reason) cleanupErrors.push(entry.reason)
      }
    }
    try {
      if (attachAttempted && !attachConfirmed) throw invalid('Interrupted readonly attach has an unproven daemon outcome; owned artifacts retained')
      const images = await attachedImages()
      if (images.some(entry => entry['image-path'] === capture)) throw invalid('Readonly capture phase still has an unconfirmed attached device; owned artifacts retained')
      const owned = images.filter(entry => entry['image-path'] === image)
      if (owned.length > 1) throw invalid('Readonly cleanup found ambiguous owned images')
      if (owned.length === 1) {
        imageOwner.assertOwned()
        const actualImage = fs.lstatSync(image, { bigint: true })
        if (!actualImage.isFile() || !sameIdentity(actualImage, imageIdentity)) throw invalid('Readonly cleanup cannot verify backing image ownership')
        const actualDevice = exactMount(images, image, mountPoint, device)
        await run('/usr/bin/hdiutil', ['detach', actualDevice])
      } else if (attachConfirmed) throw invalid('Owned readonly mount disappeared before cleanup')
      if ((await attachedImages()).some(entry => entry['image-path'] === image || entry['image-path'] === capture)) throw invalid('Owned readonly image phase remains attached')
      for (const [index, root] of [mountPoint, ...roots].entries()) {
        if (!sameIdentity(identity(root), originalIdentities[index])) throw invalid('Readonly detach did not restore original directory ownership')
      }
      owners.forEach(owner => owner.revalidate())
      assertArtifacts()
      imageOwner.dispose()
    } catch (error) { cleanupErrors.push(error) }
  }
  if (cleanupErrors.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupErrors] : cleanupErrors, 'Readonly inputs and cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupErrors[0] })
  if (operationFailure) throw operationFailure.error
  return result
}
