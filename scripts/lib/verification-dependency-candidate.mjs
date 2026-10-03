import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { gunzipSync } from 'node:zlib'
import { Parser } from 'tar'
import { rejectSymbolicVerificationInput } from './verification-input-path.mjs'

const digest = bytes => createHash('sha256').update(bytes).digest('hex')
const invalidEntry = message => Object.assign(new Error(message), { code: 'dependency-candidate-entry-invalid' })

async function readMembers(bytes) {
  const tarBytes = bytes[0] === 0x1f && bytes[1] === 0x8b ? gunzipSync(bytes) : bytes
  const members = new Map()
  let invalid
  let complete = false
  await new Promise((resolve, reject) => {
    const parser = new Parser({ strict: true })
    parser.on('error', reject)
    parser.on('eof', () => { complete = true })
    parser.on('ignoredEntry', entry => { invalid ??= invalidEntry(`Ignored dependency archive entry: ${entry.path}`) })
    parser.on('entry', entry => {
      const memberPath = entry.type === 'Directory' ? entry.path.replace(/\/$/, '') : entry.path
      const segments = memberPath.split('/')
      if (segments[0] !== 'node_modules' || segments.some(segment => !segment || segment === '.' || segment === '..' || segment.toLowerCase() === '.git' || segment.includes('\\')) || members.has(memberPath)) {
        invalid ??= invalidEntry(`Invalid or duplicate dependency path: ${entry.path}`)
      }
      if (!['File', 'Directory', 'SymbolicLink'].includes(entry.type) || !Number.isInteger(entry.mode) || (entry.mode & ~0o777) !== 0 || (entry.type === 'Directory' && (entry.mode & 0o700) !== 0o700)) {
        invalid ??= invalidEntry(`Unsupported dependency entry: ${entry.path}`)
      }
      const member = { path: memberPath, type: entry.type, mode: entry.mode, target: entry.linkpath, chunks: [] }
      members.set(memberPath, member)
      entry.on('error', reject)
      entry.on('data', chunk => member.chunks.push(chunk))
      entry.on('end', () => {
        member.bytes = Buffer.concat(member.chunks)
        delete member.chunks
        if (member.bytes.length !== entry.size || (entry.type !== 'File' && entry.size !== 0)) invalid ??= invalidEntry(`Invalid dependency entry size: ${entry.path}`)
      })
      entry.resume()
    })
    parser.on('end', resolve)
    for (let offset = 0; offset < tarBytes.length; offset += 512) {
      if (complete) {
        if (tarBytes.subarray(offset).some(byte => byte !== 0)) invalid ??= invalidEntry('Dependency archive has non-padding data after termination')
        break
      }
      parser.write(tarBytes.subarray(offset, offset + 512))
    }
    parser.end()
  })
  if (invalid) throw invalid
  if (!complete || tarBytes.length % 512 !== 0) throw invalidEntry('Dependency archive requires complete tar termination blocks')
  if (members.get('node_modules')?.type !== 'Directory' || ![...members.values()].some(member => member.type === 'File')) {
    throw invalidEntry('Dependency archive requires a node_modules directory and file contents')
  }
  for (const member of members.values()) {
    if (member.path !== 'node_modules' && members.get(path.posix.dirname(member.path))?.type !== 'Directory') {
      throw invalidEntry(`Dependency entry requires a real directory parent: ${member.path}`)
    }
    if (member.type === 'SymbolicLink') {
      if (!member.target || member.target.startsWith('/') || member.target.includes('\\')) throw invalidEntry(`Dependency link must be relative: ${member.path}`)
      const segments = path.posix.dirname(member.path).split('/')
      let hasDescended = false
      for (const segment of member.target.split('/')) {
        if (segment === '..') {
          if (hasDescended) throw invalidEntry(`Dependency link has an ambiguous parent traversal: ${member.path}`)
          if (segments.length === 1) throw invalidEntry(`Dependency link escapes node_modules: ${member.path}`)
          segments.pop()
        } else if (segment && segment !== '.') {
          hasDescended = true
          segments.push(segment)
        }
      }
      member.destination = segments.join('/')
    }
  }
  function resolveMember(memberPath, seen = new Set()) {
    const segments = memberPath.split('/')
    for (let index = 1; index <= segments.length; index++) {
      const prefix = segments.slice(0, index).join('/')
      const member = members.get(prefix)
      if (!member) throw invalidEntry(`Missing dependency link target: ${memberPath}`)
      if (member.type === 'SymbolicLink') {
        if (seen.has(prefix)) throw invalidEntry(`Cyclic dependency link: ${prefix}`)
        return resolveMember([member.destination, ...segments.slice(index)].join('/'), new Set([...seen, prefix]))
      }
      if (index < segments.length && member.type !== 'Directory') throw invalidEntry(`Dependency link traverses a file: ${prefix}`)
    }
    return memberPath
  }
  for (const member of members.values()) {
    if (member.type === 'SymbolicLink') member.resolvedTarget = resolveMember(member.path)
  }
  return [...members.values()].sort((left, right) => left.path < right.path ? -1 : left.path > right.path ? 1 : 0)
}

