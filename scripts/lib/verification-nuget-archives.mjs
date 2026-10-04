import { createHash } from 'node:crypto'
import fs from 'node:fs'

export function captureVerificationNugetArchives(packageArchives, errorPrefix) {
  const invalidEntry = message => Object.assign(new Error(message), { code: `${errorPrefix}-entry-invalid` })
  const integrityError = message => Object.assign(new Error(message), { code: `${errorPrefix}-integrity-invalid` })
  if (!Array.isArray(packageArchives) || !packageArchives.length) throw invalidEntry('Restore requires explicitly identified package archives')
  const packages = packageArchives.map(selected => {
    const { id, version, archivePath, sha512 } = selected ?? {}
    if (typeof id !== 'string' || !/^[a-z0-9][a-z0-9._-]*$/.test(id) || typeof version !== 'string' || !/^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$/.test(version)) throw invalidEntry('Package requires an ordinary lowercase ID and exact version')
    if (typeof sha512 !== 'string' || !/^[A-Za-z0-9+/]{86}==$/.test(sha512) || Buffer.from(sha512, 'base64').toString('base64') !== sha512) throw integrityError('Package requires an explicit SHA512 identity')
    if (!fs.lstatSync(archivePath).isFile()) throw invalidEntry('Selected package archive requires a real file')
    const bytes = fs.readFileSync(archivePath)
    if (createHash('sha512').update(bytes).digest('base64') !== sha512) throw integrityError(`Package differs from its selected identity: ${id}/${version}`)
    return { id, version, sha512, bytes }
  }).sort((left, right) => `${left.id}/${left.version}`.localeCompare(`${right.id}/${right.version}`, 'en'))
  if (new Set(packages.map(pkg => `${pkg.id}/${pkg.version}`)).size !== packages.length) throw invalidEntry('Selected package identities must be distinct')
  return packages
}
