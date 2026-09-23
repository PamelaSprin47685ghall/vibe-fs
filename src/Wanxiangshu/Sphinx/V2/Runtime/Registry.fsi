namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

type RegistryError = { Code: string; Message: string }

type LockedPlugin =
    { Manifest: PluginManifest
      Execute: ExecutablePlugin }

type PluginLockEntry =
    { Id: string
      Release: string
      ImplementationHash: string
      AbiHash: string
      Capabilities: Set<string>
      Dependencies: Set<string>
      Schemas: Map<string, SchemaRef> }

module Registry =
    val toLockEntry: LockedPlugin -> PluginLockEntry
    val ordered: LockedPlugin list -> Result<LockedPlugin list, RegistryError>
    val bind: LockedPlugin list -> Result<LockedPlugin list, RegistryError>
    val lockOf: LockedPlugin list -> PluginLockEntry list
    val compatible: PluginLockEntry list -> LockedPlugin list -> Result<unit, RegistryError>
