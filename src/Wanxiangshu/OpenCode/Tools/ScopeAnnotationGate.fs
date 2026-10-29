namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Context.Scope

/// Host-side Scope annotation gate. Schema is injected onto every ToolSpec;
/// semantics live in wanxiangli/scope-retention and common law «Scope».
module ScopeAnnotationGate =

    [<Import("scopeRetention", "wanxiangli/scope-retention")>]
    let private scopeRetention: obj = jsNative

    [<Emit("$0 == null")>]
    let private isNullish (_value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private field (_value: obj) (_name: string) : obj = jsNative

    /// Apply the required `scope` argument. Returns Error prose on refusal.
    let apply
        (toolName: string)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        (epochOf: HostToolContext -> string)
        : Result<obj, string> =
        let scope = field args.Raw "scope"

        if isNullish scope then
            Error "scope annotation required on every tool call (see common law «Scope»)"
        else
            match ctx.ToolCallId with
            | None -> Error "scope annotation requires tool call id"
            | Some call ->
                let callText = ToolCallId.value call
                // OpenCode pairs a provider run; OMP tool context may carry none —
                // fall back to the call id so same-turn meToo still groups.
                let runText =
                    match ctx.ProviderRunId with
                    | Some run -> ProviderRunIdentity.value run
                    | None -> callText

                let beatId = runText

                let epoch =
                    let e = epochOf ctx
                    if String.IsNullOrWhiteSpace e then "0" else e

                let receipt: obj =
                    scopeRetention?apply (
                        ctx.SessionId,
                        epoch,
                        toolName,
                        runText,
                        callText,
                        beatId,
                        scope
                    )

                if isNullish receipt || not (unbox<bool> receipt?ok) then
                    let reason =
                        if isNullish receipt then
                            "scope annotation refused"
                        else
                            string receipt?reason

                    Error reason
                else
                    let rem = receipt?remaining

                    if not (isNullish rem) then
                        try
                            Surface.setRetentionBudget ctx.SessionId (unbox<int> rem)
                        with _ ->
                            ()

                    if unbox<bool> (receipt?reanchor) = true then
                        try
                            Surface.resetScopeState ctx.SessionId (unbox<int> rem)
                        with _ ->
                            ()

                    Ok receipt

    let wrap
        (toolName: string)
        (epochOf: HostToolContext -> string)
        (inner: HostToolArguments -> HostToolContext -> Task<string>)
        : HostToolArguments -> HostToolContext -> Task<string> =
        fun args ctx ->
            task {
                match apply toolName args ctx epochOf with
                | Error reason -> return reason
                | Ok _ -> return! inner args ctx
            }

    let injectArgument (factory: HostToolFactory) (spec: ToolSpec) : ToolSpec =
        let already = spec.Arguments |> List.exists (fun (name, _) -> name = "scope")

        if already then
            spec
        else
            { spec with
                Arguments = spec.Arguments @ [ "scope", ToolHostCodec.scopeAnnotationSchema factory ] }
