namespace Wanxiangshu.Next.OpenCode

open System
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

    let private tryParseConfiguredModel (s: string) : Result<OpencodeModel, string> =
        match s.Split('/') with
        | [| provider; model |] when
            not (String.IsNullOrWhiteSpace provider)
            && not (String.IsNullOrWhiteSpace model)
            ->
            Ok
                { providerID = provider
                  modelID = model
                  variant = None }
        | _ -> Error "WANXIANGSHU_BLOGGER_MODEL must be provider/model"

    /// Resolve the dedicated Blogger model.  Missing and malformed
    /// configuration are both explicit errors: callers must not fall back to
    /// the primary model implicitly.
    let bloggerModelFromEnv () : Result<OpencodeModel, string> =
        match envVar "WANXIANGSHU_BLOGGER_MODEL" with
        | None -> Error "WANXIANGSHU_BLOGGER_MODEL is not configured"
        | Some value when String.IsNullOrWhiteSpace value -> Error "WANXIANGSHU_BLOGGER_MODEL is empty"
        | Some value -> tryParseConfiguredModel value

    let fromEnv () : ModelConfig option =
        match envVar "WANXIANGSHU_MODEL_A", envVar "WANXIANGSHU_MODEL_B" with
        | Some a, Some b ->
            Some
                { SideA = parseModel a
                  SideB = parseModel b }
        | _ -> None

    let resolve (config: ModelConfig) (fallback: FallbackProjection option) : OpencodeModel option =
        let autoFallbackDisabled = envVar "WANXIANGSHU_DISABLE_AUTO_FALLBACK" = Some "1"

        if autoFallbackDisabled then
            Some config.SideA
        else
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
