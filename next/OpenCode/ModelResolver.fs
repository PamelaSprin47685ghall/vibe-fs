namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode

module ModelResolver =

    open Fable.Core
    open Fable.Core.JsInterop

    type ModelConfig =
        { SideA: OpencodeModel
          SideB: OpencodeModel }

    [<Import("env", "node:process")>]
    let private processEnv: obj = jsNative

    let private envVar (name: string) : string option =
        let value = processEnv?(name)
        if isNull value then None else Some(unbox<string> value)

    let private parseModel (s: string) : OpencodeModel =
        match s.Split('/') with
        | [| p; m |] ->
            { providerID = p
              modelID = m
              variant = None }
        | _ ->
            { providerID = "test"
              modelID = s
              variant = None }

    let fromEnv () : ModelConfig option =
        match envVar "WANXIANGSHU_MODEL_A", envVar "WANXIANGSHU_MODEL_B" with
        | Some a, Some b ->
            Some
                { SideA = parseModel a
                  SideB = parseModel b }
        | _ -> None

    let resolve (config: ModelConfig) (fallback: FallbackProjection option) : OpencodeModel option =
        match fallback with
        | None -> Some config.SideA
        | Some fb when fb.IsDead -> None
        | Some fb ->
            match fb.Side with
            | SideA -> Some config.SideA
            | SideB -> Some config.SideB

    let resolveForSession
        (config: ModelConfig)
        (sessionId: SessionId)
        (projection: ProjectionSet)
        : OpencodeModel option =
        let fallback =
            Map.tryFind sessionId projection.AgentProjections.Sessions
            |> Option.bind (fun session -> session.Fallback)

        resolve config fallback
