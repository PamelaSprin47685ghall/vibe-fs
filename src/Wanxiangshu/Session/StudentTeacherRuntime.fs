namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Tools

module private StudentTeacherPath =
    [<Import("resolve", "node:path")>]
    let resolve (basePath: string, path: string) : string = jsNative

    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string) : obj = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: true }).decode($0)")>]
    let decodeUtf8Fatal (bytes: obj) : string = jsNative

type private StudentRun =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      Agent: string
      Tier: AgentTier
      CompileNudges: int }

type private TeacherAnswer =
    { Answer: string
      ToolRun: ProviderRunIdentity }

/// In-flight teacher call: delivery address + CE await points (Returned, Completion).
/// Nudges is a physical auto-recovery budget counter (EXEC-027), not a lifecycle stage.
type private TeacherCall =
    { Student: StudentRun
      Teacher: SessionId
      Returned: TaskCompletionSource<Result<TeacherAnswer, string>>
      Completion: TaskCompletionSource<Result<unit, string>>
      Nudges: int }

/// TextComplete rewrite arm only — presence must not select HandleTurn branches.
type private PendingCompletionText =
    { Text: string
      ToolRun: ProviderRunIdentity }

/// Registries after CE collapse / durable-evidence revise:
/// - `runs` — physical Student lifetime + delivery address
/// - `teacherCalls` — in-flight teacher call delivery + EXEC-027 single-flight latch
/// - `pendingCompletionTexts` — TextComplete rewrite arm (not a HandleTurn PC)
/// - `skillMutations` — observed skill write/edit evidence
/// Deleted: `teacherOwners` (durable association is sole truth), `teacherCompletions`
/// (answer+confirm live on TeacherCall CE stack). No function may jointly probe two
/// registry presences to choose an effect branch.
type StudentTeacherRuntime
    (
        sessions: ISessionHostPort,
        satellites: SatelliteRuntime,
        dispatcher: PromptDispatcher.Runtime,
        journal: AgentJournal,
        qa: StudentQaStore,
        workspaceDirectory: string,
        onTeacherReady: SessionId -> string -> unit,
        quiescence: SessionQuiescenceGate
    ) =
    let gate = obj ()
    let runs = Dictionary<string, StudentRun>()
    let teacherCalls = Dictionary<string, TeacherCall>()
    let pendingCompletionTexts = Dictionary<string, PendingCompletionText>()
    let skillMutations = Dictionary<string, Map<string, string>>()
    let recoveryBudget = AgentPairCursor.DefaultAutoRecoveryBudget

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId

    let normalizePayload (text: string) = if isNull text then "" else text.Trim()

    let appendFact owner fact =
        AgentJournal.appendAgent (StreamId.Session owner) None fact journal
        |> Result.map (fun _ -> ())
        |> Result.mapError JournalAppendFailure.describe

    let restoredTeacher owner =
        (AgentJournal.snapshot journal).AgentProjections.Associations
        |> SessionAssociationProjection.tryTeacherOf owner

    let teacherAgent run = StudentTeacher.teacherAgentFor run.Tier

    let linkTeacher owner teacher agent =
        appendFact
            owner
            (CompanionFact.StudentTeacherLinked
                {| SessionId = owner
                   TeacherSessionId = teacher
                   TeacherAgent = agent |})

    let closeTeacher owner =
        appendFact owner (CompanionFact.StudentTeacherClosed {| SessionId = owner |})

    let teacherSpec run =
        let agent = teacherAgent run

        { Kind = SatelliteKind.Teacher
          Agent = agent
          Title = agent
          Directory = Some workspaceDirectory
          RestoredSessionId = restoredTeacher run.SessionId
          Link = fun owner teacher linkedAgent -> linkTeacher owner teacher linkedAgent
          Close = closeTeacher }

    let toolMap role requestKind =
        PromptAuthority.toolCapabilitiesFor role requestKind
        |> StaticTools.requestToolMap

    let activeProfile sessionId = dispatcher.ActiveProfile sessionId

    let currentStudentRequestKind sessionId =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.map (fun authority ->
            let fromAccepted =
                authority.AcceptedContinuationIds
                |> Map.toSeq
                |> Seq.map snd
                |> Seq.tryPick (function
                    | PromptAuthority.ContinuationKind.StudentCompile
                    | PromptAuthority.ContinuationKind.StudentCompileNudge -> Some ProviderRequestKind.StudentCompile
                    | _ -> None)

            match fromAccepted with
            | Some kind -> kind
            | None ->
                // Claimed-but-not-Accepted must not fall through to Learn (ce.md).
                let claimedCompile =
                    authority.PendingClaims
                    |> Map.toSeq
                    |> Seq.map snd
                    |> Seq.exists (fun claim ->
                        match claim.Origin with
                        | PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.StudentCompile
                        | PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.StudentCompileNudge ->
                            true
                        | _ -> false)

                if claimedCompile then
                    ProviderRequestKind.StudentCompile
                else
                    ProviderRequestKind.StudentLearn)

    let tryRun sessionId =
        lock gate (fun () ->
            match runs.TryGetValue(sessionKey sessionId) with
            | true, run -> Some run
            | false, _ -> None)

    let tryOwner teacher =
        (AgentJournal.snapshot journal).AgentProjections.Associations
        |> SessionAssociationProjection.tryOwnerOf teacher

    let tryTeacherCall student =
        lock gate (fun () ->
            match teacherCalls.TryGetValue(sessionKey student) with
            | true, call -> Some call
            | false, _ -> None)

    let tryTeacherCallByTeacher teacher =
        lock gate (fun () -> teacherCalls.Values |> Seq.tryFind (fun call -> call.Teacher = teacher))

    let tryPendingText sessionId =
        lock gate (fun () ->
            match pendingCompletionTexts.TryGetValue(sessionKey sessionId) with
            | true, pending -> Some pending
            | false, _ -> None)

    let armPendingText sessionId pending =
        lock gate (fun () -> pendingCompletionTexts.[sessionKey sessionId] <- pending)

    let clearPendingText sessionId =
        lock gate (fun () -> pendingCompletionTexts.Remove(sessionKey sessionId) |> ignore)

    let updateTeacherCall student (update: TeacherCall -> TeacherCall) =
        lock gate (fun () ->
            let key = sessionKey student

            match teacherCalls.TryGetValue key with
            | true, current ->
                let next = update current
                teacherCalls.[key] <- next
                Some next
            | false, _ -> None)

    let updateStudentRun student (update: StudentRun -> StudentRun) =
        lock gate (fun () ->
            let key = sessionKey student

            match runs.TryGetValue key with
            | true, current ->
                let next = update current
                runs.[key] <- next
                Some next
            | false, _ -> None)

    let skillDocuments student =
        lock gate (fun () ->
            match skillMutations.TryGetValue(sessionKey student) with
            | true, documents -> documents
            | false, _ -> Map.empty)

    let validateTouchedSkills run : Result<unit, string> =
        let documents = skillDocuments run.SessionId

        if Map.isEmpty documents then
            Error "return rejected: StudentCompile must write or edit at least one loadable SKILL.md"
        else
            documents
            |> Map.toList
            |> List.fold
                (fun state (path, expectedName) ->
                    state
                    |> Result.bind (fun () ->
                        try
                            let content =
                                StudentTeacherPath.readFileSync path |> StudentTeacherPath.decodeUtf8Fatal

                            StudentSkill.validateDocument expectedName content
                            |> Result.mapError (fun error -> sprintf "%s: %s" path error)
                        with ex ->
                            Error(sprintf "%s: SKILL.md UTF-8 read failed: %s" path ex.Message)))
                (Ok())

    let failTeacherCall (call: TeacherCall) (error: string) =
        AsyncSupport.trySetResult call.Returned (Error error) |> ignore
        AsyncSupport.trySetResult call.Completion (Error error) |> ignore

    let removeTeacherCall student =
        lock gate (fun () -> teacherCalls.Remove(sessionKey student) |> ignore)

    let sendTeacherPrompt run (lease: SatelliteLease) question =
        task {
            let text =
                StudentTeacherPrompt.teacherQuestion question (lease.Origin = SatelliteOrigin.Replacement)

            let tools = toolMap Role.Teacher ProviderRequestKind.WorkMain
            let agent = teacherAgent run

            match activeProfile lease.SessionId with
            | None ->
                return!
                    dispatcher.SendAgentOwnerRootWithTools
                        sessions
                        lease.SessionId
                        text
                        agent
                        (Some workspaceDirectory)
                        PromptDispatcher.AwaitMode.Detached
                        None
                        tools
            | Some profile ->
                return!
                    dispatcher.SendContinuationWithTools
                        sessions
                        lease.SessionId
                        text
                        PromptAuthority.ContinuationKind.TeacherQuestion
                        profile
                        agent
                        (Some workspaceDirectory)
                        PromptDispatcher.AwaitMode.Detached
                        None
                        tools
        }

    let sendTeacherNudge run (permit: QuiescencePermit option) teacher =
        task {
            match permit with
            | None -> return Error "Superseded: no idle permit for TeacherIdleNudge"
            | Some current when not (quiescence.TryConsume current) ->
                return Error "Superseded: idle permit stale for TeacherIdleNudge"
            | Some _ ->
                match activeProfile teacher with
                | None -> return Error "Teacher nudge rejected: no active Teacher Authority Root"
                | Some profile ->
                    return!
                        dispatcher.SendContinuationWithTools
                            sessions
                            teacher
                            StudentTeacherPrompt.teacherIdleNudge
                            PromptAuthority.ContinuationKind.TeacherIdleNudge
                            profile
                            (teacherAgent run)
                            (Some workspaceDirectory)
                            PromptDispatcher.AwaitMode.Detached
                            None
                            (toolMap Role.Teacher ProviderRequestKind.WorkMain)
        }

    let sendCompile run isNudge (permit: QuiescencePermit option) =
        task {
            match isNudge, permit with
            | true, None -> return Error "Superseded: no idle permit for StudentCompileNudge"
            | true, Some current when not (quiescence.TryConsume current) ->
                return Error "Superseded: idle permit stale for StudentCompileNudge"
            | _ ->
                match qa.Path(run.SessionId, run.LogicalRunId), activeProfile run.SessionId with
                | Error error, _ -> return Error error
                | _, None -> return Error "Student compile rejected: no active Student Authority Root"
                | Ok path, Some profile ->
                    let continuation, text =
                        if isNudge then
                            PromptAuthority.ContinuationKind.StudentCompileNudge, StudentTeacherPrompt.compileNudge
                        else
                            PromptAuthority.ContinuationKind.StudentCompile, StudentTeacherPrompt.compile path

                    return!
                        dispatcher.SendContinuationWithTools
                            sessions
                            run.SessionId
                            text
                            continuation
                            profile
                            run.Agent
                            (Some workspaceDirectory)
                            PromptDispatcher.AwaitMode.Detached
                            None
                            (toolMap Role.Student ProviderRequestKind.StudentCompile)
        }

    let releaseStudent student =
        lock gate (fun () ->
            runs.Remove(sessionKey student) |> ignore
            teacherCalls.Remove(sessionKey student) |> ignore
            pendingCompletionTexts.Remove(sessionKey student) |> ignore
            skillMutations.Remove(sessionKey student) |> ignore)

    /// Registers the call under `teacherCalls` and fails unsettled waiters on dispose
    /// if the entry is still owned by this registration (send-fail / abandon paths).
    let beginTeacherCall (run: StudentRun) (teacher: SessionId) : TeacherCall * IDisposable =
        let returned =
            TaskCompletionSource<Result<TeacherAnswer, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let completion =
            TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let call =
            { Student = run
              Teacher = teacher
              Returned = returned
              Completion = completion
              Nudges = 0 }

        let key = sessionKey run.SessionId
        lock gate (fun () -> teacherCalls.[key] <- call)

        let registration =
            { new IDisposable with
                member _.Dispose() =
                    let stillOwned =
                        lock gate (fun () ->
                            match teacherCalls.TryGetValue key with
                            | true, current when Object.ReferenceEquals(current.Returned, call.Returned) ->
                                teacherCalls.Remove key |> ignore
                                true
                            | _ -> false)

                    if stillOwned then
                        failTeacherCall call "Teacher call scope disposed" }

        call, registration

    member _.ObserveChatMessage(message: PromptIngressCodec.DecodedMessage) : Result<unit, string> =
        match message.SessionId, message.PhysicalUserMessageId, message.PromptKey, message.Text with
        | Some sessionId, Some physical, None, Some text ->
            match activeProfile sessionId with
            | Some profile when
                profile.CanonicalRole = Role.Student
                && profile.AuthorityRootUserMessageId = PhysicalUserMessageId.promoteToAuthorityRoot physical
                ->
                let run =
                    lock gate (fun () ->
                        match runs.TryGetValue(sessionKey sessionId) with
                        | true, current when current.LogicalRunId = profile.LogicalRunId -> current
                        | _ ->
                            let created =
                                { SessionId = sessionId
                                  LogicalRunId = profile.LogicalRunId
                                  Agent = profile.SelectedAgent
                                  Tier = profile.SelectedTier
                                  CompileNudges = 0 }

                            runs.[sessionKey sessionId] <- created
                            created)

                match qa.Read(run.SessionId, run.LogicalRunId) with
                | Error error -> Error error
                | Ok current when String.IsNullOrEmpty current ->
                    qa.Append(run.SessionId, run.LogicalRunId, text) |> Result.map ignore
                | Ok current when StudentTeacher.hasOpening current text -> Ok()
                | Ok _ -> Error "Student QA opening does not match the active HumanRoot"
            | _ -> Ok()
        | _ -> Ok()

    member _.InvokeTeacher(studentSessionId: string, question: string) : Task<Result<string, string>> =
        task {
            let student = SessionId.create studentSessionId

            match tryRun student with
            | None -> return Error "teacher rejected: no active Student run"
            | Some run when currentStudentRequestKind student <> Some ProviderRequestKind.StudentLearn ->
                return Error "teacher rejected: Student is not in StudentLearn"
            | Some run ->
                let claimed =
                    lock gate (fun () -> not (teacherCalls.ContainsKey(sessionKey student)))

                if not claimed then
                    return Error "teacher rejected: another Student operation is in flight"
                else
                    match qa.Append(run.SessionId, run.LogicalRunId, question) with
                    | Error error -> return Error error
                    | Ok _ ->
                        match! satellites.Ensure(run.SessionId, teacherSpec run) with
                        | Error error ->
                            satellites.Invalidate(run.SessionId, SatelliteKind.Teacher)
                            return Error error
                        | Ok lease ->
                            let call, registration = beginTeacherCall run lease.SessionId

                            use _registration = registration

                            onTeacherReady lease.SessionId (teacherAgent run)

                            match! sendTeacherPrompt run lease question with
                            | Error error ->
                                failTeacherCall call error
                                removeTeacherCall student
                                return Error error
                            | Ok _ ->
                                // CE stack owns the handshake: return payload then fixed completion.
                                let! returned = call.Returned.Task

                                match returned with
                                | Error error -> return Error error
                                | Ok answer ->
                                    let! confirmed = call.Completion.Task

                                    return
                                        confirmed
                                        |> Result.map (fun () -> StudentTeacherPrompt.teacherAnswerResult answer.Answer)
        }

    member _.Return
        (sessionKeyValue: string, providerRunId: ProviderRunIdentity option, message: string)
        : Task<Result<string, string>> =
        task {
            let sessionId = SessionId.create sessionKeyValue

            match activeProfile sessionId with
            | Some profile when profile.CanonicalRole = Role.Teacher ->
                match
                    (tryOwner sessionId
                     |> Option.bind tryTeacherCall
                     |> Option.orElseWith (fun () -> tryTeacherCallByTeacher sessionId)),
                    providerRunId
                with
                | None, _ -> return Error "return rejected: Teacher has no active Student owner"
                | Some _, None -> return Error "return rejected: Host provided no Teacher provider-run identity"
                | Some call, Some toolRun when call.Teacher <> sessionId ->
                    return Error "return rejected: Teacher does not own the active Student call"
                | Some call, Some toolRun ->
                    // Fable Task has no IsCompleted — pending rewrite arm is the duplicate latch.
                    if tryPendingText sessionId |> Option.isSome then
                        return Error "return rejected: Teacher return completion is already pending"
                    else
                        match qa.Append(call.Student.SessionId, call.Student.LogicalRunId, message) with
                        | Error error -> return Error error
                        | Ok _ ->
                            armPendingText
                                sessionId
                                { Text = StudentTeacherPrompt.TeacherReturnCompletion
                                  ToolRun = toolRun }

                            AsyncSupport.trySetResult call.Returned (Ok { Answer = message; ToolRun = toolRun })
                            |> ignore

                            return Ok StudentTeacherPrompt.teacherReturnResult

            | Some profile when profile.CanonicalRole = Role.Student ->
                match tryRun sessionId, providerRunId, currentStudentRequestKind sessionId with
                | None, _, _ -> return Error "return rejected: no active Student run"
                | Some _, None, _ -> return Error "return rejected: Host provided no provider-run identity"
                | Some _, Some _, Some kind when kind <> ProviderRequestKind.StudentCompile ->
                    return Error "return rejected: Student is not in StudentCompile"
                | Some run, Some providerRun, _ when tryPendingText sessionId |> Option.isSome ->
                    return Error "return rejected: final completion is already pending"
                | Some run, Some providerRun, _ ->
                    match validateTouchedSkills run with
                    | Error error -> return Error error
                    | Ok() ->
                        match qa.Delete(run.SessionId, run.LogicalRunId) with
                        | Error error -> return Error error
                        | Ok() ->
                            armPendingText
                                sessionId
                                { Text = message
                                  ToolRun = providerRun }

                            return Ok(StudentTeacherPrompt.finalReturnResult message)
            | _ -> return Error "return rejected: role is neither active Student nor Teacher"
        }

    member _.TextComplete(input: obj, output: obj) =
        if
            not (isNull input)
            && not (isNull input?sessionID)
            && not (isNull input?messageID)
        then
            let sessionId = SessionId.create (unbox<string> input?sessionID)
            let completionRun = ProviderRunIdentity.create (unbox<string> input?messageID)

            match tryPendingText sessionId with
            | Some pending when completionRun <> pending.ToolRun -> output?text <- pending.Text
            | _ -> ()

    member _.ValidateTool(input: obj, output: obj) : Result<unit, string> =
        if isNull input || isNull input?sessionID || isNull input?tool then
            Error "Student/Teacher tool gate received an incomplete Host context"
        else
            let sessionId = SessionId.create (unbox<string> input?sessionID)
            let tool = unbox<string> input?tool

            match activeProfile sessionId with
            | Some profile when profile.CanonicalRole = Role.Student ->
                match tryRun sessionId with
                | None -> Error "Student tool rejected: no active QA-backed run"
                | Some _ when tryPendingText sessionId |> Option.isSome ->
                    Error "Student tool rejected: final text completion is already expected"
                | Some run ->
                    let requestKind =
                        currentStudentRequestKind sessionId
                        |> Option.defaultValue ProviderRequestKind.StudentLearn

                    let allowed =
                        PromptAuthority.toolCapabilitiesFor Role.Student requestKind
                        |> Set.map StaticTools.toolName

                    if not (Set.contains tool allowed) then
                        Error(sprintf "Tool '%s' is outside the active Student request profile" tool)
                    elif tool = "write" || tool = "edit" then
                        let args = if isNull output then null else output?args

                        let path =
                            if isNull args then null
                            elif not (isNull args?filePath) then args?filePath
                            elif not (isNull args?path) then args?path
                            else null

                        if isNull path then
                            Error(sprintf "StudentCompile %s requires a target path" tool)
                        else
                            let rawPath = unbox<string> path

                            match StudentSkill.targetName rawPath with
                            | Error error -> Error error
                            | Ok skillName ->
                                let target = StudentTeacherPath.resolve (workspaceDirectory, rawPath)

                                lock gate (fun () ->
                                    let current = skillDocuments sessionId
                                    skillMutations.[sessionKey sessionId] <- Map.add target skillName current)

                                Ok()
                    else
                        Ok()
            | Some profile when profile.CanonicalRole = Role.Teacher ->
                let allowed = Roles.permissions Role.Teacher |> Set.map StaticTools.toolName

                if tryPendingText sessionId |> Option.isSome then
                    Error "Teacher tool rejected: return completion is already expected"
                elif Set.contains tool allowed then
                    Ok()
                else
                    Error(sprintf "Tool '%s' is outside the Teacher execution profile" tool)
            | _ -> Ok()

    member _.HandleTurn(turn: ReconciledTurn, permit: QuiescencePermit option) : Task<bool> =
        task {
            match turn.Role with
            | Some Role.Student ->
                match tryRun turn.SessionId with
                | None -> return false
                | Some run ->
                    match turn.Outcome with
                    | ReconcileProgram.TurnCompleted ->
                        match currentStudentRequestKind run.SessionId with
                        | Some ProviderRequestKind.StudentLearn ->
                            let! _ = satellites.Retire(run.SessionId, teacherSpec run)
                            let! _ = sendCompile run false None

                            updateStudentRun run.SessionId (fun current -> { current with CompileNudges = 0 })
                            |> ignore

                            return true
                        | Some ProviderRequestKind.StudentCompile ->
                            // Durable evidence: QA absence means final return already deleted it.
                            match qa.Exists(run.SessionId, run.LogicalRunId) with
                            | Error _ ->
                                // Fail closed: do not throw across reconcile Running latch.
                                return true
                            | Ok false ->
                                let pending = tryPendingText turn.SessionId
                                let payload = normalizePayload (CompletedTurnClassifier.partsText turn.Parts)

                                let matched =
                                    match pending with
                                    | Some arm -> payload = normalizePayload arm.Text
                                    | None -> false

                                if matched then
                                    let! _ = satellites.Retire(run.SessionId, teacherSpec run)
                                    releaseStudent run.SessionId

                                return true
                            | Ok true ->
                                match run.CompileNudges >= recoveryBudget with
                                | true -> return true
                                | false ->
                                    match! sendCompile run true permit with
                                    | Ok _ ->
                                        updateStudentRun run.SessionId (fun current ->
                                            { current with
                                                CompileNudges = current.CompileNudges + 1 })
                                        |> ignore
                                    | Error _ -> ()

                                    return true
                        | _ -> return true
                    | ReconcileProgram.TurnAborted _ ->
                        let! _ = satellites.Retire(run.SessionId, teacherSpec run)
                        qa.Delete(run.SessionId, run.LogicalRunId) |> ignore
                        releaseStudent run.SessionId
                        return true
                    | _ -> return true
            | Some Role.Teacher ->
                match
                    tryOwner turn.SessionId
                    |> Option.bind tryTeacherCall
                    |> Option.orElseWith (fun () -> tryTeacherCallByTeacher turn.SessionId)
                with
                | None -> return true
                | Some call ->
                    match turn.Outcome with
                    | ReconcileProgram.TurnCompleted ->
                        let payload = normalizePayload (CompletedTurnClassifier.partsText turn.Parts)

                        if payload = normalizePayload StudentTeacherPrompt.TeacherReturnCompletion then
                            clearPendingText turn.SessionId
                            removeTeacherCall call.Student.SessionId
                            AsyncSupport.trySetResult call.Completion (Ok()) |> ignore
                            return true
                        else
                            // Payload is turn-carried physical evidence — not registry presence.
                            match call.Nudges >= recoveryBudget with
                            | true ->
                                clearPendingText turn.SessionId
                                removeTeacherCall call.Student.SessionId

                                failTeacherCall
                                    call
                                    (sprintf "Teacher idle recovery budget exhausted after %i nudges" recoveryBudget)

                                return true
                            | false ->
                                match! sendTeacherNudge call.Student permit turn.SessionId with
                                | Ok _ ->
                                    updateTeacherCall call.Student.SessionId (fun current ->
                                        { current with
                                            Nudges = current.Nudges + 1 })
                                    |> ignore
                                | Error _ -> ()

                                return true
                    | ReconcileProgram.TurnFailed error
                    | ReconcileProgram.TurnAborted error ->
                        clearPendingText turn.SessionId
                        removeTeacherCall call.Student.SessionId
                        failTeacherCall call (sprintf "Teacher run failed: %s" error)
                        return true
                    | _ -> return true
            | _ -> return false
        }

    member _.CancelSession(sessionId: SessionId) : Task<Result<unit, string>> =
        task {
            match tryRun sessionId with
            | None -> return Ok()
            | Some run ->
                let call = tryTeacherCall sessionId

                call
                |> Option.iter (fun scope ->
                    clearPendingText scope.Teacher
                    removeTeacherCall sessionId
                    failTeacherCall scope "Student run was cancelled")

                clearPendingText sessionId

                let deleteResult = qa.Delete(run.SessionId, run.LogicalRunId)
                let! retireResult = satellites.Retire(run.SessionId, teacherSpec run)
                releaseStudent sessionId

                match deleteResult, retireResult with
                | Ok(), Ok() -> return Ok()
                | Error deleteError, Ok() -> return Error deleteError
                | Ok(), Error retireError -> return Error retireError
                | Error deleteError, Error retireError -> return Error(sprintf "%s; %s" deleteError retireError)
        }

    member _.Dispose() =
        lock gate (fun () ->
            for call in teacherCalls.Values do
                failTeacherCall call "Student–Teacher runtime disposed"

            runs.Clear()
            teacherCalls.Clear()
            pendingCompletionTexts.Clear()
            skillMutations.Clear())

    interface IDisposable with
        member runtime.Dispose() = runtime.Dispose()
