const files = require('node:fs')
const path = require('node:path')
const { syncBuiltinESMExports } = require('node:module')

if (process.argv[2] === '--wanxiangshu-pty-owner') {
  const append = files.appendFileSync
  const rename = files.renameSync
  const open = files.openSync
  const close = files.closeSync
  const synchronize = files.fsyncSync
  const remove = files.rmSync
  const read = files.readFileSync
  const directory = process.argv[3]
  const trace = process.env.WXS_PUBLICATION_TRACE
  const mode = process.env.WXS_PUBLICATION_MODE
  const descriptors = new Map()
  const record = (kind, fields) => append(trace, `${JSON.stringify({ at: Date.now(), pid: process.pid, kind, ...fields })}\n`)
  let terminalAttempts = 0
  let terminalRenamed = false
  let directoryFailed = false
  let terminalReadFailed = false
  const directorySync = mode.startsWith('directory-sync')
  files.openSync = function (name) {
    const descriptor = open.apply(this, arguments)
    descriptors.set(descriptor, name)
    return descriptor
  }
  files.closeSync = function (descriptor) {
    descriptors.delete(descriptor)
    return close.apply(this, arguments)
  }
  files.renameSync = function (source, destination) {
    const name = path.basename(destination)
    if (name === 'terminal') {
      terminalAttempts++
      record('terminal-publication', { attempt: terminalAttempts })
      if (mode === 'owner-lost' || (!directorySync && terminalAttempts <= 2)) {
        throw Object.assign(new Error('PTY terminal rename EIO'), { code: 'EIO' })
      }
    }
    if (name === 'fault' && mode === 'double-failure') {
      record('fault-publication-failed', {})
      throw Object.assign(new Error('PTY fault rename ENOSPC'), { code: 'ENOSPC' })
    }
    const result = rename.apply(this, arguments)
    if (name === 'terminal') terminalRenamed = true
    return result
  }
  files.fsyncSync = function (descriptor) {
    if (directorySync && terminalRenamed && !directoryFailed && descriptors.get(descriptor) === directory) {
      directoryFailed = true
      record('directory-fsync-failed', {})
      throw Object.assign(new Error('PTY directory fsync EIO'), { code: 'EIO' })
    }
    return synchronize.apply(this, arguments)
  }
  files.readFileSync = function (name) {
    if (mode === 'directory-sync-read' && directoryFailed && !terminalReadFailed && name === path.join(directory, 'terminal')) {
      terminalReadFailed = true
      record('terminal-read-failed', {})
      throw Object.assign(new Error('PTY terminal read EIO'), { code: 'EIO' })
    }
    return read.apply(this, arguments)
  }
  files.rmSync = function (name) {
    if (path.basename(String(name)).startsWith('wanxiangshu-pty-')) {
      const state = {}
      for (const file of ['started', 'target-exit', 'scope-ended', 'fault']) {
        if (files.existsSync(path.join(name, file))) state[file] = JSON.parse(files.readFileSync(path.join(name, file), 'utf8'))
      }
      record('inner-before-remove', { state })
    }
    return remove.apply(this, arguments)
  }
  syncBuiltinESMExports()
  record('preload-active', {})
}
