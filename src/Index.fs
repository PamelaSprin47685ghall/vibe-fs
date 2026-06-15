module VibeFs.Index

open Fable.Core

let createRegistration (deps: obj) : obj =
    VibeFs.MuxPlugin.Registration.createRegistration deps

let getPluginToolPolicy (_agentId: string) (role: string) : VibeFs.Kernel.MuxPolicy.MuxPluginToolPolicy option =
    let roleOpt = if System.String.IsNullOrEmpty role then None else Some role
    VibeFs.Kernel.MuxPolicy.getPluginToolPolicy roleOpt

let buildCapsFileReadData (projectRoot: string) : JS.Promise<VibeFs.Mux.CapsFileRead.CapsFileReadEntry[]> =
    VibeFs.Mux.CapsFileRead.buildCapsFileReadData projectRoot

let deduplicateReadOutputs (messages: obj array) : obj array =
    VibeFs.Mux.Dedup.deduplicateReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj array =
    VibeFs.Mux.Dedup.deduplicateReadOutputsWithSeen (List.ofArray seenOutputs) messages |> snd

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj array =
    let seen, result = VibeFs.Mux.Dedup.deduplicateModelReadOutputsWithSeen (List.ofArray seenOutputs) messages
    Array.ofList seen, result

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Mux.Dedup.collectReadOutputs messages |> Array.ofList
