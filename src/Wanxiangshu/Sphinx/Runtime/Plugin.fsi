namespace Wanxiangshu.Sphinx.Runtime

open Wanxiangshu.Sphinx.Core

type PluginManifest =
    { Id: string
      Release: string
      AbiHash: string
      Capabilities: Set<string>
      Dependencies: Set<string>
      Schemas: Map<string, SchemaRef> }

type PluginError =
    { Code: string
      Message: string }

type BoundPlugin = private BoundPlugin of PluginManifest

module Plugin =
    val validate: manifest: PluginManifest -> Result<PluginManifest, PluginError>
    val toPluginRef: manifest: PluginManifest -> PluginRef
    val toLockEntry: manifest: PluginManifest -> PluginLockEntry
    val bind: manifest: PluginManifest -> Result<BoundPlugin, PluginError>
    val manifestOf: bound: BoundPlugin -> PluginManifest
    val pluginRef: bound: BoundPlugin -> PluginRef
