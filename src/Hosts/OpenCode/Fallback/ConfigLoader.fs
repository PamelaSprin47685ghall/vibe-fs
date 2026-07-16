module Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader

open Wanxiangshu.Kernel.FallbackKernel.Types

let loadFallbackConfig (directory: string) : FallbackConfig option =
    Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.loadFallbackConfig directory

let buildAgentModelOverrides (cfg: FallbackConfig) : Map<string, string> =
    cfg.AgentChains
    |> Map.map (fun _ chain ->
        match chain with
        | model :: _ ->
            match model.Variant with
            | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
            | None -> sprintf "%s/%s" model.ProviderID model.ModelID
        | [] -> "")
    |> Map.filter (fun _ v -> v <> "")

let defaultPreferredModel (cfg: FallbackConfig) : string option =
    match cfg.DefaultChain with
    | model :: _ ->
        match model.Variant with
        | Some v -> Some(sprintf "%s/%s:%s" model.ProviderID model.ModelID v)
        | None -> Some(sprintf "%s/%s" model.ProviderID model.ModelID)
    | [] -> None