export async function prepareVerificationDependencies({ sourceRoot, archivePath, archiveSha256, parentDirectory }) {
  if (!/^[a-f0-9]{64}$/.test(archiveSha256)) {
    throw Object.assign(new Error('Dependency preparation requires an explicit SHA-256 archive identity'), { code: 'dependency-candidate-integrity-invalid' })
  }
  rejectSymbolicVerificationInput(sourceRoot)
  const lockfile = path.join(sourceRoot, 'package-lock.json')
  if (!rejectSymbolicVerificationInput(lockfile).isFile()) throw invalidEntry('Dependency lockfile must be a regular file')
  const lockfileSha256 = digest(fs.readFileSync(lockfile))
  const bytes = fs.readFileSync(archivePath)
  if (digest(bytes) !== archiveSha256) throw Object.assign(new Error('Dependency archive differs from its selected identity'), { code: 'dependency-candidate-integrity-invalid' })
  const members = await readMembers(bytes)
  const children = new Map()
  for (const member of members) {
    const parent = path.posix.dirname(member.path)
    if (!children.has(parent)) children.set(parent, [])
    children.get(parent).push(path.posix.basename(member.path))
  }
  const allocatedRoot = fs.mkdtempSync(path.join(path.resolve(parentDirectory), 'verification-dependencies-'))
  try {
    const dependencyRoot = fs.realpathSync(allocatedRoot)
    for (const member of members.filter(member => member.type !== 'SymbolicLink')) {
      const destination = path.join(dependencyRoot, member.path)
      if (member.type === 'Directory') fs.mkdirSync(destination)
      else fs.writeFileSync(destination, member.bytes, { flag: 'wx' })
      fs.chmodSync(destination, member.mode)
    }
    for (const member of members.filter(member => member.type === 'SymbolicLink')) {
      fs.symlinkSync(member.target, path.join(dependencyRoot, member.path))
    }
    const entries = members.map(member => {
      const destination = path.join(dependencyRoot, member.path)
      const stat = fs.lstatSync(destination)
      if (member.type === 'SymbolicLink') {
        if (!stat.isSymbolicLink() || fs.readlinkSync(destination) !== member.target || fs.realpathSync(destination) !== path.join(dependencyRoot, member.resolvedTarget)) throw invalidEntry(`Materialized dependency link differs: ${member.path}`)
        return { path: member.path, type: member.type, target: member.target }
      }
      if ((stat.mode & 0o777) !== member.mode || (member.type === 'Directory' ? !stat.isDirectory() : !stat.isFile())) throw invalidEntry(`Materialized dependency type or mode differs: ${member.path}`)
      if (member.type === 'Directory') {
        const expected = (children.get(member.path) ?? []).sort()
        if (JSON.stringify(fs.readdirSync(destination).sort()) !== JSON.stringify(expected)) throw invalidEntry(`Materialized dependency inventory differs: ${member.path}`)
        return { path: member.path, type: member.type, mode: member.mode }
      }
      const actual = fs.readFileSync(destination)
      if (!actual.equals(member.bytes)) throw invalidEntry(`Materialized dependency bytes differ: ${member.path}`)
      return { path: member.path, type: member.type, mode: member.mode, size: actual.length, sha256: digest(actual) }
    })
    const dependencyDigest = digest(JSON.stringify({ archiveSha256, lockfileSha256, entries }))
    return { dependencyRoot, archiveSha256, lockfileSha256, entries, dependencyDigest, dispose() { fs.rmSync(dependencyRoot, { recursive: true, force: true }) } }
  } catch (error) {
    try {
      fs.rmSync(allocatedRoot, { recursive: true, force: true })
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Dependency preparation and cleanup failed', { cause: error })
    }
    throw error
  }
}
