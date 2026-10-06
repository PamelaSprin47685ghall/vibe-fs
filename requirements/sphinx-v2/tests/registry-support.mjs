import {createHash} from 'node:crypto'
import * as Runtime from '../../../dist/Sphinx/V2/Runtime/Surface.js'

export const schemaHash = id => createHash('sha256').update(JSON.stringify({
  additionalProperties: false,
  properties: {fixture: {const: id, type: 'string'}},
  type: 'object',
})).digest('hex')

export const manifest = (id, dependencies = []) => ({
  id,
  release: 'fixture@2',
  implementationHash: 'implementation-' + id,
  abiHash: 'fixture-abi@2',
  capabilities: ['fixture.' + id],
  dependencies,
  schemas: [{name: 'response', id: 'fixture.response@2', hash: schemaHash(id)}],
})

export const declaration = (value, executableManifest = value) => ({manifest: value, executableManifest})

export const inspectRegistry = values => Runtime.inspectRegistryBinding(values)

export function permutations(values) {
  if (values.length === 0) return [[]]
  return values.flatMap((value, index) => permutations(values.filter((_, other) => other !== index)).map(rest => [value, ...rest]))
}
