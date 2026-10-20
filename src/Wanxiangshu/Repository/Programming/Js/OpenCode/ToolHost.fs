namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Repository.Programming.Js

/// PROMPT-019: load already-localized js-program prose. Domain assembles;
/// this module binds language.
module JsDescriptionAssets =

    let private text (lang: ProviderLanguage) (path: string) =
        ProviderProse.render lang path Map.empty

    let private template (lang: ProviderLanguage) (path: string) =
        ProviderResources.requireLanguagePair path
        ProviderResources.readText lang path

    let load (lang: ProviderLanguage) : JsCanonicalDescription.Prose =
        { Header = text lang JsCanonicalDescription.Path.Header

          Footer = text lang JsCanonicalDescription.Path.Footer
          Contract = template lang JsCanonicalDescription.Path.Contract

          ContractParallelEdits = template lang JsCanonicalDescription.Path.ContractParallelEdits
          ContractParallelReads = template lang JsCanonicalDescription.Path.ContractParallelReads
          VerbRead = text lang JsCanonicalDescription.Path.VerbRead

          VerbCreate = text lang JsCanonicalDescription.Path.VerbCreate
          ReadRules = template lang JsCanonicalDescription.Path.ReadRules
          ReviewOnlyRules = text lang JsCanonicalDescription.Path.ReviewOnlyRules
          Recipes = text lang JsCanonicalDescription.Path.Recipes

          WriteRules = text lang JsCanonicalDescription.Path.WriteRules
          MutationRules = text lang JsCanonicalDescription.Path.MutationRules
          Rm = text lang JsCanonicalDescription.Path.Rm
          Mv = text lang JsCanonicalDescription.Path.Mv
          RunRules = text lang JsCanonicalDescription.Path.RunRules
          Deliver = text lang JsCanonicalDescription.Path.Deliver
          DelegationRules = text lang JsCanonicalDescription.Path.DelegationRules
          UltraFraming = text lang JsCanonicalDescription.Path.UltraFraming
          UltraUnavailable = text lang JsCanonicalDescription.Path.UltraUnavailable
          UltraEngineering = text lang JsCanonicalDescription.Path.UltraEngineering
          UltraInvestigation = text lang JsCanonicalDescription.Path.UltraInvestigation
          UltraDevOps = text lang JsCanonicalDescription.Path.UltraDevOps
          UltraManagerInvestigation = text lang JsCanonicalDescription.Path.UltraManagerInvestigation
          UltraPlan = text lang JsCanonicalDescription.Path.UltraPlan
          MechanicalSemantic = text lang JsCanonicalDescription.Path.MechanicalSemantic

          ReasonRunUnimplemented = text lang JsCanonicalDescription.Path.ReasonRunUnimplemented

          Park = template lang JsCanonicalDescription.Path.Park
          ParkedPayload = template lang JsCanonicalDescription.Path.ParkedPayload
          BaseClass = template lang JsCanonicalDescription.Path.BaseClass
          BaseClassCallable = template lang JsCanonicalDescription.Path.BaseClassCallable
          BaseClassRead = template lang JsCanonicalDescription.Path.BaseClassRead
          BaseClassAnchors = template lang JsCanonicalDescription.Path.BaseClassAnchors
          BaseClassGlob = template lang JsCanonicalDescription.Path.BaseClassGlob
          BaseClassGrep = template lang JsCanonicalDescription.Path.BaseClassGrep
          BaseClassWrite = template lang JsCanonicalDescription.Path.BaseClassWrite
          BaseClassRemove = template lang JsCanonicalDescription.Path.BaseClassRemove
          BaseClassMove = template lang JsCanonicalDescription.Path.BaseClassMove
          BaseClassExec = template lang JsCanonicalDescription.Path.BaseClassExec
          BaseClassFork = template lang JsCanonicalDescription.Path.BaseClassFork
          BaseClassDag = template lang JsCanonicalDescription.Path.BaseClassDag
          BaseClassResume = template lang JsCanonicalDescription.Path.BaseClassResume
          BaseClassCommission = template lang JsCanonicalDescription.Path.BaseClassCommission
          BaseClassDevOps = template lang JsCanonicalDescription.Path.BaseClassDevOps
          BaseClassJoin = template lang JsCanonicalDescription.Path.BaseClassJoin
          BaseClassFission = template lang JsCanonicalDescription.Path.BaseClassFission
          BaseClassHorizon = template lang JsCanonicalDescription.Path.BaseClassHorizon
          BaseClassAdvisor = template lang JsCanonicalDescription.Path.BaseClassAdvisor
          BaseClassAsk = template lang JsCanonicalDescription.Path.BaseClassAsk
          BaseClassLifecycle = template lang JsCanonicalDescription.Path.BaseClassLifecycle
          JsPlan = text lang JsCanonicalDescription.Path.JsPlan }

    let argProgram (lang: ProviderLanguage) =
        text lang JsCanonicalDescription.Path.ArgProgram

    let missingProgram (lang: ProviderLanguage) =
        text lang JsCanonicalDescription.Path.MissingProgram

    let viewDeclarationExtra (lang: ProviderLanguage) =
        text lang JsCanonicalDescription.Path.ViewDeclarationExtra

