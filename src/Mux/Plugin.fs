module Wanxiangshu.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Mux.PluginCatalog
open Wanxiangshu.Mux.ReadDedup
open Wanxiangshu.Shell.WorkspaceFiles
open Wanxiangshu.Shell.Clock
module Dyn = Wanxiangshu.Shell.Dyn

let muxToolNames = PluginCatalog.muxToolNames

let getPluginToolPolicy (agentId: string) (role: obj) : obj =
    buildToolPolicy muxToolNames role

let collectReadOutputs (messages: obj array) : string[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.collectReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.deduplicateReadOutputsWithSeen seenOutputs messages

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    ReadDedup.deduplicateModelReadOutputsWithSeen seenOutputs messages

type CapsFileReadEntry =
    { path: string
      callId: string
      input: {| path: string |}
      output: {| success: bool; file_size: int; modifiedTime: string; lines_read: int; content: string |} }

let buildCapsFileReadData (projectRoot: string) : JS.Promise<CapsFileReadEntry[]> =
    promise {
        let! files = findCapsFiles projectRoot
        if List.isEmpty files then return [||]
        else
            let timestamp = getTimestampMs()
            let token = string timestamp
            let modified = System.DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("O")
            return
                files
                |> List.toArray
                |> Array.mapi (fun index f ->
                let lines = f.content.Split('\n')
                { path = f.label
                  callId = $"caps-fr-{token}-{index}"
                  input = {| path = f.label |}
                  output = {| success = true
                              file_size = f.content.Length
                              modifiedTime = modified
                              lines_read = lines.Length
                              content = lines |> Array.mapi (fun i line -> $"{i + 1}\t{line}") |> String.concat "\n" |} })
    }

let createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope =
    PluginCatalog.createToolCatalog deps toolNames reviewStore hostReadExec finderCache sessionScope

let createRegistration deps =
    Wanxiangshu.Mux.PluginRegistration.createRegistration deps