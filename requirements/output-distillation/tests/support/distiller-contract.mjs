import { managedAgentCatalog, roles } from '../../../verification-system/tests/support/domain.mjs'

const { runSpec } = await import('../../../../dist/OpenCode/Tools/ExecutorTool.js')
const { ToolHostCodec_factory } = await import('../../../../dist/OpenCode/Codec/ToolHostCodec.js')

const chain = (kind) => ({
  kind,
  describe: () => chain(`${kind}-described`),
  optional: () => chain(`${kind}-optional`),
})

const factory = ToolHostCodec_factory({
  tool: {
    schema: {
      string: () => chain('string'),
      number: () => chain('number'),
      enum: () => chain('enum'),
      boolean: () => chain('boolean'),
    },
  },
})

export const distillerContract = {
  publicRoles: () => managedAgentCatalog.allPublicRoles().map((name) => name.toLowerCase()),
  internalRoles: () => managedAgentCatalog.allInternalRoles().map((name) => name.toLowerCase()),
  machineName: () => managedAgentCatalog.nameOf(roles.tier('Fast'), roles.of('Distiller')),
  permissions: () => roles.permissions('Distiller'),
  runToolName: () => runSpec(factory, undefined).Name,
}