/// JS-073/JS-074: a generated js-* tool spec — the dynamic counterpart of the
/// static baseSpecs. Built from a generated surface (JS-002); execution goes
/// through JsToolWorkflow (sandbox → staging → preflight → durable facts →
/// commit) and the result renders through JsToolsResult (JS-016).
module JsToolSpec =

    let admissionFor (roleName: string) : ToolAdmission =
        // Any office permission that projects a programming-surface member
        // admits the role's todowrite-for-* tool. JsPlan expands to Read/Write/Ask
        // at projection time; keep the grant itself so Plan is not special-cased.
        let surfacePermissions =
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.Exec
                  ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.Fission
                  ToolPermission.Horizon
                  ToolPermission.Advisor
                  ToolPermission.Ask
                  ToolPermission.JsPlan
                  ToolPermission.Resume
                  ToolPermission.Review
                  ToolPermission.ReviewAssessment
                  ToolPermission.Fetch
                  ToolPermission.DevOpsConsultation
                  ToolPermission.Deliver
                  ToolPermission.Finality ]

        ToolAdmission.OfficeRole(fun _ r ->
            (string r).ToLowerInvariant() = roleName.ToLowerInvariant()
            && not (Set.isEmpty (Set.intersect (OfficeCapability.permissions r) surfacePermissions)))

    [<Emit("$0===undefined||$0===null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// A Host argument is present when its key holds a value; null and
    /// undefined are absences.
    [<Emit("$0 !== null && $0 !== undefined")>]
    let private isPresent (value: obj) : bool = jsNative

    /// The directory one session's js-* surface executes in.
    ///
    /// The Host creates a Manager and her forked children rooted at the directory
    /// their owner registered — for a Change road that is the job's own worktree
    /// (`OrchestratorHost.directoryFor` → `HostForkRuntime.CreateChildSession`).
    /// The surface must execute where its session lives: resolving the plugin's
    /// project directory instead writes every road mutation into the target's own
    /// working tree, leaves the job worktree empty, and turns the candidate into a
    /// no-op commit whose publish rebase cannot conflict.
    ///
    /// A registered path is used as given, never existence-checked into the
    /// fallback: a worktree released under a live session must fail the write
    /// closed, not silently redirect it onto the target's checkout.
    let rootForSession
        (sessionDirectories: System.Collections.Generic.Dictionary<string, string>)
        (fallback: string)
        (sessionId: string)
        : string =
        match System.String.IsNullOrWhiteSpace sessionId, sessionDirectories.TryGetValue sessionId with
        | false, (true, path) when not (System.String.IsNullOrWhiteSpace path) -> path
        | _ -> fallback

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

            let unpark = if isPresent (args.Raw?unpark) then Some(args.Raw?unpark) else None

            let abandon = if isPresent (args.Raw?abandon) then Some(args.Raw?abandon) else None

            let programEntry =
                match program with
                | Some source -> Some("program" ==> box source)
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
                | Some abandonValue ->
                    Some("abandon" ==> createObj [ "handle" ==> string (abandonValue?handle) ])
                | None -> None

            [ programEntry; unparkEntry; abandonEntry ]
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

        // One view contract: the prefix is protected; the map states keep or summary.
        let mappingSchema =
            ToolHostCodec.optionalRecordSchemaOf
                (ToolHostCodec.stringSchema factory)
                (ToolHostCodec.nullableStringSchema factory)
                (JsDescriptionAssets.viewDeclarationExtra (ProviderLanguageBinding.readGlobalPreference ()))
                factory

        { Name = surface.ToolName
          Description = surface.Description
          Arguments =
            [ "program",
              ToolHostCodec.optionalStringSchemaDescribed
                  (JsDescriptionAssets.argProgram (ProviderLanguageBinding.readGlobalPreference ()))
                  factory
              "unpark", unparkSchema
              "abandon", abandonSchema
              "prefixUntil", ToolHostCodec.optionalNumberSchema factory
              "thenMap", mappingSchema ]
          Admission = admissionFor surface.RoleName
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
                                JsToolWorkflow.JsToolOutcome.Failed(
                                    JsFailure.InvalidArgument reason,
                                    [],
                                    [],
                                    [],
                                    []
                                )
                            )
                    | None ->
                        let hasProgram = isPresent (input?program)

                        let noState =
                            not hasProgram
                            && not (isPresent (input?unpark))
                            && not (isPresent (input?abandon))

                        let declaration =
                            if isUndefined (args.Raw?prefixUntil) && isUndefined (args.Raw?thenMap) then null
                            else
                                createObj [ "prefixUntil" ==> args.Raw?prefixUntil; "thenMap" ==> args.Raw?thenMap ]

                        let setView = Wanxiangshu.Repository.Programming.Js.Anchor.AnchorSurface.setView

                        // Clause 6: a failed call must not leave a presentable
                        // view declaration behind. The declaration is validated
                        // here — an invalid one is still refused before anything
                        // runs — but it is committed to the pending view only
                        // after the program settled, so a program that threw, exited
                        // non-zero or never settled leaves the pending view empty.
                        let viewFailure =
                            if not (isUndefined (args.Raw?view)) then
                                Some(JsFailure.ViewDeclarationInvalid "use prefixUntil and thenMap, not view")
                            elif not (isPresent declaration) then
                                None
                            else
                                match
                                    Wanxiangshu.Repository.Programming.Js.Anchor.AnchorSurface.parse
                                        declaration
                                with
                                | Ok _ -> None
                                | Error failure -> Some failure

                        match viewFailure with
                        | Some failure ->
                            return
                                JsToolsResult.render (
                                    JsToolWorkflow.JsToolOutcome.Failed(failure, [], [], [], [])
                                )
                        | None when noState ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "error",
                                      ToolHostCodec.TString(
                                          JsDescriptionAssets.missingProgram (
                                              ProviderLanguageBinding.readGlobalPreference ()
                                          )
                                      ) ]
                        | None ->
                            // 10 s sandbox deadline; 1 MiB output bound (JS-054).
                            let deadlineEpochMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L

                            // The session's own directory wins over the plugin workspace:
                            // a road's deliverable belongs in the worktree that session was
                            // created in, not in the target's checkout.
                            let root = rootOf ctx.SessionId

                            let fence =
                                match ctx.ProviderRunId, ctx.ToolCallId with
                                | Some providerRun, Some toolCallId -> Some(providerRun, toolCallId)
                                | _ -> None

                            // A delivery witness belongs to a fenced call: without the
                            // call fence there is nothing that can own the witness.
                            // A fence-less call therefore never carries a settlement.
                            let! settlement =
                                match fence with
                                | None -> Task.FromResult None
                                | Some _ -> settlementOf ctx

                            let! outcome =
                                match settlement with
                                | Some settlement ->
                                    JsToolWorkflow.executeWithReviewCheckAndDelivery
                                        (reviewCheck |> Option.map (fun check -> fun () -> check ctx))
                                        (Some settlement)
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
                                | None ->
                                    JsToolWorkflow.executeWithReviewCheck
                                        (reviewCheck |> Option.map (fun check -> fun () -> check ctx))
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

                            // Commit the declaration only now: the pending view is
                            // what the next request projects, and a failed program
                            // must not be able to present anything.
                            let commitFailure =
                                match outcome with
                                | JsToolWorkflow.JsToolOutcome.Failed _ -> None
                                | JsToolWorkflow.JsToolOutcome.Succeeded _
                                | JsToolWorkflow.JsToolOutcome.Parked _ ->
                                    match setView ctx.SessionId declaration with
                                    | Ok() -> None
                                    | Error failure -> Some failure

                            match commitFailure with
                            | Some failure ->
                                return
                                    JsToolsResult.render (
                                        JsToolWorkflow.JsToolOutcome.Failed(failure, [], [], [], [])
                                    )
                            | None -> return JsToolsResult.render outcome
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

    /// Build a ToolSpec with dedicated ToolAdmission.
    let createWithAdmission
        (factory: HostToolFactory)
        (surface: JsSurface)
        (admission: ToolAdmission)
        (workspaceRoot: string)
        (persistence: IJsTransactionPersistence option)
        (lifecyclePersistence: IJsLifecyclePersistence option)
        (fileAccessObservation: (HostToolContext -> JsExplicitFileRead list -> string list -> Task<unit>) option)
        (rootOf: string -> string)
        (settlementOf: HostToolContext -> Task<DeliverySettlement option>)
        : ToolSpec =
        let spec =
            create
                factory
                surface
                workspaceRoot
                persistence
                lifecyclePersistence
                fileAccessObservation
                rootOf
                settlementOf

        { spec with Admission = admission }
