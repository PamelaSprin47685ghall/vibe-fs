import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { materializeVerificationArchive } from './verification-archive.mjs'
import { rejectSymbolicVerificationInput } from './verification-input-path.mjs'

export async function prepareVerificationDependencies({ sourceRoot, archivePath, archiveSha256, parentDirectory }) {
  rejectSymbolicVerificationInput(sourceRoot)
  const lockfile = path.join(sourceRoot, 'package-lock.json')
  if (!rejectSymbolicVerificationInput(lockfile).isFile()) {
    throw Object.assign(new Error('Dependency lockfile must be a regular file'), { code: 'dependency-candidate-entry-invalid' })
  }
  const lockfileSha256 = createHash('sha256').update(fs.readFileSync(lockfile)).digest('hex')
  const prepared = await materializeVerificationArchive({ archiveBytes: fs.readFileSync(archivePath), archiveSha256, parentDirectory, rootDirectory: 'node_modules', errorPrefix: 'dependency-candidate' })
  const dependencyDigest = createHash('sha256').update(JSON.stringify({ archiveSha256, lockfileSha256, entries: prepared.entries })).digest('hex')
  return { dependencyRoot: prepared.root, archiveSha256, lockfileSha256, entries: prepared.entries, dependencyDigest, dispose: prepared.dispose }
}
