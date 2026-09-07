namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution

type private ExecutionLease =
    { PhysicalUserMessageId: string option
      Participant: string option
      RoutingRole: Role
      Target: ModelRoutingTarget }

module ModelRouting =

    let internal failureOfExecutionAdmissionAcquisition =
        function
        | ExecutionAdmissionAcquisition.QueueFull -> Some ExecutionFailure.CapacityQueueFull
        | ExecutionAdmissionAcquisition.Cancelled -> Some ExecutionFailure.UserCancelled
        | ExecutionAdmissionAcquisition.Superseded -> Some ExecutionFailure.Superseded
        | ExecutionAdmissionAcquisition.Admitted _
        | ExecutionAdmissionAcquisition.Queued _ -> None

    let internal capacityOwnership (lease: ExecutionAdmissionLease) =
        lease
        |> box
        |> ExactCapacityFenceReference.Create
        |> CapacityOwnership.OwnsExactFence

    [<Import("homedir", "node:os")>]
    let private homeDir () : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirName (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (left: string, right: string) : string = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string, options: obj) : unit = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string, content: string, options: obj) : unit = jsNative

    [<Import("linkSync", "node:fs")>]
    let private linkSync (existingPath: string, newPath: string) : unit = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("randomUUID", "node:crypto")>]
    let private randomUuid () : string = jsNative

    [<Import("pathToFileURL", "node:url")>]
    let private pathToFileUrl (path: string) : obj = jsNative

    [<Emit("import($0)")>]
    let private importModule (url: string) : Task<obj> = jsNative

    [<Emit("typeof $0 === 'function'")>]
    let private isFunction (value: obj) : bool = jsNative

    [<Emit("$0 != null && typeof $0.then === 'function'")>]
    let private isThenable (value: obj) : bool = jsNative

    [<Emit("$0($1, $2, $3)")>]
    let private callScheduler (scheduler: obj) (role: string) (running: obj array) (previous: obj) : obj = jsNative

    let private nonEmpty name (value: obj) =
        match value with
        | :? string as text when not (String.IsNullOrWhiteSpace text) -> text.Trim()
        | _ -> invalidOp (sprintf "execution-model-routing: scheduler target requires non-empty %s" name)

    let private requireFullModelSelector (model: string) =
        let slash = model.IndexOf '/'

        if slash <= 0 || slash >= model.Length - 1 then
            invalidOp "execution-model-routing: scheduler target model must be full provider/model"

        model

    let private parseTarget (value: obj) : ModelRoutingTarget =
        if isNull value then
            invalidOp "execution-model-routing: null is not a target"

        let model = nonEmpty "model" value?model |> requireFullModelSelector
        let reasoning = nonEmpty "reasoning" value?reasoning
        { Model = model; Reasoning = reasoning }

    let private targetObject (target: ModelRoutingTarget) =
        createObj [ "model" ==> target.Model; "reasoning" ==> target.Reasoning ]

    let invokeScheduler
        (scheduler: obj)
        (role: string)
        (running: ModelRoutingTarget array)
        (previous: ModelRoutingTarget option)
        : ModelRoutingTarget option =
        if not (isFunction scheduler) then
            invalidOp "execution-model-routing: scheduler default export must be a function"

        if String.IsNullOrWhiteSpace role then
            invalidOp "execution-model-routing: scheduler role must be non-empty"

        let result =
            callScheduler
                scheduler
                (role.Trim())
                (running |> Array.map targetObject)
                (previous |> Option.map targetObject |> Option.defaultValue null)

        if isThenable result then
            invalidOp "execution-model-routing: scheduler must be synchronous and must not return a Promise"

        if isNull result then None else Some(parseTarget result)

    let configPath () =
        pathJoin (pathJoin (homeDir (), ".config"), "opencode")
        |> fun root -> pathJoin (root, "wanxiangshu.mjs")

    let private errorCode (error: exn) =
        let value = box error

        if isNull value?code then None else Some(string value?code)

    let private acceptBootstrapLinkError error =
        match errorCode error with
        | Some "EEXIST" -> ()
        | _ -> raise error

    let private publishBootstrap tempPath path =
        try
            linkSync (tempPath, path)
        with ex ->
            acceptBootstrapLinkError ex

    let private removeBootstrapTemp tempPath =
        try
            unlinkSync tempPath
        with _ ->
            ()

    let private publishBootstrapTemplate tempPath path template =
        try
            writeFileSync (tempPath, template, createObj [ "encoding" ==> "utf8"; "flag" ==> "wx" ])
            publishBootstrap tempPath path
        finally
            removeBootstrapTemp tempPath

    let bootstrapAndLoadAt (path: string) (template: string) : Task<obj> =
        task {
            if String.IsNullOrWhiteSpace path then
                invalidArg "path" "execution-model-routing: scheduler path must be non-empty"

            mkdirSync (dirName path, createObj [ "recursive" ==> true ])

            // A direct O_EXCL write makes the destination name visible before all
            // bytes are written, so another OpenCode process could import a partial
            // module. Publish a fully-written same-directory inode with an atomic
            // hard-link instead; EEXIST means another bootstrap already won.
            let tempPath = path + ".tmp-" + randomUuid ()
            publishBootstrapTemplate tempPath path template

            let fileUrl = string (pathToFileUrl path)?href
            let! moduleObj = importModule fileUrl
            let scheduler = if isNull moduleObj then null else moduleObj?``default``

            if not (isFunction scheduler) then
                invalidOp "execution-model-routing: scheduler default export must be a function"

            if not (isNull moduleObj) then
                scheduler?markProviderFailed <- moduleObj?markProviderFailed
                scheduler?hasTheoreticalCapacity <- moduleObj?hasTheoreticalCapacity

            return scheduler
        }

    let private recommendedTemplate () =
        ModelRoutingResource.recommendedTemplate ()

    let bootstrapDefault () =
        bootstrapAndLoadAt (configPath ()) (recommendedTemplate ())

    let toOpenCodeModel (target: ModelRoutingTarget) : OpencodeModel =
        let slash = target.Model.IndexOf '/'

        { providerID = target.Model.Substring(0, slash)
          modelID = target.Model.Substring(slash + 1)
          variant = Some target.Reasoning }

    let ofOpenCodeModel (model: OpencodeModel) : ModelRoutingTarget option =
        if
            String.IsNullOrWhiteSpace model.providerID
            || String.IsNullOrWhiteSpace model.modelID
        then
            None
        else
            model.variant
            |> Option.bind (fun reasoning ->
                if String.IsNullOrWhiteSpace reasoning then
                    None
                else
                    Some
                        { Model = model.providerID.Trim() + "/" + model.modelID.Trim()
                          Reasoning = reasoning.Trim() })

    let sameTarget (expected: ModelRoutingTarget) (observed: OpencodeModel) =
        match ofOpenCodeModel observed with
        | Some actual -> actual = expected
        | None -> false

    /// A SessionId is a reusable container. Model occupancy belongs to the exact
    /// physical user material that caused the provider execution, never to the
    /// session lifecycle or to a cursor-selected identity. Strength may reserve one
    /// target before its physical prompt exists; chat.message later adopts that
    /// reservation into the exact PhysicalUserMessageId without double-counting.
    let private failedTask<'T> (error: exn) : Task<'T> =
        let completion =
            TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

        completion.SetException(error)
        completion.Task

    let private normalizeReservationInput sessionId (role: Role) =
        if String.IsNullOrWhiteSpace sessionId then
            Error(ArgumentException("sessionId must be non-empty") :> exn)
        else
            Ok(sessionId.Trim(), role)

    let private normalizeExecutionInput sessionId physicalUserMessageId (role: Role) (participant: string) =
        match normalizeReservationInput sessionId role with
        | Error error -> Error error
        | Ok(normSessionId, normAgent) when String.IsNullOrWhiteSpace physicalUserMessageId ->
            Error(ArgumentException("physicalUserMessageId must be non-empty") :> exn)
        | Ok(normSessionId, normRole) when String.IsNullOrWhiteSpace participant ->
            Error(ArgumentException("participant must be non-empty") :> exn)
        | Ok(normSessionId, normRole) -> Ok(normSessionId, physicalUserMessageId.Trim(), normRole, participant.Trim())

    let private normalizeSessionId sessionId =
        if String.IsNullOrWhiteSpace sessionId then
            None
        else
            Some(sessionId.Trim())

    let private normalizePhysicalExecutionKey sessionId physicalUserMessageId =
        match normalizeSessionId sessionId with
        | None -> None
        | Some _ when String.IsNullOrWhiteSpace physicalUserMessageId -> None
        | Some normSessionId -> Some(normSessionId, physicalUserMessageId.Trim())

    let private targetProvider (target: ModelRoutingTarget) =
        target.Model.Substring(0, target.Model.IndexOf '/')

    type internal ModelRoutingRuntime(scheduler: obj) =
        let gate = obj ()
        let transitionCounters = CapacityTransitionCounters()

        let capacity =
            BorrowingCapacity<ModelRoutingTarget>(CapacityLedger<ModelRoutingTarget>(), targetProvider, (=))
        // DSL-MUTABLE: resource — active execution lease map per session
        let activeBySession = Dictionary<string, ExecutionLease>()
        // DSL-MUTABLE: resource — one exact provider-run witness per live session
        let targetByProviderRun = Dictionary<string, struct (string * ModelRoutingTarget)>()
        let latestProviderRunBySession = Dictionary<string, string>()
        let admissionQueue = ExecutionAdmissionQueue(gate, transitionCounters)
        let admissionOwner = ExecutionCapacityOwner(transitionCounters)
        // DSL-MUTABLE: resource — process-local scheduler poison
        let mutable fatalError: exn option = None

        let markFailedFn: obj =
            if isNull scheduler then
                null
            else
                scheduler?markProviderFailed

        let hasCapFn: obj =
            if isNull scheduler then
                null
            else
                scheduler?hasTheoreticalCapacity

        let hasTheoreticalCapacityLocked (role: string) : bool =
            if not (isNull hasCapFn) && isFunction hasCapFn then
                unbox<bool> (callScheduler hasCapFn role [||] null)
            else
                true

        let running () = capacity.Snapshot()

        let activePhysicalTarget sessionId =
            match activeBySession.TryGetValue sessionId with
            | true, lease when lease.PhysicalUserMessageId.IsSome -> Some lease.Target
            | _ -> None

        let retireProviderRunTarget sessionId =
            match latestProviderRunBySession.TryGetValue sessionId with
            | true, providerRun ->
                latestProviderRunBySession.Remove sessionId |> ignore
                targetByProviderRun.Remove providerRun |> ignore
            | false, _ -> ()

        let rememberProviderRunTarget sessionId physicalUserMessageId providerRun =
            match activeBySession.TryGetValue sessionId with
            | true, lease when lease.PhysicalUserMessageId = Some physicalUserMessageId ->
                retireProviderRunTarget sessionId
                targetByProviderRun.[providerRun] <- struct (sessionId, lease.Target)
                latestProviderRunBySession.[sessionId] <- providerRun
            | _ -> ()

        let forgetLatestProviderRunIfCurrent sessionId providerRun =
            match latestProviderRunBySession.TryGetValue sessionId with
            | true, latest when latest = providerRun -> latestProviderRunBySession.Remove sessionId |> ignore
            | _ -> ()

        let takeProviderRunTarget providerRun =
            match targetByProviderRun.TryGetValue providerRun with
            | true, struct (sessionId, target) ->
                targetByProviderRun.Remove providerRun |> ignore
                forgetLatestProviderRunIfCurrent sessionId providerRun
                Some target
            | false, _ -> None

        let ensureHealthy () = fatalError |> Option.iter raise

        let poison (error: exn) =
            fatalError <- Some error
            capacity.Fail error
            admissionQueue.Fail error

        let scheduleOrPoison running (role: Role) previous =
            try
                invokeScheduler scheduler (Roles.roleLabel role) running previous
            with ex ->
                poison ex
                raise ex

        let routeFreshOrPoison
            sessionId
            oldPhysicalUserMessageId
            physicalUserMessageId
            (role: Role)
            lenderSessionId
            previous
            =
            try
                capacity.RouteFresh(
                    sessionId,
                    oldPhysicalUserMessageId,
                    physicalUserMessageId,
                    lenderSessionId,
                    fun running -> scheduleOrPoison running role previous
                )
            with ex ->
                poison ex
                raise ex

        let reserveFreshOrPoison sessionId (role: Role) lenderSessionId previous =
            try
                capacity.ReserveFresh(
                    sessionId,
                    lenderSessionId,
                    (fun running -> scheduleOrPoison running role previous)
                )
            with ex ->
                poison ex
                raise ex

        let rememberExecution (demand: ExecutionAdmissionDemand) (target: ModelRoutingTarget) =
            activeBySession.[demand.SessionId] <-
                { PhysicalUserMessageId = Some demand.PhysicalUserMessageId
                  Participant = Some demand.Participant
                  RoutingRole = demand.Role
                  Target = target }

        let commit (demand: ExecutionAdmissionDemand) (target: ModelRoutingTarget) =
            rememberExecution demand target

            let identity: ExecutionAdmissionExactIdentity =
                { SessionId = demand.SessionId
                  PhysicalUserMessageId = demand.PhysicalUserMessageId
                  Role = demand.Role
                  Participant = demand.Participant
                  Target = target }

            let lease =
                capacity.ExactCredit(demand.SessionId, demand.PhysicalUserMessageId)
                |> fun credit -> admissionOwner.Issue(identity, credit)

            admissionQueue.Admit(demand.Node, lease) |> ignore

        let commitScheduled (demand: ExecutionAdmissionDemand) (scheduled: ModelRoutingTarget option) =
            match scheduled with
            | None -> false
            | Some target ->
                commit demand target
                true

        let schedulePendingDemand (demand: ExecutionAdmissionDemand) =
            routeFreshOrPoison
                demand.SessionId
                None
                demand.PhysicalUserMessageId
                demand.Role
                demand.LenderSessionId
                demand.PreviousTarget
            |> commitScheduled demand

        let rec drainDemands () =
            ensureHealthy ()

            admissionQueue.Snapshot()
            |> Array.map schedulePendingDemand
            |> Array.exists id
            |> continueDrain

        and continueDrain progressed =
            if progressed && admissionQueue.Count > 0 then
                drainDemands ()

        let retireCurrentExecution sessionId =
            let changed = activeBySession.Remove sessionId
            retireProviderRunTarget sessionId
            capacity.ReleaseSession sessionId |> ignore

            if admissionQueue.ContainsSession sessionId then
                admissionQueue.CancelSession sessionId |> ignore

            if changed && fatalError.IsNone then
                drainDemands ()

        let retirePhysicalExecution sessionId physicalUserMessageId =
            let changed =
                match activeBySession.TryGetValue sessionId with
                | true, lease when lease.PhysicalUserMessageId = Some physicalUserMessageId ->
                    let removed = activeBySession.Remove sessionId
                    capacity.ReleasePhysical(sessionId, physicalUserMessageId) |> ignore
                    removed
                | _ -> false

            // Exact terminal evidence for an older physical execution must never
            // cancel a newer pending demand for the same reusable SessionId.
            if changed && fatalError.IsNone then
                drainDemands ()

        let requireSameIdentity
            sessionId
            physicalUserMessageId
            (expectedRole: Role)
            (expectedParticipant: string option)
            (observedRole: Role)
            (observedParticipant: string)
            =
            if expectedRole <> observedRole then
                invalidOp (
                    sprintf
                        "execution-model-routing: physical execution %s/%s changed role (%s -> %s)"
                        sessionId
                        physicalUserMessageId
                        (Roles.roleLabel expectedRole)
                        (Roles.roleLabel observedRole)
                )

            match expectedParticipant with
            | Some expected when expected <> observedParticipant ->
                invalidOp (
                    sprintf
                        "execution-model-routing: physical execution %s/%s changed participant (%s -> %s)"
                        sessionId
                        physicalUserMessageId
                        expected
                        observedParticipant
                )
            | _ -> ()

        let issueAdmission sessionId physicalUserMessageId (role: Role) (participant: string) target =
            let identity: ExecutionAdmissionExactIdentity =
                { SessionId = sessionId
                  PhysicalUserMessageId = physicalUserMessageId
                  Role = role
                  Participant = participant
                  Target = target }

            capacity.ExactCredit(sessionId, physicalUserMessageId)
            |> fun credit -> admissionOwner.Issue(identity, credit)
            |> ExecutionAdmissionAcquisition.Admitted


        let reuseOrAdoptActiveExecution
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lease: ExecutionLease)
            =
            match lease.PhysicalUserMessageId with
            | Some current when current = physicalUserMessageId ->
                requireSameIdentity sessionId physicalUserMessageId lease.RoutingRole lease.Participant role participant
                Some(issueAdmission sessionId physicalUserMessageId role participant lease.Target)
            | None ->
                requireSameIdentity sessionId physicalUserMessageId lease.RoutingRole lease.Participant role participant

                capacity.AdoptReservation(sessionId, physicalUserMessageId, lease.Target)

                activeBySession.[sessionId] <-
                    { lease with
                        PhysicalUserMessageId = Some physicalUserMessageId
                        Participant = Some participant }

                Some(issueAdmission sessionId physicalUserMessageId role participant lease.Target)
            | Some _ -> None

        let reusePendingExecution sessionId physicalUserMessageId (role: Role) (participant: string) =
            match admissionQueue.TryCurrent sessionId with
            | Some demand when demand.PhysicalUserMessageId = physicalUserMessageId ->
                requireSameIdentity
                    sessionId
                    physicalUserMessageId
                    demand.Role
                    (Some demand.Participant)
                    role
                    participant

                Some(ExecutionAdmissionAcquisition.Queued demand.Node)
            | Some _
            | None -> None

        let currentExecutionOutcome sessionId physicalUserMessageId (role: Role) (participant: string) =
            match activeBySession.TryGetValue sessionId with
            | true, lease -> reuseOrAdoptActiveExecution sessionId physicalUserMessageId role participant lease
            | false, _ -> reusePendingExecution sessionId physicalUserMessageId role participant

        let currentPhysicalUserMessageId sessionId =
            match activeBySession.TryGetValue sessionId with
            | true, lease -> lease.PhysicalUserMessageId
            | false, _ -> None

        let supersedeCurrentDemand sessionId =
            if admissionQueue.ContainsSession sessionId then
                admissionQueue.SupersedeSession sessionId |> ignore

        let acquireFreshDemand
            sessionId
            oldPhysicalUserMessageId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lenderSessionId: string option)
            (previous: ModelRoutingTarget option)
            =
            match
                routeFreshOrPoison
                    sessionId
                    oldPhysicalUserMessageId
                    physicalUserMessageId
                    role
                    lenderSessionId
                    previous
            with
            | Some target ->
                activeBySession.[sessionId] <-
                    { PhysicalUserMessageId = Some physicalUserMessageId
                      Participant = Some participant
                      RoutingRole = role
                      Target = target }

                drainDemands ()
                issueAdmission sessionId physicalUserMessageId role participant target
            | None ->
                admissionQueue.Enqueue(sessionId, physicalUserMessageId, role, participant, lenderSessionId, previous)

        let acquireFreshOrAdopt
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lenderSessionId: string option)
            =
            match currentExecutionOutcome sessionId physicalUserMessageId role participant with
            | Some current -> current
            | None ->
                let oldPhysicalUserMessageId = currentPhysicalUserMessageId sessionId
                let previous = activePhysicalTarget sessionId
                activeBySession.Remove sessionId |> ignore
                supersedeCurrentDemand sessionId

                acquireFreshDemand
                    sessionId
                    oldPhysicalUserMessageId
                    physicalUserMessageId
                    role
                    participant
                    lenderSessionId
                    previous

        let acquireManagedTask
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lenderSessionId: string option)
            =
            lock gate (fun () ->
                ensureHealthy ()

                if not (hasTheoreticalCapacityLocked (Roles.roleLabel role)) then
                    ExecutionAdmissionAcquisition.QueueFull
                else
                    acquireFreshOrAdopt sessionId physicalUserMessageId role participant lenderSessionId)

        let acquireManagedSafe
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lenderSessionId: string option)
            =
            try
                acquireManagedTask sessionId physicalUserMessageId role participant lenderSessionId
                |> Task.FromResult
            with ex ->
                failedTask<ExecutionAdmissionAcquisition> ex

        let tryReserveFresh sessionId (role: Role) (lenderSessionId: string option) =
            let previous = activePhysicalTarget sessionId

            match reserveFreshOrPoison sessionId role lenderSessionId previous with
            | None -> None
            | Some target ->
                activeBySession.[sessionId] <-
                    { PhysicalUserMessageId = None
                      Participant = None
                      RoutingRole = role
                      Target = target }

                drainDemands ()
                Some target

        let tryReserveLocked sessionId (role: Role) (lenderSessionId: string option) =
            ensureHealthy ()

            match activeBySession.TryGetValue sessionId, admissionQueue.ContainsSession sessionId with
            | (true, lease), _ when lease.PhysicalUserMessageId.IsNone && lease.RoutingRole = role -> Some lease.Target
            | (true, _), _ -> None
            | (false, _), true -> None
            | (false, _), false -> tryReserveFresh sessionId role lenderSessionId

        let adoptExistingReservation
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lease: ExecutionLease)
            =
            if lease.RoutingRole <> role then
                invalidOp (
                    sprintf
                        "execution-model-routing: physical execution %s/%s changed role (%s -> %s)"
                        sessionId
                        physicalUserMessageId
                        (Roles.roleLabel lease.RoutingRole)
                        (Roles.roleLabel role)
                )

            match lease.Participant with
            | Some expected when expected <> participant ->
                invalidOp (
                    sprintf
                        "execution-model-routing: physical execution %s/%s changed participant (%s -> %s)"
                        sessionId
                        physicalUserMessageId
                        expected
                        participant
                )
            | _ -> ()

            capacity.AdoptReservation(sessionId, physicalUserMessageId, lease.Target)

            let updated =
                { lease with
                    PhysicalUserMessageId = Some physicalUserMessageId
                    Participant = Some participant }

            activeBySession.[sessionId] <- updated
            Some lease.Target

        let routeFreshOrNone
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            lenderSessionId
            previous
            =
            match routeFreshOrPoison sessionId None physicalUserMessageId role lenderSessionId previous with
            | Some target ->
                activeBySession.[sessionId] <-
                    { PhysicalUserMessageId = Some physicalUserMessageId
                      Participant = Some participant
                      RoutingRole = role
                      Target = target }

                drainDemands ()
                Some target
            | None -> None

        let tryAcquireFreshLease
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lenderSessionId: string option)
            =
            if admissionQueue.ContainsSession sessionId then
                None
            else
                let previous = activePhysicalTarget sessionId
                routeFreshOrNone sessionId physicalUserMessageId role participant lenderSessionId previous

        let tryLeaseLocked
            sessionId
            physicalUserMessageId
            (role: Role)
            (participant: string)
            (lenderSessionId: string option)
            =
            match activeBySession.TryGetValue sessionId with
            | true, lease when
                lease.PhysicalUserMessageId = Some physicalUserMessageId
                && lease.RoutingRole = role
                && (lease.Participant = Some participant || lease.Participant.IsNone)
                ->
                Some lease.Target
            | true, lease when
                lease.PhysicalUserMessageId.IsNone
                && lease.RoutingRole = role
                && (lease.Participant = Some participant || lease.Participant.IsNone)
                ->
                adoptExistingReservation sessionId physicalUserMessageId role participant lease
            | true, _ -> None
            | false, _ -> tryAcquireFreshLease sessionId physicalUserMessageId role participant lenderSessionId

        let exactTargetAvailable (role: Role) target running =
            match scheduleOrPoison running role (Some target) with
            | Some candidate -> candidate = target
            | None -> false

        let enterProviderStepLocked sessionId physicalUserMessageId fence =
            ensureHealthy ()

            match activeBySession.TryGetValue sessionId with
            | true, lease when lease.PhysicalUserMessageId = Some physicalUserMessageId ->
                capacity.EnterStep(
                    sessionId,
                    physicalUserMessageId,
                    lease.Target,
                    fence,
                    fun running -> exactTargetAvailable lease.RoutingRole lease.Target running
                )
            | _ ->
                failedTask<unit> (
                    InvalidOperationException(
                        sprintf
                            "execution-model-routing: provider step %s/%s has no active execution binding"
                            sessionId
                            physicalUserMessageId
                    )
                )
                :> Task

        let drainIfHealthy () =
            if fatalError.IsNone then
                drainDemands ()

        let normalizeAdmissionInput sessionId physicalUserMessageId (role: Role) (participant: string) =
            normalizeExecutionInput sessionId physicalUserMessageId role participant

        let completePhysicalRelease sessionId physicalUserMessageId =
            function
            | CapacityTransitionOutcome.Applied as applied ->
                retirePhysicalExecution sessionId physicalUserMessageId
                drainIfHealthy ()
                applied
            | outcome -> outcome

        let releasePhysicalExecutionLocked (sessionId, physicalUserMessageId) =
            lock gate (fun () ->
                (match admissionQueue.TryCurrent sessionId with
                 | Some _ -> admissionQueue.CancelExecution(sessionId, physicalUserMessageId)
                 | None -> admissionOwner.ReleasePhysical(sessionId, physicalUserMessageId))
                |> completePhysicalRelease sessionId physicalUserMessageId)

        let capacitySnapshotLocked () =
            let physical: BorrowingCapacitySnapshot<ModelRoutingTarget> =
                capacity.InvariantSnapshot()

            let executions: CapacityExactOwnerSnapshot array =
                activeBySession
                |> Seq.choose (fun (KeyValue(sessionId, execution)) ->
                    execution.PhysicalUserMessageId
                    |> Option.map (fun physicalUserMessageId ->
                        ({ SessionId = sessionId
                           PhysicalUserMessageId = physicalUserMessageId
                           Role = Some execution.RoutingRole
                           Participant = execution.Participant }
                        : CapacityExactOwnerSnapshot)))
                |> Seq.sortBy (fun owner -> owner.SessionId, owner.PhysicalUserMessageId)
                |> Seq.toArray

            let identityByExecution: Map<string * string, Role option * string option> =
                executions
                |> Array.map (fun owner ->
                    (owner.SessionId, owner.PhysicalUserMessageId), (owner.Role, owner.Participant))
                |> Map.ofArray

            let enrichOwner (owner: CapacityExactOwnerSnapshot) : CapacityExactOwnerSnapshot =
                match Map.tryFind (owner.SessionId, owner.PhysicalUserMessageId) identityByExecution with
                | Some(role, participant) ->
                    { owner with
                        Role = role |> Option.orElse owner.Role
                        Participant = participant |> Option.orElse owner.Participant }
                | None -> owner

            let tokens: CapacityTokenSnapshot<ModelRoutingTarget> array =
                physical.Tokens
                |> Array.map (fun token ->
                    { token with
                        Owner = enrichOwner token.Owner })

            let custodies: CapacityCustodySnapshot array =
                physical.Custodies
                |> Array.map (fun custody ->
                    { custody with
                        Owner = enrichOwner custody.Owner })

            let admissionWaiters: CapacityWaiterSnapshot array =
                admissionQueue.Snapshot()
                |> Array.map (fun (demand: ExecutionAdmissionDemand) ->
                    ({ Owner =
                        ({ SessionId = demand.SessionId
                           PhysicalUserMessageId = demand.PhysicalUserMessageId
                           Role = Some demand.Role
                           Participant = Some demand.Participant }
                        : CapacityExactOwnerSnapshot)
                       Sequence = demand.Sequence
                       Kind = "Admission" }
                    : CapacityWaiterSnapshot))

            let waiters: CapacityWaiterSnapshot array =
                Array.append
                    (physical.Waiters
                     |> Array.map (fun waiter ->
                         { waiter with
                             Owner = enrichOwner waiter.Owner }))
                    admissionWaiters
                |> Array.sortBy (fun waiter -> waiter.Sequence, waiter.Owner.SessionId)

            let owners: CapacityExactOwnerSnapshot array =
                seq {
                    yield! executions
                    yield! tokens |> Seq.map _.Owner
                    yield! waiters |> Seq.map _.Owner
                }
                |> Seq.distinctBy (fun owner -> owner.SessionId, owner.PhysicalUserMessageId)
                |> Seq.sortBy (fun owner -> owner.SessionId, owner.PhysicalUserMessageId)
                |> Seq.toArray

            { LedgerEntries = physical.LedgerEntries
              Tokens = tokens
              Custodies = custodies
              Executions = executions
              Waiters = waiters
              Owners = owners
              Lineage = physical.Lineage
              IdleCount = physical.IdleCount
              InFlightCount = physical.InFlightCount
              RetiringCount = physical.RetiringCount
              ActiveCount = physical.InFlightCount + physical.RetiringCount
              Counters = transitionCounters.Snapshot() }

        member _.AcquireExecutionAdmission
            (
                sessionId: string,
                physicalUserMessageId: string,
                role: Role,
                participant: string,
                lenderSessionId: string option
            ) : Task<ExecutionAdmissionAcquisition> =
            match normalizeAdmissionInput sessionId physicalUserMessageId role participant with
            | Error error -> failedTask<ExecutionAdmissionAcquisition> error
            | Ok(normSessionId, normPhysicalUserMessageId, normRole, normParticipant) ->
                let normLender = lenderSessionId |> Option.bind normalizeSessionId
                acquireManagedSafe normSessionId normPhysicalUserMessageId normRole normParticipant normLender

        member _.ExecutionAdmissionTarget(lease: ExecutionAdmissionLease) = admissionOwner.Target lease

        member _.CommitExecutionAdmission(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
            admissionOwner.Commit(lease, observed)

        member _.ReleaseExecutionAdmissionBeforeProvider
            (lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity)
            =
            match admissionOwner.ReleaseBeforeProvider(lease, observed) with
            | CapacityTransitionOutcome.Applied as applied ->
                lock gate (fun () ->
                    retirePhysicalExecution lease.Identity.SessionId lease.Identity.PhysicalUserMessageId)

                applied
            | settlement -> settlement

        member _.ExecutionAdmissionLifecycle(lease: ExecutionAdmissionLease) = admissionOwner.LifecycleName lease

        /// Strength-only nonwaiting reservation. It is capacity-bearing, but not
        /// yet a provider execution identity. The exact chat.message later adopts
        /// it without another scheduler decision or another running occurrence.
        member _.TryReserveManaged
            (sessionId: string, role: Role, lenderSessionId: string option)
            : ModelRoutingTarget option =
            match normalizeReservationInput sessionId role with
            | Error _ -> None
            | Ok(normSessionId, normRole) ->
                let normLender = lenderSessionId |> Option.bind normalizeSessionId
                lock gate (fun () -> tryReserveLocked normSessionId normRole normLender)

        member _.TryLease
            (
                sessionId: string,
                physicalUserMessageId: string,
                role: Role,
                participant: string,
                lenderSessionId: string option
            ) : ModelRoutingTarget option =
            match normalizeExecutionInput sessionId physicalUserMessageId role participant with
            | Error _ -> None
            | Ok(normSessionId, normPhysicalUserMessageId, normRole, normParticipant) ->
                let normLender = lenderSessionId |> Option.bind normalizeSessionId

                lock gate (fun () ->
                    tryLeaseLocked normSessionId normPhysicalUserMessageId normRole normParticipant normLender)

        /// Physical end signals are cleanup evidence, not the sole correctness
        /// mechanism. A newer chat.message also supersedes this lease atomically.
        member internal _.ReleaseExecution(sessionId: string) =
            normalizeSessionId sessionId
            |> Option.map (fun normSessionId ->
                lock gate (fun () ->
                    let outcome =
                        currentPhysicalUserMessageId normSessionId
                        |> Option.map (fun physical -> admissionOwner.ReleasePhysical(normSessionId, physical))
                        |> Option.defaultWith (fun () -> admissionQueue.CancelSession normSessionId)

                    match outcome with
                    | CapacityTransitionOutcome.Applied -> retireCurrentExecution normSessionId
                    | CapacityTransitionOutcome.AlreadyApplied
                    | CapacityTransitionOutcome.StaleFence
                    | CapacityTransitionOutcome.Conflict -> ()

                    outcome))
            |> Option.defaultValue CapacityTransitionOutcome.Conflict

        /// Exact physical terminal evidence. Unlike force cleanup, this cannot
        /// retire a newer execution or pending demand that happens to reuse the
        /// same SessionId after the terminal event was produced.
        member internal _.ReleasePhysicalExecution(sessionId: string, physicalUserMessageId: string) =
            normalizePhysicalExecutionKey sessionId physicalUserMessageId
            |> Option.map releasePhysicalExecutionLocked
            |> Option.defaultValue CapacityTransitionOutcome.Conflict

        member _.CancelPendingExecution(sessionId: string) =
            normalizeSessionId sessionId
            |> Option.map (fun normSessionId -> lock gate (fun () -> admissionQueue.CancelSession normSessionId))
            |> Option.defaultValue CapacityTransitionOutcome.Conflict

        member _.CapacitySnapshot() = lock gate capacitySnapshotLocked

        member _.EnterProviderStep
            (sessionId: string, physicalUserMessageId: string, visibleProviderRuns: Set<string>)
            : Task =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> failedTask<unit> (ArgumentException("provider step identity must be non-empty")) :> Task
            | Some(normSessionId, normPhysicalUserMessageId) ->
                let admission =
                    lock gate (fun () ->
                        enterProviderStepLocked normSessionId normPhysicalUserMessageId visibleProviderRuns)

                task {
                    do! admission
                    lock gate drainIfHealthy
                }
                :> Task

        member _.EndProviderStep(sessionId: string, physicalUserMessageId: string, providerRun: string) =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> ()
            | Some _ when String.IsNullOrWhiteSpace providerRun -> ()
            | Some(normSessionId, normPhysicalUserMessageId) ->
                lock gate (fun () ->
                    let normalizedProviderRun = providerRun.Trim()
                    rememberProviderRunTarget normSessionId normPhysicalUserMessageId normalizedProviderRun
                    capacity.EndStep(normSessionId, normPhysicalUserMessageId, normalizedProviderRun)
                    drainIfHealthy ())

        member _.SuppressProviderStep(sessionId: string, physicalUserMessageId: string) =
            match normalizePhysicalExecutionKey sessionId physicalUserMessageId with
            | None -> ()
            | Some(normSessionId, normPhysicalUserMessageId) ->
                lock gate (fun () ->
                    capacity.SuppressStep(normSessionId, normPhysicalUserMessageId)
                    drainIfHealthy ())

        member _.SnapshotOccupied() = lock gate (fun () -> running ())
        member _.PendingCount = lock gate (fun () -> admissionQueue.Count)
        member _.PendingBound = ModelCapacityQueue.MaximumPendingDemands
        member _.PendingContractVersion = ModelCapacityQueue.ContractVersion

        member _.TakeProviderRunTarget(providerRun: string) : ModelRoutingTarget option =
            lock gate (fun () -> takeProviderRunTarget providerRun)

        member _.MarkProviderFailed(provider: string) =
            if not (isNull markFailedFn) && isFunction markFailedFn then
                callScheduler markFailedFn provider [||] null |> ignore

        member _.HasTheoreticalCapacity(role: string) : bool =
            lock gate (fun () -> hasTheoreticalCapacityLocked role)

    let private sharedGate = obj ()
    // DSL-MUTABLE: resource — process-shared scheduler runtime singleton
    let mutable private sharedRuntime: ModelRoutingRuntime option = None
    // DSL-MUTABLE: single-flight — in-flight scheduler bootstrap
    let mutable private sharedLoad: Task<ModelRoutingRuntime> option = None

    let private ensureShared () : Task<ModelRoutingRuntime> =
        lock sharedGate (fun () ->
            match sharedRuntime, sharedLoad with
            | Some runtime, _ -> Task.FromResult runtime
            | None, Some loading -> loading
            | None, None ->
                let loading =
                    task {
                        let! scheduler = bootstrapDefault ()
                        let runtime = ModelRoutingRuntime(scheduler)
                        lock sharedGate (fun () -> sharedRuntime <- Some runtime)
                        return runtime
                    }

                sharedLoad <- Some loading
                loading)

    let initialize () : Task =
        task {
            let! _ = ensureShared ()
            return ()
        }
        :> Task

    let private current () =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime
        | None -> invalidOp "execution-model-routing: scheduler runtime was not initialized during plugin load"

    let internal takeProviderRunTarget (providerRun: ProviderRunIdentity) : ModelRoutingTarget option =
        current().TakeProviderRunTarget(ProviderRunIdentity.value providerRun)

    let internal markProviderFailed (provider: string) : unit = current().MarkProviderFailed provider

    let internal hasTheoreticalCapacity (role: string) : bool = current().HasTheoreticalCapacity role

    let internal acquireExecutionAdmission
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (role: Role)
        (participant: string)
        (lenderSessionId: string option)
        =
        current()
            .AcquireExecutionAdmission(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                role,
                participant,
                lenderSessionId
            )

    let internal executionAdmissionTarget (lease: ExecutionAdmissionLease) =
        current().ExecutionAdmissionTarget lease

    let internal commitExecutionAdmission (lease: ExecutionAdmissionLease) (observed: ExecutionAdmissionExactIdentity) =
        current().CommitExecutionAdmission(lease, observed)

    let internal releaseExecutionAdmissionBeforeProvider
        (lease: ExecutionAdmissionLease)
        (observed: ExecutionAdmissionExactIdentity)
        =
        current().ReleaseExecutionAdmissionBeforeProvider(lease, observed)

    let hasRuntime () : bool =
        lock sharedGate (fun () -> sharedRuntime.IsSome)

    let tryReserveManaged (sessionId: SessionId) (role: Role) (lenderSessionId: string option) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.TryReserveManaged(SessionId.value sessionId, role, lenderSessionId)
        | None -> None

    let tryLease
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (role: Role)
        (participant: string)
        (lenderSessionId: string option)
        =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.TryLease(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                role,
                participant,
                lenderSessionId
            )
        | None -> None

    let internal releaseExecution (sessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.ReleaseExecution(SessionId.value sessionId)
        | None -> CapacityTransitionOutcome.AlreadyApplied

    let internal releasePhysicalExecution (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.ReleasePhysicalExecution(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId
            )
        | None -> CapacityTransitionOutcome.AlreadyApplied

    let internal observePhysicalResource (key: ChatExecutionKey) =
        let observation held =
            if held then
                PhysicalResourceObservation.ResourceHeld key
            else
                PhysicalResourceObservation.ResourceAbsent key

        match lock sharedGate (fun () -> sharedRuntime) with
        | None -> PhysicalResourceObservation.ResourceAbsent key
        | Some runtime ->
            let sessionId = SessionId.value key.SessionId
            let physicalUserMessageId = PhysicalUserMessageId.value key.PhysicalUserMessageId

            let exact (owner: CapacityExactOwnerSnapshot) =
                owner.SessionId = sessionId
                && owner.PhysicalUserMessageId = physicalUserMessageId

            let snapshot = runtime.CapacitySnapshot()

            let held =
                snapshot.Owners |> Array.exists exact
                || snapshot.Custodies |> Array.exists (fun custody -> exact custody.Owner)

            observation held

    let internal cancelUnacquiredExecution (sessionId: SessionId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime -> runtime.CancelPendingExecution(SessionId.value sessionId)
        | None -> CapacityTransitionOutcome.AlreadyApplied

    let internal capacitySnapshot () = current().CapacitySnapshot()

    let enterProviderStep
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (visibleProviderRuns: Set<ProviderRunIdentity>)
        =
        current()
            .EnterProviderStep(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                visibleProviderRuns |> Set.map ProviderRunIdentity.value
            )

    let endProviderStep
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (providerRun: ProviderRunIdentity)
        =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.EndProviderStep(
                SessionId.value sessionId,
                PhysicalUserMessageId.value physicalUserMessageId,
                ProviderRunIdentity.value providerRun
            )
        | None -> ()

    let suppressProviderStep (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        match lock sharedGate (fun () -> sharedRuntime) with
        | Some runtime ->
            runtime.SuppressProviderStep(SessionId.value sessionId, PhysicalUserMessageId.value physicalUserMessageId)
        | None -> ()

    let private requireOutputMessage output =
        let message = if isNull output then null else output?message

        if isNull message then
            invalidOp "EMR-009: managed chat.message routing has no mutable output.message"

        message

    /// EMR-009 Host projection. Routing owns both which outcomes carry a model
    /// and the exact mutable Host field that receives that model; composition
    /// roots only invoke this published projection.
    let projectHostModel (output: obj) (model: OpencodeModel) =
        try
            let message = requireOutputMessage output
            message?model <- box model
            Ok()
        with error ->
            Error error
