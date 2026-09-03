namespace Wanxiangshu.Sphinx.Runtime

open System
open Wanxiangshu.Sphinx.Core

type PluginManifest =
    { Id: string
      Release: string
      AbiHash: string
      Capabilities: Set<string>
      Dependencies: Set<string>
      Schemas: Map<string, SchemaRef> }

type PluginError = { Code: string; Message: string }

type BoundPlugin = BoundPlugin of PluginManifest

module Plugin =

    let private error code message =
        Error { Code = code; Message = message }

    let validate manifest =
        if String.IsNullOrWhiteSpace manifest.Id then
            error "invalid-manifest" "plugin id must not be blank"
        elif String.IsNullOrWhiteSpace manifest.Release then
            error "invalid-manifest" "plugin release must not be blank"
        elif String.IsNullOrWhiteSpace manifest.AbiHash then
            error "invalid-manifest" "plugin abi hash must not be blank"
        else
            Ok manifest

    let toPluginRef manifest =
        { Id = manifest.Id
          Release = manifest.Release
          AbiHash = manifest.AbiHash }

    let toLockEntry manifest =
        { Plugin = toPluginRef manifest
          Capabilities = manifest.Capabilities
          Dependencies = manifest.Dependencies
          Schemas = manifest.Schemas }

    let bind manifest =
        validate manifest |> Result.map BoundPlugin

    let manifestOf (BoundPlugin manifest) = manifest

    let pluginRef bound = bound |> manifestOf |> toPluginRef
