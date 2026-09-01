namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources
open ToolHostCodec

/// One process-local persistent JSON canvas interpreted by jq.
module AssumeTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/assume/description"

        [<Literal>]
        let ArgUpdate = "tool/assume/arg-update"

        [<Literal>]
        let ArgQuery = "tool/assume/arg-query"

    [<Import("json", "jq-wasm")>]
    let private jqJson (inputJson: string) (query: string) : JS.Promise<obj array> = jsNative

    [<Emit("$0.then($1).catch((error) => $2(String((error && (error.stderr || error.message)) || error)))")>]
    let private observeJq
        (promise: JS.Promise<obj array>)
        (onSuccess: obj array -> unit)
        (onError: string -> unit)
        : unit =
        jsNative

    let private runJq input query : Task<Result<obj array, string>> =
        let completion = TaskCompletionSource<Result<obj array, string>>()

        observeJq (jqJson (JS.JSON.stringify input) query) (Ok >> completion.SetResult) (Error >> completion.SetResult)

        completion.Task

    [<Emit("$0.map((value) => JSON.stringify(value, null, 2)).join('\\n')")>]
    let private renderOutputs (outputs: obj array) : string = jsNative

    // DSL-MUTABLE: resource — the single process-local persistent JSON canvas.
    let mutable private workspace: obj = createObj []
    // DSL-MUTABLE: resource — serializes update→query calls against the single canvas.
    let mutable private tail = Task.FromResult(())

    let private executeCore (args: HostToolArguments) =
        task {
            let update = args.Text "update"
            let query = args.Text "query"
            let! updateResult = runJq workspace update

            let nextWorkspace =
                match updateResult with
                | Error message -> raise (InvalidOperationException($"assume update failed: {message}"))
                | Ok outputs when outputs.Length <> 1 ->
                    raise (
                        InvalidOperationException(
                            $"assume update must produce exactly one JSON value; got {outputs.Length}; workspace unchanged"
                        )
                    )
                | Ok outputs -> outputs[0]

            workspace <- nextWorkspace
            let! queryResult = runJq workspace query

            return
                match queryResult with
                | Ok outputs -> renderOutputs outputs
                | Error message ->
                    raise (InvalidOperationException($"assume query failed after update committed: {message}"))
        }

    let private execute (args: HostToolArguments) (_ctx: HostToolContext) =
        let previous = tail
        let completion = TaskCompletionSource<string>()

        let next =
            task {
                try
                    do! previous
                with _ ->
                    ()

                try
                    let! result = executeCore args
                    completion.SetResult result
                with error ->
                    completion.SetException error
            }

        tail <- next
        completion.Task

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> r <> Role.Blogger && r <> Role.Distiller)

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "assume"
          Description = ProviderProse.render language Path.Description Map.empty
          Arguments =
            [ "update",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgUpdate Map.empty) factory
              "query",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgQuery Map.empty) factory ]
          Admission = admission
          Execute = execute }
