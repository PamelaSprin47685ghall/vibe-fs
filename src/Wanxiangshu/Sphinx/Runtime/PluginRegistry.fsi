namespace Wanxiangshu.Sphinx.Runtime

open Wanxiangshu.Sphinx.Core

type RegistryError = { Code: string; Message: string }

module PluginRegistry =
    val ordered: manifests: PluginManifest list -> Result<PluginManifest list, RegistryError>
    val bind: manifests: PluginManifest list -> Result<PluginLockEntry list, RegistryError>
    val compatible: existing: PluginLockEntry list -> candidate: PluginLockEntry list -> Result<unit, RegistryError>

    val checkObservation:
        inquiryLock: PluginLockEntry list ->
        observationLock: PluginLockEntry list ->
        schema: SchemaRef ->
            Result<unit, RegistryError>
