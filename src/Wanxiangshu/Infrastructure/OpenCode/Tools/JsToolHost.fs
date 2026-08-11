namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.OpenCode

/// JS-003/JS-072/JS-074: the coexistence seam between builtin filesystem
/// tools and the generated js-* surface.
///
/// Builtin read/edit/write/glob/grep/patch keep their original schemas and
/// executors; the hook only rewrites their visible descriptions to deprecate
/// them in favour of the current js-ROLE. The hook is not a security scope:
/// it never changes builtin executability, and the js-* name it recommends
/// must be provider-visible at the same time (JS-003).
module BuiltinToolDescriptionHook =

    [<Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// The builtin filesystem tools the hook may annotate (凡存在者).
    let BuiltinFilesystemTools =
        set [ "read"; "edit"; "write"; "glob"; "grep"; "patch" ]

    /// Hook text appended to a builtin tool's description.
    ///
    /// Canonical shape (JS-003): DEPRECATED marker, the preferred js-* tool
    /// name, and a one-line push toward complex programs / parallel calls.
    let hookSuffix (jsRoleToolName: string) : string =
        "DEPRECATED — prefer "
        + jsRoleToolName
        + " for filesystem work: one capability-projected JS program per task, "
        + "parallel calls are safe."

    /// Apply the hook to a builtin description (idempotent: a description
    /// already carrying the DEPRECATED marker is not annotated twice).
    let annotate (builtinName: string) (description: string) (jsRoleToolName: string) : string =
        if not (Set.contains builtinName BuiltinFilesystemTools) then
            description
        elif description.Contains "DEPRECATED" then
            description
        else
            description + "\n\n" + hookSuffix jsRoleToolName

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
