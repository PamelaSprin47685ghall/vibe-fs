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
      Tier: AgentTier }

type private TeacherCallScope =
    { Student: StudentRun
      Teacher: SessionId
      Waiter: TaskCompletionSource<Result<string, string>> }

type private TeacherCompletionScope =
    { Call: TeacherCallScope
      ToolRun: ProviderRunIdentity
      Answer: string
      CompletionRun: ProviderRunIdentity option }

type private StudentFinalCompletionScope =
    { ProviderRun: ProviderRunIdentity
      Message: string }

/// Audited manual-proof classification (physical lifetimes, not stage encoding):
/// the six registries below — `runs`, `teacherOwners`, `teacherCalls`,
/// `teacherCompletions`, `studentFinalCompletions`, `skillMutations` — each own
/// one physical lifetime only (a teacher call, teacher completion, final
/// completion, or observed skill mutation). HandleTurn / observe paths MUST NOT
/// jointly match presence across these registries as an implicit program counter.
/// Student facts remain durable; no registry encodes a Student lifecycle stage.
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
    let teacherOwners = Dictionary<string, string>()
    let teacherCalls = Dictionary<string, TeacherCallScope>()
    let teacherCompletions = Dictionary<string, TeacherCompletionScope>()
    let studentFinalCompletions = Dictionary<string, StudentFinalCompletionScope>()
    let skillMutations = Dictionary<string, Map<string, string>>()

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId

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
        |> Result.map (fun () -> lock gate (fun () -> teacherOwners.[sessionKey teacher] <- sessionKey owner))

    let closeTeacher owner =
        appendFact owner (CompanionFact.StudentTeacherClosed {| SessionId = owner |})
        |> Result.map (fun () ->
            restoredTeacher owner
            |> Option.iter (fun teacher -> lock gate (fun () -> teacherOwners.Remove(sessionKey teacher) |> ignore)))

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
            authority.AcceptedContinuationIds
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.tryPick (function
                | PromptAuthority.ContinuationKind.StudentCompile
                | PromptAuthority.ContinuationKind.StudentCompileNudge -> Some ProviderRequestKind.StudentCompile
                | _ -> None)
            |> Option.defaultValue ProviderRequestKind.StudentLearn)

    let tryRun sessionId =
        lock gate (fun () ->
            match runs.TryGetValue(sessionKey sessionId) with
            | true, run -> Some run
            | false, _ -> None)

    let tryOwner teacher =
        lock gate (fun () ->
            match teacherOwners.TryGetValue(sessionKey teacher) with
            | true, owner -> Some(SessionId.create owner)
            | false, _ ->
                (AgentJournal.snapshot journal).AgentProjections.Associations
                |> SessionAssociationProjection.tryOwnerOf teacher)

    let tryTeacherCall student =
        lock gate (fun () ->
            match teacherCalls.TryGetValue(sessionKey student) with
            | true, scope -> Some scope
            | false, _ -> None)

    let tryTeacherCompletion teacher =
        lock gate (fun () ->
            match teacherCompletions.TryGetValue(sessionKey teacher) with
            | true, scope -> Some scope
            | false, _ -> None)

    let tryFinalCompletion student =
        lock gate (fun () ->
            match studentFinalCompletions.TryGetValue(sessionKey student) with
            | true, scope -> Some scope
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
            studentFinalCompletions.Remove(sessionKey student) |> ignore
            skillMutations.Remove(sessionKey student) |> ignore)

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
                                  Tier = profile.SelectedTier }

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
                let waiter =
                    TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

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
                            let scope =
                                { Student = run
                                  Teacher = lease.SessionId
                                  Waiter = waiter }

                            lock gate (fun () ->
                                teacherCalls.[sessionKey student] <- scope
                                teacherOwners.[sessionKey lease.SessionId] <- sessionKey student)

                            onTeacherReady lease.SessionId (teacherAgent run)

                            match! sendTeacherPrompt run lease question with
                            | Error error ->
                                lock gate (fun () -> teacherCalls.Remove(sessionKey student) |> ignore)
                                return Error error
                            | Ok _ ->
                                let! answer = waiter.Task
                                return answer |> Result.map StudentTeacherPrompt.teacherAnswerResult
        }

    member _.Return
        (sessionKeyValue: string, providerRunId: ProviderRunIdentity option, message: string)
        : Task<Result<string, string>> =
        task {
            let sessionId = SessionId.create sessionKeyValue

            match activeProfile sessionId with
            | Some profile when profile.CanonicalRole = Role.Teacher ->
                match tryOwner sessionId |> Option.bind tryTeacherCall, providerRunId with
                | None, _ -> return Error "return rejected: Teacher has no active Student owner"
                | Some _, None -> return Error "return rejected: Host provided no Teacher provider-run identity"
                | Some call, Some toolRun when call.Teacher <> sessionId ->
                    return Error "return rejected: Teacher does not own the active Student call"
                | Some call, Some toolRun ->
                    match tryTeacherCompletion sessionId with
                    | Some _ -> return Error "return rejected: Teacher return completion is already pending"
                    | None ->
                        match qa.Append(call.Student.SessionId, call.Student.LogicalRunId, message) with
                        | Error error -> return Error error
                        | Ok _ ->
                            let completion =
                                { Call = call
                                  ToolRun = toolRun
                                  Answer = message
                                  CompletionRun = None }

                            lock gate (fun () -> teacherCompletions.[sessionKey sessionId] <- completion)
                            return Ok StudentTeacherPrompt.teacherReturnResult

            | Some profile when profile.CanonicalRole = Role.Student ->
                match tryRun sessionId, providerRunId, currentStudentRequestKind sessionId with
                | None, _, _ -> return Error "return rejected: no active Student run"
                | Some _, None, _ -> return Error "return rejected: Host provided no provider-run identity"
                | Some _, Some _, Some kind when kind <> ProviderRequestKind.StudentCompile ->
                    return Error "return rejected: Student is not in StudentCompile"
                | Some run, Some providerRun, _ when tryFinalCompletion sessionId |> Option.isSome ->
                    return Error "return rejected: final completion is already pending"
                | Some run, Some providerRun, _ ->
                    match validateTouchedSkills run with
                    | Error error -> return Error error
                    | Ok() ->
                        match qa.Delete(run.SessionId, run.LogicalRunId) with
                        | Error error -> return Error error
                        | Ok() ->
                            lock gate (fun () ->
                                studentFinalCompletions.[sessionKey sessionId] <-
                                    { ProviderRun = providerRun
                                      Message = message })

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

            match tryFinalCompletion sessionId with
            | Some completion -> output?text <- completion.Message
            | None ->
                match tryTeacherCompletion sessionId with
                | None -> ()
                | Some completion ->
                    let completionRun = ProviderRunIdentity.create (unbox<string> input?messageID)

                    if completionRun <> completion.ToolRun then
                        lock gate (fun () ->
                            teacherCompletions.[sessionKey sessionId] <-
                                { completion with
                                    CompletionRun = Some completionRun })

                        output?text <- StudentTeacherPrompt.TeacherReturnCompletion

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
                | Some _ when tryFinalCompletion sessionId |> Option.isSome ->
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

                if tryTeacherCompletion sessionId |> Option.isSome then
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
                    match turn.Outcome, tryFinalCompletion turn.SessionId with
                    | ReconcileProgram.TurnCompleted, Some completion ->
                        if CompletedTurnClassifier.partsText turn.Parts = completion.Message then
                            let! _ = satellites.Retire(run.SessionId, teacherSpec run)
                            releaseStudent run.SessionId

                        return true
                    | ReconcileProgram.TurnCompleted, None ->
                        match currentStudentRequestKind run.SessionId with
                        | Some ProviderRequestKind.StudentLearn ->
                            let! _ = satellites.Retire(run.SessionId, teacherSpec run)
                            let! _ = sendCompile run false None
                            return true
                        | Some ProviderRequestKind.StudentCompile ->
                            let! _ = sendCompile run true permit
                            return true
                        | _ -> return true
                    | ReconcileProgram.TurnAborted _, _ ->
                        let! _ = satellites.Retire(run.SessionId, teacherSpec run)
                        qa.Delete(run.SessionId, run.LogicalRunId) |> ignore
                        releaseStudent run.SessionId
                        return true
                    | _ -> return true
            | Some Role.Teacher ->
                match tryTeacherCall (tryOwner turn.SessionId |> Option.defaultValue turn.SessionId) with
                | None -> return true
                | Some call ->
                    match turn.Outcome, tryTeacherCompletion turn.SessionId with
                    | ReconcileProgram.TurnCompleted, Some completion ->
                        let valid =
                            completion.CompletionRun.IsSome
                            && CompletedTurnClassifier.partsText turn.Parts = StudentTeacherPrompt.TeacherReturnCompletion

                        lock gate (fun () ->
                            teacherCompletions.Remove(sessionKey turn.SessionId) |> ignore
                            teacherCalls.Remove(sessionKey call.Student.SessionId) |> ignore)

                        if valid then
                            AsyncSupport.trySetResult call.Waiter (Ok completion.Answer) |> ignore
                        else
                            AsyncSupport.trySetResult
                                call.Waiter
                                (Error "Teacher return completion did not match the pending provider run")
                            |> ignore

                        return true
                    | ReconcileProgram.TurnCompleted, None ->
                        let! _ = sendTeacherNudge call.Student permit turn.SessionId
                        return true
                    | ReconcileProgram.TurnFailed error, _
                    | ReconcileProgram.TurnAborted error, _ ->
                        lock gate (fun () ->
                            teacherCompletions.Remove(sessionKey turn.SessionId) |> ignore
                            teacherCalls.Remove(sessionKey call.Student.SessionId) |> ignore)

                        AsyncSupport.trySetResult call.Waiter (Error(sprintf "Teacher run failed: %s" error))
                        |> ignore

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

                lock gate (fun () ->
                    teacherCompletions
                    |> Seq.filter (fun pair -> pair.Value.Call.Student.SessionId = sessionId)
                    |> Seq.map (fun pair -> pair.Key)
                    |> Seq.toList
                    |> List.iter (fun key -> teacherCompletions.Remove key |> ignore))

                call
                |> Option.iter (fun scope ->
                    AsyncSupport.trySetResult scope.Waiter (Error "Student run was cancelled")
                    |> ignore)

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
                AsyncSupport.trySetResult call.Waiter (Error "Student–Teacher runtime disposed")
                |> ignore

            runs.Clear()
            teacherOwners.Clear()
            teacherCalls.Clear()
            teacherCompletions.Clear()
            studentFinalCompletions.Clear()
            skillMutations.Clear())

    interface IDisposable with
        member runtime.Dispose() = runtime.Dispose()
