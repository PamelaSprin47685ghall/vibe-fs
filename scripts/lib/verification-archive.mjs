import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { gunzipSync } from 'node:zlib'
import { Parser } from 'tar'

const digest = bytes => createHash('sha256').update(bytes).digest('hex')

async function readMembers(bytes, rootDirectory, errorPrefix) {
  const invalidEntry = message => Object.assign(new Error(message), { code: `${errorPrefix}-entry-invalid` })
  const tarBytes = bytes[0] === 0x1f && bytes[1] === 0x8b ? gunzipSync(bytes) : bytes
  const members = new Map()
  let invalid
  let complete = false
  await new Promise((resolve, reject) => {
    const parser = new Parser({ strict: true })
    parser.on('error', reject)
    parser.on('eof', () => { complete = true })
    parser.on('ignoredEntry', entry => { invalid ??= invalidEntry(`Ignored archive entry: ${entry.path}`) })
    parser.on('entry', entry => {
      const memberPath = entry.type === 'Directory' ? entry.path.replace(/\/$/, '') : entry.path
      const segments = memberPath.split('/')
      if (segments[0] !== rootDirectory || segments.some(segment => !segment || segment === '.' || segment === '..' || segment.toLowerCase() === '.git' || segment.includes('\\')) || members.has(memberPath)) {
        invalid ??= invalidEntry(`Invalid or duplicate archive path: ${entry.path}`)
      }
      if (!['File', 'Directory', 'SymbolicLink'].includes(entry.type) || !Number.isInteger(entry.mode) || (entry.mode & ~0o777) !== 0 || (entry.type === 'Directory' && (entry.mode & 0o700) !== 0o700)) {
        invalid ??= invalidEntry(`Unsupported archive entry: ${entry.path}`)
      }
      const member = { path: memberPath, type: entry.type, mode: entry.mode, target: entry.linkpath, chunks: [] }
      members.set(memberPath, member)
      entry.on('error', reject)
      entry.on('data', chunk => member.chunks.push(chunk))
      entry.on('end', () => {
        member.bytes = Buffer.concat(member.chunks)
        delete member.chunks
        if (member.bytes.length !== entry.size || (entry.type !== 'File' && entry.size !== 0)) invalid ??= invalidEntry(`Invalid archive entry size: ${entry.path}`)
      })
      entry.resume()
    })
    parser.on('end', resolve)
    for (let offset = 0; offset < tarBytes.length; offset += 512) {
      if (complete) {
        if (tarBytes.subarray(offset).some(byte => byte !== 0)) invalid ??= invalidEntry('Archive has non-padding data after termination')
        break
      }
      parser.write(tarBytes.subarray(offset, offset + 512))
    }
    parser.end()
  })
  if (invalid) throw invalid
  if (!complete || tarBytes.length % 512 !== 0) throw invalidEntry('Archive requires complete tar termination blocks')
  if (members.get(rootDirectory)?.type !== 'Directory' || ![...members.values()].some(member => member.type === 'File')) {
    throw invalidEntry(`Archive requires a ${rootDirectory} directory and file contents`)
  }
  for (const member of members.values()) {
    if (member.path !== rootDirectory && members.get(path.posix.dirname(member.path))?.type !== 'Directory') {
      throw invalidEntry(`Archive entry requires a real directory parent: ${member.path}`)
    }
    if (member.type === 'SymbolicLink') {
      if (!member.target || member.target.startsWith('/') || member.target.includes('\\')) throw invalidEntry(`Archive link must be relative: ${member.path}`)
      const segments = path.posix.dirname(member.path).split('/')
      let hasDescended = false
      for (const segment of member.target.split('/')) {
        if (segment === '..') {
          if (hasDescended) throw invalidEntry(`Archive link has an ambiguous parent traversal: ${member.path}`)
          if (segments.length === 1) throw invalidEntry(`Archive link escapes ${rootDirectory}: ${member.path}`)
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
      if (!member) throw invalidEntry(`Missing archive link target: ${memberPath}`)
      if (member.type === 'SymbolicLink') {
        if (seen.has(prefix)) throw invalidEntry(`Cyclic archive link: ${prefix}`)
        return resolveMember([member.destination, ...segments.slice(index)].join('/'), new Set([...seen, prefix]))
      }
      if (index < segments.length && member.type !== 'Directory') throw invalidEntry(`Archive link traverses a file: ${prefix}`)
    }
    return memberPath
  }
  for (const member of members.values()) {
    if (member.type === 'SymbolicLink') member.resolvedTarget = resolveMember(member.path)
  }
  return [...members.values()].sort((left, right) => left.path < right.path ? -1 : left.path > right.path ? 1 : 0)
}

export async function materializeVerificationArchive({ archiveBytes, archiveSha256, parentDirectory, rootDirectory, errorPrefix }) {
  const invalidEntry = message => Object.assign(new Error(message), { code: `${errorPrefix}-entry-invalid` })
  if (!/^[A-Za-z0-9_-]+$/.test(rootDirectory)) throw invalidEntry('Archive root must be a single ordinary path component')
  if (!/^[a-f0-9]{64}$/.test(archiveSha256)) {
    throw Object.assign(new Error('Archive preparation requires an explicit SHA-256 identity'), { code: `${errorPrefix}-integrity-invalid` })
  }
  if (!Buffer.isBuffer(archiveBytes) || digest(archiveBytes) !== archiveSha256) throw Object.assign(new Error('Archive differs from its selected identity'), { code: `${errorPrefix}-integrity-invalid` })
  const members = await readMembers(archiveBytes, rootDirectory, errorPrefix)
  const children = new Map()
  for (const member of members) {
    const parent = path.posix.dirname(member.path)
    if (!children.has(parent)) children.set(parent, [])
    children.get(parent).push(path.posix.basename(member.path))
  }
  const allocatedRoot = fs.mkdtempSync(path.join(path.resolve(parentDirectory), 'verification-archive-'))
  try {
    const archiveRoot = fs.realpathSync(allocatedRoot)
    for (const member of members.filter(member => member.type !== 'SymbolicLink')) {
      const destination = path.join(archiveRoot, member.path)
      if (member.type === 'Directory') fs.mkdirSync(destination)
      else fs.writeFileSync(destination, member.bytes, { flag: 'wx' })
      fs.chmodSync(destination, member.mode)
    }
    for (const member of members.filter(member => member.type === 'SymbolicLink')) {
      fs.symlinkSync(member.target, path.join(archiveRoot, member.path))
    }
    const entries = members.map(member => {
      const destination = path.join(archiveRoot, member.path)
      const stat = fs.lstatSync(destination)
      if (member.type === 'SymbolicLink') {
        if (!stat.isSymbolicLink() || fs.readlinkSync(destination) !== member.target || fs.realpathSync(destination) !== path.join(archiveRoot, member.resolvedTarget)) throw invalidEntry(`Materialized archive link differs: ${member.path}`)
        return { path: member.path, type: member.type, target: member.target }
      }
      if ((stat.mode & 0o777) !== member.mode || (member.type === 'Directory' ? !stat.isDirectory() : !stat.isFile())) throw invalidEntry(`Materialized archive type or mode differs: ${member.path}`)
      if (member.type === 'Directory') {
        const expected = (children.get(member.path) ?? []).sort()
        if (JSON.stringify(fs.readdirSync(destination).sort()) !== JSON.stringify(expected)) throw invalidEntry(`Materialized archive inventory differs: ${member.path}`)
        return { path: member.path, type: member.type, mode: member.mode }
      }
      const actual = fs.readFileSync(destination)
      if (!actual.equals(member.bytes)) throw invalidEntry(`Materialized archive bytes differ: ${member.path}`)
      return { path: member.path, type: member.type, mode: member.mode, size: actual.length, sha256: digest(actual) }
    })
    const expectedEntries = entries.map(entry => ({ ...entry }))
    const resolvedTargets = new Map(members.filter(member => member.type === 'SymbolicLink').map(member => [member.path, member.resolvedTarget]))
    function revalidate() {
      if (JSON.stringify(fs.readdirSync(archiveRoot)) !== JSON.stringify([rootDirectory])) throw invalidEntry('Materialized archive root inventory differs')
      for (const entry of expectedEntries) {
        const destination = path.join(archiveRoot, entry.path)
        const stat = fs.lstatSync(destination)
        if (entry.type === 'Directory') {
          if (!stat.isDirectory() || (stat.mode & 0o777) !== entry.mode || JSON.stringify(fs.readdirSync(destination).sort()) !== JSON.stringify((children.get(entry.path) ?? []).sort())) throw invalidEntry(`Materialized archive directory differs: ${entry.path}`)
        } else if (entry.type === 'File') {
          if (!stat.isFile() || (stat.mode & 0o777) !== entry.mode || stat.size !== entry.size || digest(fs.readFileSync(destination)) !== entry.sha256) throw invalidEntry(`Materialized archive file differs: ${entry.path}`)
        } else if (!stat.isSymbolicLink() || fs.readlinkSync(destination) !== entry.target || fs.realpathSync(destination) !== path.join(archiveRoot, resolvedTargets.get(entry.path))) {
          throw invalidEntry(`Materialized archive link differs: ${entry.path}`)
        }
      }
    }
    return { root: archiveRoot, archiveSha256, entries, revalidate, dispose() { fs.rmSync(archiveRoot, { recursive: true, force: true }) } }
  } catch (error) {
    try {
      fs.rmSync(allocatedRoot, { recursive: true, force: true })
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Archive preparation and cleanup failed', { cause: error })
    }
    throw error
  }
}
