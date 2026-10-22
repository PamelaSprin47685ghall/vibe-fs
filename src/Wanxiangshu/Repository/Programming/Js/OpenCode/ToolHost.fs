namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
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
        (reviewCheck: (HostToolContext -> Task<bool>) option)
        (factory: HostToolFactory)
        (surface: JsSurface)
        (workspaceRoot: string)
        (persistence: IJsTransactionPersistence option)
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (fileAccessObservation: (HostToolContext -> JsExplicitFileRead list -> string list -> Task<unit>) option)
        (rootOf: string -> string)
        (settlementOf: HostToolContext -> Task<DeliverySettlement option>)
        : ToolSpec =
        let reviewCheck =
            match reviewCheck with
            | None when surface.RoleName.Trim().ToLowerInvariant() = "manager" -> Some(fun _ -> Task.FromResult false)
            | configured -> configured

        let readProgram (args: HostToolArguments) : string option = args.OptionalText "program"

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

            [ programEntry; nameEntry; unparkEntry; abandonEntry ]
            |> List.choose id
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

        let carrySchema =
            ToolHostCodec.optionalCarrySchema (JsToolPolicy.viewDeclarationExtraProse ()) factory

        let callFenceOf (ctx: HostToolContext) =
            match ctx.ProviderRunId, ctx.ToolCallId with
            | Some providerRun, Some toolCallId -> Some(providerRun, toolCallId)
            | _ -> None

        // A delivery witness belongs to a fenced call: without the
        // call fence there is nothing that can own the witness.
        // A fence-less call therefore never carries a settlement.
        // RT-02: attribution resolution is deferred to delivery time.
        let settlementFor (ctx: HostToolContext) fence : DeliverySettlement option =
            match fence with
            | None -> None
            | Some _ -> Some (DeliverySettlement.DeferredDelivery (fun () -> settlementOf ctx))

        let runProgramCall (ctx: HostToolContext) (input: obj) (declaration: obj) =
            task {
                // 10 s sandbox deadline; 1 MiB output bound (JS-054).
                let deadlineEpochMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L

                // The session's own directory wins over the plugin workspace:
                // a road's deliverable belongs in the worktree that session was
                // created in, not in the target's checkout.
                let root = rootOf ctx.SessionId
                let fence = callFenceOf ctx

                let callIdentity =
                    fence
                    |> Option.map (fun (providerRun, toolCallId) ->
                        ProviderRunIdentity.value providerRun, ToolCallId.value toolCallId)

                let settlement = settlementFor ctx fence

                // Without a settlement this is exactly executeWithReviewCheck.
                let! outcome =
                    JsToolWorkflow.executeWithReviewCheckAndDelivery
                        (reviewCheck |> Option.map (fun check -> fun () -> check ctx))
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

                return JsToolPolicy.renderCommitted ctx.SessionId callIdentity declaration outcome
            }

        // Clause 6: a failed call must not leave a presentable
        // view declaration behind. The declaration is validated
        // here — an invalid one is still refused before anything
        // runs — but it is committed to the pending view only
        // after the program settled, so a program that threw, exited
        // non-zero or never settled leaves the pending view empty.
        let admitProgramCall (args: HostToolArguments) (ctx: HostToolContext) (input: obj) =
            let hasProgram = isPresent (input?program)

            let noState =
                not hasProgram
                && not (isPresent (input?unpark))
                && not (isPresent (input?abandon))

            let declaration = JsToolPolicy.viewDeclarationOf args.Raw

            match JsToolPolicy.viewFailureOf args.Raw declaration with
            | Some failure ->
                Task.FromResult(JsToolsResult.render (JsToolWorkflow.JsToolOutcome.Failed(failure, [], [], [], [])))
            | None when noState ->
                Task.FromResult(
                    ToolResultText.tomlObject [ "error", ToolResultText.TString(JsToolPolicy.missingProgramProse ()) ]
                )
            | None ->
                match
                    Wanxiangshu.Repository.Programming.Js.Anchor.AnchorSurface.validateView ctx.SessionId declaration
                with
                | Error failure ->
                    Task.FromResult(JsToolsResult.render (JsToolWorkflow.JsToolOutcome.Failed(failure, [], [], [], [])))
                | Ok() -> runProgramCall ctx input declaration

        { Name = surface.ToolName
          Description = surface.Description
          Arguments =
            [ "program", ToolHostCodec.optionalStringSchemaDescribed (JsToolPolicy.argProgramProse ()) factory
              "name", ToolHostCodec.optionalStringSchemaDescribed (JsToolPolicy.argNameProse ()) factory
              "unpark", unparkSchema
              "abandon", abandonSchema
              "prefixCount", ToolHostCodec.optionalNumberSchema factory
              "carry", carrySchema ]
          Execute =
            fun args ctx ->
                task {
                    let input =
                        let raw = inputOf args
                        if not (isNull raw) && not (isNull raw?program) && (isNull raw?name || string raw?name = "") then
                            let defaultName =
                                ctx.ToolCallId
                                |> Option.map ToolCallId.value
                                |> Option.orElseWith (fun () -> ctx.ProviderRunId |> Option.map ProviderRunIdentity.value)
                                |> Option.defaultValue ("call-" + ctx.SessionId)
                            createObj
                                [ "program" ==> raw?program
                                  "name" ==> defaultName ]
                        else
                            raw

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
                    | None -> return! admitProgramCall args ctx input
                } }

    let create
        factory
        surface
        workspaceRoot
        persistence
        lifecyclePersistence
        fileAccessObservation
        rootOf
        settlementOf
        =
        createCore
            None
            factory
            surface
            workspaceRoot
            persistence
            lifecyclePersistence
            fileAccessObservation
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
        rootOf
        settlementOf
        =
        createCore
            (Some check)
            factory
            surface
            workspaceRoot
            persistence
            lifecyclePersistence
            fileAccessObservation
            rootOf
            settlementOf
