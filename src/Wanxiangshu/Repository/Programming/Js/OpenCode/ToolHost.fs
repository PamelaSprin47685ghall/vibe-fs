namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Repository.Programming.Js

/// JS-073/JS-074: a generated js-* tool spec — the dynamic counterpart of the
/// static baseSpecs. Built from a generated surface (JS-002); execution goes
/// through JsToolWorkflow (sandbox → staging → preflight → durable facts →
/// commit) and the result renders through JsToolsResult (JS-016).
module JsToolSpec =

    /// A Host argument is present when its key holds a value; null and
    /// undefined are absences.
    [<Emit("$0 !== null && $0 !== undefined")>]
    let private isPresent (value: obj) : bool = jsNative

    /// Build a ToolSpec for one generated surface.
    ///
    /// `workspaceRoot` is where the tool executes; `persistence` enables the
    /// durable transaction facts (JS-012) when the caller has an EventStore.
    /// `modelSourceProvider` supplies the model program (the tool arguments);
    /// the first version reads it from the Host arguments payload.
    let private createCore
        (scopeApi: (HostToolContext -> Wanxiangshu.Process.JsSandbox.ParkState -> obj -> obj) option)
        (reviewCheck: (HostToolContext -> Task<bool>) option)
        (factory: HostToolFactory)
        (surface: JsSurface)
        (workspaceRoot: string)
        (persistence: IJsTransactionPersistence option)
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (fileAccessObservation: (HostToolContext -> JsExplicitFileRead list -> string list -> Task<unit>) option)
        (processHost: HostToolContext -> JsToolWorkflow.ProcessHost)
        (rootOf: string -> string)
        (settlementOf: HostToolContext -> Task<DeliverySettlement option>)
        : ToolSpec =
        let reviewCheck =
            match reviewCheck with
            | None when surface.RoleName.Trim().ToLowerInvariant() = "manager" -> Some(fun _ -> Task.FromResult false)
            | configured -> configured

        let readProgram (args: HostToolArguments) : string option = args.OptionalText "program"

        let executeFor ctx settlement =
            match scopeApi with
            | Some scope -> JsToolWorkflow.executeWithApiScope (scope ctx) settlement
            | None ->
                JsToolWorkflow.executeWithReviewCheckAndDelivery
                    (reviewCheck |> Option.map (fun check -> fun () -> check ctx))
                    settlement

        /// Build the three-state input object from the Host arguments:
        /// `{ program } | { unpark: { handle, value } } | { abandon: { handle } }`.
        ///
        /// Every present state is copied into the input object: a call that
        /// carries two of them is a contradiction the workflow refuses
        /// visibly, never a state this builder silently drops.
        let inputOf (args: HostToolArguments) : obj =
            let program = readProgram args

            let unpark =
                if isPresent (args.Raw?unpark) then
                    Some(args.Raw?unpark)
                else
                    None

            let abandon =
                if isPresent (args.Raw?abandon) then
                    Some(args.Raw?abandon)
                else
                    None

            let programEntry =
                match program with
                | Some source -> Some("program" ==> box source)
                | None -> None

            let nameEntry =
                match args.OptionalText "name" with
                | Some name -> Some("name" ==> box name)
                | None -> None

            let unparkEntry =
                match unpark with
                | Some unparkValue ->
                    Some(
                        "unpark"
                        ==> createObj [ "handle" ==> string (unparkValue?handle); "value" ==> unparkValue?value ]
                    )
                | None -> None

            let abandonEntry =
                match abandon with
                | Some abandonValue -> Some("abandon" ==> createObj [ "handle" ==> string (abandonValue?handle) ])
                | None -> None

            let contextEntries =
                [ "prefixUntil"; "thenMap"; "view" ]
                |> List.choose (fun key ->
                    let value = args.Raw?(key)
                    if isPresent value then Some(key ==> value) else None)

            ([ programEntry; nameEntry; unparkEntry; abandonEntry ] |> List.choose id)
            @ contextEntries
            |> createObj

        // The unpark/abandon arguments are nested objects; the Host schema
        // describes them as plain objects with string handle fields. The value
        // field of unpark is free-form (the injected resume value). Both are
        // optional: a call carries exactly one of program / unpark / abandon,
        // so no single one of them may be required by the schema.
        let unparkSchema =
            ToolHostCodec.optionalObjectSchema
                [ "handle", ToolHostCodec.stringSchema factory
                  "value", ToolHostCodec.anySchema factory ]
                factory

        let abandonSchema =
            ToolHostCodec.optionalObjectSchema [ "handle", ToolHostCodec.stringSchema factory ] factory

        // One view contract: the prefix is protected; the map states keep or summary.
        let mappingSchema =
            ToolHostCodec.optionalRecordSchemaOf
                (ToolHostCodec.stringSchema factory)
                (ToolHostCodec.nullableStringSchema factory)
                (JsToolPolicy.viewDeclarationExtraProse ())
                factory

        let callFenceOf (ctx: HostToolContext) =
            match ctx.ProviderRunId, ctx.ToolCallId with
            | Some providerRun, Some toolCallId -> Some(providerRun, toolCallId)
            | _ -> None

        let runProgramCall (ctx: HostToolContext) (input: obj) =
            task {
                // 10 s sandbox deadline; 1 MiB output bound (JS-054).
                let deadlineEpochMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L

                // The session's own directory wins over the plugin workspace:
                // a road's deliverable belongs in the worktree that session was
                // created in, not in the target's checkout.
                let root = rootOf ctx.SessionId
                let fence = callFenceOf ctx
                let! settlement = settlementOf ctx

                // Without a settlement this is exactly executeWithReviewCheck.
                let! outcome =
                    executeFor
                        ctx
                        settlement
                        ctx.SessionId
                        fence
                        surface.Capabilities
                        root
                        surface.BaseClassSource
                        input
                        10000
                        deadlineEpochMs
                        (1 <<< 20)
                        persistence
                        lifecyclePersistence
                        (fileAccessObservation |> Option.map (fun observe -> observe ctx))
                        (Some(processHost ctx))

                return JsToolsResult.render outcome
            }

        let admitProgramCall (ctx: HostToolContext) (input: obj) =
            let hasProgram = isPresent (input?program)

            let noState =
                not hasProgram
                && not (isPresent (input?unpark))
                && not (isPresent (input?abandon))

            if noState then
                Task.FromResult(
                    ToolResultText.tomlObject [ "error", ToolResultText.TString(JsToolPolicy.missingProgramProse ()) ]
                )
            else
                runProgramCall ctx input

        { Name = surface.ToolName
          Description = surface.Description
          Arguments =
            [ "program", ToolHostCodec.optionalStringSchemaDescribed (JsToolPolicy.argProgramProse ()) factory
              "name", ToolHostCodec.optionalStringSchemaDescribed (JsToolPolicy.argNameProse ()) factory
              "unpark", unparkSchema
              "abandon", abandonSchema
              "prefixUntil", ToolHostCodec.optionalNumberSchema factory
              "thenMap", mappingSchema ]
          Execute =
            fun args ctx ->
                task {
                    let input = inputOf args

                    // The three states are mutually exclusive: a call carries
                    // exactly one of program / unpark / abandon. A conflicting
                    // call is refused before the view declaration is captured
                    // and before anything runs.
                    match JsToolWorkflow.conflictingInputReason input with
                    | Some reason ->
                        return
                            JsToolsResult.render (
                                JsToolWorkflow.JsToolOutcome.Failed(JsFailure.InvalidArgument reason, [], [], [], [])
                            )
                    | None -> return! admitProgramCall ctx input
                } }

    let create
        factory
        surface
        workspaceRoot
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        rootOf
        settlementOf
        =
        createCore
            None
            None
            factory
            surface
            workspaceRoot
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost
            rootOf
            settlementOf

    let createWithReviewCheck
        check
        factory
        surface
        workspaceRoot
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        rootOf
        settlementOf
        =
        createCore
            None
            (Some check)
            factory
            surface
            workspaceRoot
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost
            rootOf
            settlementOf

    let createWithApiScope
        scope
        factory
        surface
        workspaceRoot
        persistence
        lifecyclePersistence
        fileAccessObservation
        processHost
        rootOf
        settlementOf
        =
        createCore
            (Some scope)
            None
            factory
            surface
            workspaceRoot
            persistence
            lifecyclePersistence
            fileAccessObservation
            processHost
            rootOf
            settlementOf
