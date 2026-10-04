import { execFile } from 'node:child_process'
import { mkdtempSync, mkdirSync, realpathSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const HDIUTIL = '/usr/bin/hdiutil'
const PLUTIL = '/usr/bin/plutil'

function run(file, args, diagnostic, signal) {
  return new Promise((resolve, reject) => {
    const child = execFile(file, args, { encoding: 'utf8', maxBuffer: 2 * 1024 * 1024, timeout: 30000, signal }, (error, stdout, stderr) => {
      diagnostic(JSON.stringify({ file, args, stdout, stderr, error: error?.message ?? null }))
      if (!error) {
        resolve(stdout)
        return
      }
      if (file === HDIUTIL && args[0] === 'attach' && child.pid &&
          (error.code === 'ABORT_ERR' || (error.killed && error.signal === 'SIGTERM' && error.code == null))) {
        const uncertain = new Error(`Interrupted attach has an unproven daemon outcome: ${args[1]}`, { cause: error })
        uncertain.code = 'MAC_ATTACH_OUTCOME_UNCERTAIN'
        reject(uncertain)
      } else reject(error)
    })
  })
}

function readPlist(xml, diagnostic) {
  return new Promise((resolve, reject) => {
    const child = execFile(PLUTIL, ['-convert', 'json', '-o', '-', '-'], { encoding: 'utf8', timeout: 30000 }, (error, stdout, stderr) => {
      if (error) {
        diagnostic(JSON.stringify({ file: PLUTIL, error: error.message, stderr }))
        reject(error)
        return
      }
      try { resolve(JSON.parse(stdout)) } catch (parseError) { reject(parseError) }
    })
    child.stdin.on('error', reject)
    child.stdin.end(xml)
  })
}

function deviceAtMount(entities, mountPoint) {
  const matches = entities.filter((entity) => entity['mount-point'] === mountPoint)
  if (matches.length !== 1 || !/^\/dev\/disk\d+(s\d+)?$/.test(matches[0]['dev-entry'] ?? '')) {
    throw new Error(`No unique owned device at ${mountPoint}`)
  }
  return matches[0]['dev-entry']
}

// This fixture owns only its two images; it never detaches unrelated mounts.
export async function withMacMountedOutput(action, { diagnostic = () => {}, signal } = {}) {
  if (process.platform !== 'darwin') throw new Error('Mac mounted-output proof requires darwin')
  const root = realpathSync(mkdtempSync(join(tmpdir(), 'wanxiangshu-mounted-output-')))
  const stage = join(root, 'stage')
  const inputRoot = join(root, 'input')
  const outputRoot = join(inputRoot, 'dist')
  const inputImage = join(root, 'input.dmg')
  const outputImage = join(root, 'output.dmg')
  const images = [
    { path: inputImage, mountPoint: inputRoot, device: null },
    { path: outputImage, mountPoint: outputRoot, device: null },
  ]
  let operationFailure
  try {
    mkdirSync(join(stage, 'dist'), { recursive: true })
    mkdirSync(inputRoot)
    writeFileSync(join(stage, 'source.txt'), 'immutable verification input\n')
    await run(HDIUTIL, ['create', '-srcfolder', stage, '-format', 'UDRO', '-fs', 'Case-sensitive HFS+', '-volname', 'VerificationInput', inputImage], diagnostic, signal)
    const inputReceipt = await readPlist(await run(HDIUTIL, ['attach', inputImage, '-readonly', '-nobrowse', '-noautoopen', '-mountpoint', inputRoot, '-plist'], diagnostic, signal), diagnostic)
    images[0].device = deviceAtMount(inputReceipt['system-entities'] ?? [], inputRoot)
    await run(HDIUTIL, ['create', '-size', '32m', '-type', 'UDIF', '-fs', 'Case-sensitive HFS+', '-volname', 'VerificationOutput', outputImage], diagnostic, signal)
    const outputReceipt = await readPlist(await run(HDIUTIL, ['attach', outputImage, '-nobrowse', '-noautoopen', '-mountpoint', outputRoot, '-plist'], diagnostic, signal), diagnostic)
    images[1].device = deviceAtMount(outputReceipt['system-entities'] ?? [], outputRoot)
    await action({ inputRoot, outputRoot })
  } catch (error) {
    operationFailure = { error }
    diagnostic(JSON.stringify({ operationError: error?.message ?? String(error), code: error?.code ?? null, stack: error?.stack }))
  }

  const cleanupErrors = []
  for (const image of images.toReversed()) {
    try {
      const info = await readPlist(await run(HDIUTIL, ['info', '-plist'], diagnostic), diagnostic)
      const attached = (info.images ?? []).filter((entry) => entry['image-path'] === image.path)
      if (attached.length > 1) throw new Error(`Ambiguous owned image ${image.path}`)
      if (attached.length === 1) {
        const device = deviceAtMount(attached[0]['system-entities'] ?? [], image.mountPoint)
        if (image.device && device !== image.device) throw new Error(`Owned device changed for ${image.path}`)
        await run(HDIUTIL, ['detach', device], diagnostic)
      } else if (image.device) {
        throw new Error(`Owned mount disappeared before cleanup: ${image.path}`)
      }
    } catch (error) { cleanupErrors.push(error) }
  }
  try {
    const finalInfo = await readPlist(await run(HDIUTIL, ['info', '-plist'], diagnostic), diagnostic)
    if ((finalInfo.images ?? []).some((entry) => images.some((image) => image.path === entry['image-path']))) {
      throw new Error('Owned image remains attached after cleanup')
    }
  } catch (error) { cleanupErrors.push(error) }
  if (cleanupErrors.length === 0 && operationFailure?.error?.code !== 'MAC_ATTACH_OUTCOME_UNCERTAIN') {
    try { rmSync(root, { recursive: true, force: true }) } catch (error) { cleanupErrors.push(error) }
  }
  if (cleanupErrors.length || operationFailure?.error?.code === 'MAC_ATTACH_OUTCOME_UNCERTAIN') {
    diagnostic(`Cleanup incomplete; retained owned artifacts at ${root}`)
  }
  if (cleanupErrors.length) {
    throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupErrors] : cleanupErrors, 'Mac mounted-output cleanup failed', { cause: operationFailure?.error })
  }
  if (operationFailure) throw operationFailure.error
}
