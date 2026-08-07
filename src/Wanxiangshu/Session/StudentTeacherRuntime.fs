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
open Wanxiangshu.Tools

module private StudentTeacherPath =
    [<Import("resolve", "node:path")>]
    let resolve (basePath: string, path: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let relative (fromPath: string, toPath: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let isAbsolute (path: string) : bool = jsNative

type private StudentRunCell =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      Agent: string
      Tier: AgentTier
      mutable State: StudentTeacher.RunState
      mutable TeacherSessionId: SessionId option
      mutable Waiter: TaskCompletionSource<Result<string, string>> option
      mutable PendingFinal: (ProviderRunIdentity * string) option }

/// EXEC-025/026: control-plane owner for Student learning. This type stores no
/// question or answer text; QA.md is the sole knowledge state.
type StudentTeacherRuntime
    (
        sessions: ISessionHostPort,
        satellites: SatelliteRuntime,
        dispatcher: PromptDispatcher.Runtime,
        journal: AgentJournal,
        qa: StudentQaStore,
        workspaceDirectory: string,
        onTeacherReady: SessionId -> string -> unit
    ) =
    let gate = obj ()
    let runs = Dictionary<string, StudentRunCell>()
    let teacherOwners = Dictionary<string, string>()
    let expectedTeacherAborts = HashSet<string>()

    let sessionKey (sessionId: SessionId) = SessionId.value sessionId

    let appendFact owner fact =
        AgentJournal.appendAgent (StreamId.Session owner) None fact journal
        |> Result.map (fun _ -> ())
        |> Result.mapError JournalAppendFailure.describe

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
            lock gate (fun () ->
                match runs.TryGetValue(sessionKey owner) with
                | true, cell ->
                    cell.TeacherSessionId
                    |> Option.iter (fun teacher -> teacherOwners.Remove(sessionKey teacher) |> ignore)

                    cell.TeacherSessionId <- None
                | false, _ -> ()))

    let teacherAgent (cell: StudentRunCell) =
        StudentTeacher.teacherAgentFor cell.Tier

    let restoredTeacher owner =
        (AgentJournal.snapshot journal).AgentProjections.Associations
        |> SessionAssociationProjection.tryTeacherOf owner

    let teacherSpec (cell: StudentRunCell) =
        let agent = teacherAgent cell

        { Kind = SatelliteKind.Teacher
          Agent = agent
          Title = agent
          Directory = Some workspaceDirectory
          RestoredSessionId =
            cell.TeacherSessionId
            |> Option.orElseWith (fun () -> restoredTeacher cell.SessionId)
          Link = fun owner teacher linkedAgent -> linkTeacher owner teacher linkedAgent
          Close = closeTeacher }

    let toolMap role requestKind =
        PromptAuthority.toolCapabilitiesFor role requestKind
        |> StaticTools.requestToolMap

    let activeProfile sessionId = dispatcher.ActiveProfile sessionId

    let sendTeacherPrompt (cell: StudentRunCell) (lease: SatelliteLease) question qaPath =
        task {
            let text =
                StudentTeacherPrompt.teacherQuestion qaPath question (lease.Origin = SatelliteOrigin.Replacement)

            let tools = toolMap Role.Teacher ProviderRequestKind.WorkMain
            let agent = teacherAgent cell

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

    let sendTeacherNudge (cell: StudentRunCell) teacher =
        task {
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
                        (teacherAgent cell)
                        (Some workspaceDirectory)
                        PromptDispatcher.AwaitMode.Detached
                        None
                        (toolMap Role.Teacher ProviderRequestKind.WorkMain)
        }

    let sendCompile (cell: StudentRunCell) isNudge =
        task {
            match qa.Path(cell.SessionId, cell.LogicalRunId), activeProfile cell.SessionId with
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
                        cell.SessionId
                        text
                        continuation
                        profile
                        cell.Agent
                        (Some workspaceDirectory)
                        PromptDispatcher.AwaitMode.Detached
                        None
                        (toolMap Role.Student ProviderRequestKind.StudentCompile)
        }

    let tryCell sessionId =
        lock gate (fun () ->
            match runs.TryGetValue(sessionKey sessionId) with
            | true, cell -> Some cell
            | false, _ -> None)

    let tryOwner teacher =
        lock gate (fun () ->
            match teacherOwners.TryGetValue(sessionKey teacher) with
            | true, owner -> Some(SessionId.create owner)
            | false, _ ->
                (AgentJournal.snapshot journal).AgentProjections.Associations
                |> SessionAssociationProjection.tryOwnerOf teacher)

    member _.ObserveChatMessage(message: PromptIngressCodec.DecodedMessage) : Result<unit, string> =
        match message.SessionId, message.PhysicalUserMessageId, message.PromptKey, message.Text with
        | Some sessionId, Some physical, None, Some text ->
            match activeProfile sessionId with
            | Some profile when
                profile.CanonicalRole = Role.Student
                && profile.AuthorityRootUserMessageId = PhysicalUserMessageId.promoteToAuthorityRoot physical
                ->
                let cell =
                    lock gate (fun () ->
                        match runs.TryGetValue(sessionKey sessionId) with
                        | true, existing when existing.LogicalRunId = profile.LogicalRunId -> existing
                        | _ ->
                            let created =
                                { SessionId = sessionId
                                  LogicalRunId = profile.LogicalRunId
                                  Agent = profile.SelectedAgent
                                  Tier = profile.SelectedTier
                                  State = StudentTeacher.RunState.LearnReady
                                  TeacherSessionId = restoredTeacher sessionId
                                  Waiter = None
                                  PendingFinal = None }

                            runs.[sessionKey sessionId] <- created
                            created)

                match qa.Read(cell.SessionId, cell.LogicalRunId) with
                | Error error -> Error error
                | Ok current when String.IsNullOrEmpty current ->
                    qa.Append(cell.SessionId, cell.LogicalRunId, text) |> Result.map ignore
                | Ok current when StudentTeacher.hasOpening current text -> Ok()
                | Ok _ -> Error "Student QA opening does not match the active HumanRoot"
            | _ -> Ok()
        | _ -> Ok()

    member _.InvokeTeacher(studentSessionId: string, question: string) : Task<Result<string, string>> =
        task {
            let student = SessionId.create studentSessionId

            match tryCell student with
            | None -> return Error "teacher rejected: no active Student run"
            | Some cell ->
                let claimed =
                    lock gate (fun () ->
                        if cell.State = StudentTeacher.RunState.LearnReady && cell.Waiter.IsNone then
                            cell.State <- StudentTeacher.RunState.TeacherWaiting
                            true
                        else
                            false)

                if not claimed then
                    return Error "teacher rejected: another Student operation is in flight"
                else
                    match qa.Append(cell.SessionId, cell.LogicalRunId, question) with
                    | Error error ->
                        lock gate (fun () -> cell.State <- StudentTeacher.RunState.LearnReady)
                        return Error error
                    | Ok qaPath ->
                        match! satellites.Ensure(cell.SessionId, teacherSpec cell) with
                        | Error error ->
                            satellites.Invalidate(cell.SessionId, SatelliteKind.Teacher)
                            lock gate (fun () -> cell.State <- StudentTeacher.RunState.LearnReady)
                            return Error error
                        | Ok lease ->
                            let waiter =
                                TaskCompletionSource<Result<string, string>>(
                                    TaskCreationOptions.RunContinuationsAsynchronously
                                )

                            lock gate (fun () ->
                                cell.TeacherSessionId <- Some lease.SessionId
                                teacherOwners.[sessionKey lease.SessionId] <- sessionKey cell.SessionId
                                cell.Waiter <- Some waiter)

                            onTeacherReady lease.SessionId (teacherAgent cell)

                            match! sendTeacherPrompt cell lease question qaPath with
                            | Error error ->
                                lock gate (fun () ->
                                    cell.Waiter <- None
                                    cell.State <- StudentTeacher.RunState.LearnReady)

                                return Error error
                            | Ok _ ->
                                match! waiter.Task with
                                | Error error -> return Error error
                                | Ok answer -> return Ok(StudentTeacherPrompt.teacherAnswerResult answer)
        }

    member _.Return
        (sessionKeyValue: string, providerRunId: ProviderRunIdentity option, message: string)
        : Task<Result<string, string>> =
        task {
            let sessionId = SessionId.create sessionKeyValue

            match activeProfile sessionId with
            | Some profile when profile.CanonicalRole = Role.Teacher ->
                match tryOwner sessionId |> Option.bind tryCell with
                | None -> return Error "return rejected: Teacher has no active Student owner"
                | Some cell ->
                    let waiter = lock gate (fun () -> cell.Waiter)

                    match waiter with
                    | None -> return Error "return rejected: no Student teacher call is waiting"
                    | Some pending ->
                        match qa.Append(cell.SessionId, cell.LogicalRunId, message) with
                        | Error error -> return Error error
                        | Ok _ ->
                            lock gate (fun () ->
                                cell.Waiter <- None
                                cell.State <- StudentTeacher.RunState.LearnReady
                                expectedTeacherAborts.Add(sessionKeyValue) |> ignore)

                            AsyncSupport.trySetResult pending (Ok message) |> ignore
                            sessions.AbortSession sessionId |> ignore
                            return Ok "OK"

            | Some profile when profile.CanonicalRole = Role.Student ->
                match tryCell sessionId, providerRunId with
                | Some cell, Some providerRun when
                    cell.State = StudentTeacher.RunState.CompileReady && cell.PendingFinal.IsNone
                    ->
                    match qa.Delete(cell.SessionId, cell.LogicalRunId) with
                    | Error error -> return Error error
                    | Ok() ->
                        lock gate (fun () -> cell.PendingFinal <- Some(providerRun, message))

                        return Ok(StudentTeacherPrompt.finalReturnResult message)
                | Some _, None -> return Error "return rejected: Host provided no provider-run identity"
                | Some _, Some _ -> return Error "return rejected: Student is not in StudentCompile"
                | None, _ -> return Error "return rejected: no active Student run"
            | _ -> return Error "return rejected: role is neither active Student nor Teacher"
        }

    member _.TextComplete(input: obj, output: obj) =
        if
            not (isNull input)
            && not (isNull input?sessionID)
            && not (isNull input?messageID)
        then
            let sessionId = SessionId.create (unbox<string> input?sessionID)

            match tryCell sessionId with
            | Some cell ->
                match lock gate (fun () -> cell.PendingFinal) with
                // The return tool context identifies the assistant message that
                // CALLED return. Host tool-loop continuation creates a new
                // assistant message for the following text completion, so its
                // messageID is necessarily different. Per-session execution is
                // serial and PendingFinal is armed only after QA deletion; the
                // next provider text completion is therefore the terminal slot.
                | Some(_, finalMessage) -> output?text <- finalMessage
                | _ -> ()
            | None -> ()

    member _.ValidateTool(input: obj, output: obj) : Result<unit, string> =
        if isNull input || isNull input?sessionID || isNull input?tool then
            Error "Student/Teacher tool gate received an incomplete Host context"
        else
            let sessionId = SessionId.create (unbox<string> input?sessionID)
            let tool = unbox<string> input?tool

            match activeProfile sessionId with
            | Some profile when profile.CanonicalRole = Role.Student ->
                match tryCell sessionId with
                | None -> Error "Student tool rejected: no active QA-backed run"
                | Some cell when cell.PendingFinal.IsSome ->
                    Error "Student tool rejected: final text completion is already expected"
                | Some cell ->
                    let requestKind =
                        match cell.State with
                        | StudentTeacher.RunState.LearnReady
                        | StudentTeacher.RunState.TeacherWaiting -> ProviderRequestKind.StudentLearn
                        | StudentTeacher.RunState.CompileDispatching
                        | StudentTeacher.RunState.CompileReady
                        | StudentTeacher.RunState.Closed -> ProviderRequestKind.StudentCompile

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
                            let target = StudentTeacherPath.resolve (workspaceDirectory, unbox<string> path)
                            let skillRoot = StudentTeacherPath.resolve (workspaceDirectory, ".agent/skills")
                            let relative = StudentTeacherPath.relative (skillRoot, target)

                            if
                                relative = ""
                                || (not (relative.StartsWith("..")))
                                   && not (StudentTeacherPath.isAbsolute relative)
                            then
                                Ok()
                            else
                                Error "StudentCompile write/edit is restricted to .agent/skills"
                    else
                        Ok()

            | Some profile when profile.CanonicalRole = Role.Teacher ->
                let allowed = Roles.permissions Role.Teacher |> Set.map StaticTools.toolName

                if Set.contains tool allowed then
                    Ok()
                else
                    Error(sprintf "Tool '%s' is outside the Teacher execution profile" tool)
            | _ -> Ok()

    member _.HandleTurn(turn: ReconciledTurn) : Task<bool> =
        task {
            match turn.Role with
            | Some Role.Student ->
                match tryCell turn.SessionId with
                | None -> return false
                | Some cell ->
                    match turn.Outcome, cell.PendingFinal, cell.State with
                    | ReconcileProgram.TurnCompleted, Some(_, expected), _ ->
                        let finalText = CompletedTurnClassifier.partsText turn.Parts

                        if finalText = expected then
                            let! _ = satellites.Retire(cell.SessionId, teacherSpec cell)
                            cell.State <- StudentTeacher.RunState.Closed
                            lock gate (fun () -> runs.Remove(sessionKey cell.SessionId) |> ignore)

                        return true
                    | ReconcileProgram.TurnCompleted, None, StudentTeacher.RunState.LearnReady ->
                        cell.State <- StudentTeacher.RunState.CompileDispatching

                        match! sendCompile cell false with
                        | Ok _ -> cell.State <- StudentTeacher.RunState.CompileReady
                        | Error _ -> cell.State <- StudentTeacher.RunState.LearnReady

                        return true
                    | ReconcileProgram.TurnCompleted, None, StudentTeacher.RunState.CompileReady ->
                        let! _ = sendCompile cell true
                        return true
                    | ReconcileProgram.TurnAborted _, _, _ ->
                        let! _ = satellites.Retire(cell.SessionId, teacherSpec cell)
                        qa.Delete(cell.SessionId, cell.LogicalRunId) |> ignore
                        lock gate (fun () -> runs.Remove(sessionKey cell.SessionId) |> ignore)
                        return true
                    | _ -> return true

            | Some Role.Teacher ->
                let expectedAbort =
                    lock gate (fun () -> expectedTeacherAborts.Remove(sessionKey turn.SessionId))

                if expectedAbort then
                    return true
                else
                    match tryOwner turn.SessionId |> Option.bind tryCell with
                    | None -> return true
                    | Some cell ->
                        match turn.Outcome, cell.Waiter with
                        | ReconcileProgram.TurnCompleted, Some _ ->
                            let! _ = sendTeacherNudge cell turn.SessionId
                            return true
                        | ReconcileProgram.TurnFailed error, Some waiter
                        | ReconcileProgram.TurnAborted error, Some waiter ->
                            AsyncSupport.trySetResult waiter (Error(sprintf "Teacher run failed: %s" error))
                            |> ignore

                            cell.Waiter <- None
                            cell.State <- StudentTeacher.RunState.LearnReady
                            return true
                        | _ -> return true
            | _ -> return false
        }

    member _.CancelSession(sessionId: SessionId) : Task<Result<unit, string>> =
        task {
            match tryCell sessionId with
            | None -> return Ok()
            | Some cell ->
                let waiter =
                    lock gate (fun () ->
                        let pending = cell.Waiter
                        cell.Waiter <- None
                        cell.State <- StudentTeacher.RunState.Closed
                        pending)

                waiter
                |> Option.iter (fun pending ->
                    AsyncSupport.trySetResult pending (Error "Student run was cancelled") |> ignore)

                let deleteResult = qa.Delete(cell.SessionId, cell.LogicalRunId)
                let! retireResult = satellites.Retire(cell.SessionId, teacherSpec cell)

                lock gate (fun () -> runs.Remove(sessionKey cell.SessionId) |> ignore)

                match deleteResult, retireResult with
                | Ok(), Ok() -> return Ok()
                | Error deleteError, Ok() -> return Error deleteError
                | Ok(), Error retireError -> return Error retireError
                | Error deleteError, Error retireError -> return Error(sprintf "%s; %s" deleteError retireError)
        }
