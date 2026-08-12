namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.OpenCode

/// Coexistence seam between builtin filesystem fallbacks and generated js-*.
/// GrandRewrite keeps read/edit/write/glob/grep as normal primitive fallback:
/// their Host descriptions are left untouched. Preference for intent-level
/// programs is taught inside the generated js-* contract and its Ultra Example.
module BuiltinToolDescriptionHook =

    [<Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// The builtin filesystem tools the hook may annotate (凡存在者).
    let BuiltinFilesystemTools =
        set [ "read"; "edit"; "write"; "glob"; "grep"; "patch" ]

    /// No provider annotation: primitive fallbacks are not deprecated.
    let hookSuffix (_jsRoleToolName: string) : string = ""

    let annotate (_builtinName: string) (description: string) (_jsRoleToolName: string) : string =
        description

    /// JS-003: the hook must not recommend a tool the provider cannot see.
    /// `visibleToolNames` is the current Attempt's tool set; a recommendation
    /// outside it is a lying hook and fails closed.
    let validateRecommendation (jsRoleToolName: string) (visibleToolNames: Set<string>) : Result<unit, string> =
        if Set.contains jsRoleToolName visibleToolNames then
            Ok()
        else
            Error(sprintf "hook recommends '%s' which is not provider-visible" jsRoleToolName)

/// JS-073/JS-074: a generated js-* tool spec — the dynamic counterpart of the
/// static baseSpecs. Built from a generated surface (JS-002); execution goes
/// through JsToolWorkflow (sandbox → staging → preflight → durable facts →
/// commit) and the result renders through JsToolsResult (JS-016).
module JsToolSpec =

    [<Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// Build a ToolSpec for one generated surface.
    ///
    /// `workspaceRoot` is where the tool executes; `persistence` enables the
    /// durable transaction facts (JS-012) when the caller has an EventStore.
    /// `modelSourceProvider` supplies the model program (the tool arguments);
    /// the first version reads it from the Host arguments payload.
    let create
        (factory: HostToolFactory)
        (surface: JsSurface)
        (workspaceRoot: string)
        (persistence: (IEventStore * IGitRawStore) option)
        : ToolSpec =
        let readProgram (args: HostToolArguments) : string option = args.OptionalText "program"

        { Name = surface.ToolName
          Description = surface.Description
          Arguments =
            [ "program",
              ToolHostCodec.stringSchemaDescribed
                  "Exactly one class named Js that extends the generated JsProgram in this tool description and implements async run()."
                  factory ]
          Execute =
            fun args _ ->
                task {
                    match readProgram args with
                    | None ->
                        return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString "missing 'program' argument" ]
                    | Some programSource ->
                        // 10 s sandbox deadline; 1 MiB output bound (JS-054).
                        let! outcome =
                            JsToolWorkflow.run
                                workspaceRoot
                                surface.BaseClassSource
                                programSource
                                10000
                                (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L)
                                (1 <<< 20)
                                persistence

                        return JsToolsResult.render outcome
                } }
