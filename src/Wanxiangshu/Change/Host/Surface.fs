namespace Wanxiangshu.Change.Host

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Change
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// JS-native owner for the OrchestratorHost semantic harness.
///
/// Host runtime state, ManagerPort, journal projection and typed ports remain
/// opaque. The harness supplies plain JavaScript port observations; this owner
/// translates them once into the real Host contracts and exposes only the
/// ManagerPort capability plus a child-presence observation.
[<RequireQualifiedAccess>]
module OrchestratorHostSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private field (value: obj) (name: string) : obj = jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawTask (value: obj) (name: string) (args: obj array) : Task<obj> = jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawDisposable (value: obj) (name: string) (args: obj array) : IDisposable = jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawUnit (value: obj) (name: string) (args: obj array) : Task = jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawValue (value: obj) (name: string) (args: obj array) : obj = jsNative

    [<Emit("typeof $0[$1] === 'function' ? Boolean($0[$1](...$2)) : false")>]
    let private invokeRawBoolOrFalse (value: obj) (name: string) (args: obj array) : bool = jsNative

    [<Emit("$0.managerPort")>]
    let private managerPortOf (host: OrchestratorHost) : obj = jsNative

    [<Emit("$0.gitPort = $1")>]
    let private replaceGitPort (host: OrchestratorHost) (port: GitPort) : unit = jsNative

    [<Emit("$0.runtime.children.has($1)")>]
    let private hasChildInRuntime (host: OrchestratorHost) (agentId: string) : bool = jsNative

    let private stringOf (value: obj) =
        if isNullish value then "" else string value

    let private boolOf (value: obj) =
        if isNullish value then false else unbox<bool> value

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private optionalString (value: obj) =
        if isNullish value then None else Some(stringOf value)

    let private plainResult (value: obj) (read: obj -> 'T) : Result<'T, string> =
        if isNullish value then
            Error "plain port returned no result"
        elif boolOf (field value "ok") then
            Ok(read (field value "value"))
        else
            Error(stringOf (field value "error"))

    let private plainUnitResult (value: obj) : Result<unit, string> = plainResult value (fun _ -> ())

    let private sessionValue (value: obj) = SessionId.create (stringOf value)
    let private targetValue (value: obj) = TargetRef.create (stringOf value)
    let private worktreePathValue (value: obj) = WorktreePath.create (stringOf value)

    let private worktreeIdentityValue (value: obj) =
        WorktreeIdentity.create (stringOf value)

    let private commitValue (value: obj) = CommitHash.create (stringOf value)

    let private roleOf (value: obj) : Role option =
        match stringOf value with
        | "Manager" -> Some Role.Manager
        | "Orchestrator" -> Some Role.Orchestrator
        | "Coder" -> Some Role.Coder
        | "Inspector" -> Some Role.Inspector
        | "Browser" -> Some Role.Browser
        | "Inquiry" -> Some Role.Inquiry
        | "DevOps" -> Some Role.DevOps
        | "Distiller" -> Some Role.Distiller
        | "Blogger" -> Some Role.Blogger
        | "Reviewer" -> Some Role.Reviewer
        | _ -> None

    let private terminalOutcome (value: obj) : TerminalOutcome =
        match stringOf (field value "kind") with
        | "Completed" ->
            let sessionId = sessionValue (field value "sessionId")

            match roleOf (field value "role") with
            | None -> TerminalOutcome.Failed "invalid role"
            | Some role ->
                TerminalOutcome.Completed
                    { SessionId = sessionId
                      AuthorityRootUserMessageId =
                        AuthorityRootUserMessageId.create (stringOf (field value "authorityRoot"))
                      ProviderRun = ProviderRunIdentity.create (stringOf (field value "providerRun"))
                      Role = role
                      Directory = optionalString (field value "directory")
                      TerminalText = stringOf (field value "terminalText")
                      TurnFormalText = stringOf (field value "turnFormalText") }
        | "Aborted" -> TerminalOutcome.Aborted(stringOf (field value "reason"))
        | _ -> TerminalOutcome.Failed(stringOf (field value "error"))

    let private sendOutcome (value: obj) : Outcome.SendOutcome =
        match stringOf (field value "kind") with
        | "Receipt" -> Outcome.SendOutcome.AdmittedWithReceipt(TransportReceipt.create (stringOf (field value "value")))
        | "Physical" ->
            Outcome.SendOutcome.AdmittedWithPhysicalMessage(
                PhysicalUserMessageId.create (stringOf (field value "value"))
            )
        | "Retryable" -> Outcome.SendOutcome.Retryable(stringOf (field value "reason"))
        | "Unknown" -> Outcome.SendOutcome.AcceptanceUnknown(stringOf (field value "reason"))
        | _ -> Outcome.SendOutcome.Fatal(stringOf (field value "reason"))

    let private childInfo (value: obj) : OpenCodeChildInfo =
        { SessionId = sessionValue (field value "sessionId")
          ParentSessionId = optionalString (field value "parentSessionId") |> Option.map SessionId.create
          Agent = optionalString (field value "agent")
          Title = optionalString (field value "title") }

    type private PlainSessionPort(raw: obj) =
        interface ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) =
                let callback =
                    fun rawSession rawOutcome -> listener (sessionValue rawSession) (terminalOutcome rawOutcome)

                invokeRawDisposable raw "SubscribeTerminal" [| box (SessionId.value sessionId); box callback |]

            member _.SubscribeFutureTerminal(sessionId, listener) =
                let callback =
                    fun rawSession rawOutcome -> listener (sessionValue rawSession) (terminalOutcome rawOutcome)

                invokeRawDisposable raw "SubscribeFutureTerminal" [| box (SessionId.value sessionId); box callback |]

            member _.SendPrompt(sessionId, text, options) =
                task {
                    let! value =
                        invokeRawTask raw "SendPrompt" [| box (SessionId.value sessionId); box text; box options |]

                    return sendOutcome value
                }

            member _.AbortSession(sessionId) =
                task {
                    let! value = invokeRawTask raw "AbortSession" [| box (SessionId.value sessionId) |]
                    return plainUnitResult value
                }

            member _.InterruptAttempt(sessionId) =
                task {
                    let! value = invokeRawTask raw "InterruptAttempt" [| box (SessionId.value sessionId) |]
                    return plainUnitResult value
                }

            member _.IsManagedChild(sessionId) =
                invokeRawBoolOrFalse raw "IsManagedChild" [| box (SessionId.value sessionId) |]

            member _.AbortChildren(sessionId) =
                invokeRawUnit raw "AbortChildren" [| box (SessionId.value sessionId) |]

            member _.CreateSiblingSession(owner, parent, options) =
                task {
                    let! value =
                        invokeRawTask
                            raw
                            "CreateSiblingSession"
                            [| box (SessionId.value owner)
                               box (parent |> Option.map SessionId.value |> Option.toObj)
                               box options |]

                    return plainResult value sessionValue
                }

            member _.TryGetParentSession(sessionId) =
                task {
                    let! value = invokeRawTask raw "TryGetParentSession" [| box (SessionId.value sessionId) |]
                    return plainResult value (fun item -> optionalString item |> Option.map SessionId.create)
                }

            member _.CreateChildSession(parent, options) =
                task {
                    let! value = invokeRawTask raw "CreateChildSession" [| box (SessionId.value parent); box options |]

                    return plainResult value sessionValue
                }

            member _.ListChildren(parent) =
                task {
                    let! value = invokeRawTask raw "ListChildren" [| box (SessionId.value parent) |]
                    return plainResult value (fun item -> arrayOf item |> Array.toList |> List.map childInfo)
                }

            member _.FamilyRootOf(sessionId) =
                sessionValue (invokeRawValue raw "FamilyRootOf" [| box (SessionId.value sessionId) |])

    let private sessionPort (raw: obj) : ISessionHostPort =
        PlainSessionPort(raw) :> ISessionHostPort

    let private worktreePair (value: obj) =
        let identity =
            field value "identity" |> optionalString |> Option.map WorktreeIdentity.create

        worktreePathValue (field value "path"), identity

    let private gitPort (raw: obj) : GitPort =
        { IsDirty =
            fun path ->
                task {
                    let! value = invokeRawTask raw "IsDirty" [| box (WorktreePath.value path) |]
                    return boolOf value
                }
          CreateWorktree =
            fun job path ->
                task {
                    let! value =
                        invokeRawTask
                            raw
                            "CreateWorktree"
                            [| box (ManagerJobId.value job); box (WorktreePath.value path) |]

                    return plainResult value worktreeIdentityValue
                }
          FreezeTargetBranch =
            fun () ->
                task {
                    let! value = invokeRawTask raw "FreezeTargetBranch" [||]
                    return plainResult value targetValue
                }
          Rebase =
            fun path target ->
                task {
                    let! value =
                        invokeRawTask raw "Rebase" [| box (WorktreePath.value path); box (TargetRef.value target) |]

                    return plainUnitResult value
                }
          FfMerge =
            fun path target expected ->
                task {
                    let! value =
                        invokeRawTask
                            raw
                            "FfMerge"
                            [| box (WorktreePath.value path)
                               box (TargetRef.value target)
                               box (CommitHash.value expected) |]

                    return plainResult value commitValue
                }
          ConflictedFiles =
            fun path ->
                task {
                    let! value = invokeRawTask raw "ConflictedFiles" [| box (WorktreePath.value path) |]
                    return plainResult value (fun item -> arrayOf item |> Array.toList |> List.map stringOf)
                }
          RemoveWorktree =
            fun path ->
                task {
                    let! value = invokeRawTask raw "RemoveWorktree" [| box (WorktreePath.value path) |]
                    return plainUnitResult value
                }
          HasRebaseHead =
            fun path ->
                task {
                    let! value = invokeRawTask raw "HasRebaseHead" [| box (WorktreePath.value path) |]
                    return boolOf value
                }
          ListWorktrees =
            fun () ->
                task {
                    let! value = invokeRawTask raw "ListWorktrees" [||]
                    return plainResult value (fun item -> arrayOf item |> Array.toList |> List.map worktreePair)
                }
          ListManagerBranches =
            fun () ->
                task {
                    let! value = invokeRawTask raw "ListManagerBranches" [||]

                    return
                        plainResult value (fun item -> arrayOf item |> Array.toList |> List.map worktreeIdentityValue)
                }
          DeleteBranch =
            fun identity ->
                task {
                    let! value = invokeRawTask raw "DeleteBranch" [| box (WorktreeIdentity.value identity) |]
                    return plainUnitResult value
                }
          ReadHead =
            fun path ->
                task {
                    let! value = invokeRawTask raw "ReadHead" [| box (WorktreePath.value path) |]
                    return plainResult value commitValue
                }
          GetTargetHead =
            fun target ->
                task {
                    let! value = invokeRawTask raw "GetTargetHead" [| box (TargetRef.value target) |]
                    return plainResult value commitValue
                } }

    type private HostHandle(host: OrchestratorHost, manager: obj) =
        member _.Host = host
        member _.Manager = manager

    let private journalOf (value: obj) : AgentJournal option =
        if isNullish value then
            None
        else
            Some((unbox<JournalHandle> value).Journal)

    /// Build a real OrchestratorHost from plain JavaScript port contracts.
    /// `sessions`, `gitPort`, and `journal` are capabilities owned by the caller;
    /// this function never projects their internal representation.
    let create (options: obj) : obj =
        let sessions = sessionPort (field options "sessions")
        let journal = journalOf (field options "journal")

        let deps: OrchestratorHostDeps =
            { Sessions = sessions
              Journal = journal
              SessionSnapshot = None
              OnChildCreated = fun _ _ _ -> ()
              RegisterChildDirectory = fun _ _ -> ()
              RegisterReviewerTree = fun _ _ -> ()
              OnRunStarted = fun _ _ _ -> ()
              RepoPath = stringOf (field options "repoPath")
              TargetBranch = stringOf (field options "targetBranch")
              ParentWorkRecordFor = fun _ -> Task.FromResult None
              ChildWorkRecordFor = fun _ -> Task.FromResult None }

        let host =
            OrchestratorHost(deps, SessionId.create (stringOf (field options "orchestratorId")))

        let rawGit = field options "gitPort"

        if not (isNullish rawGit) then
            replaceGitPort host (gitPort rawGit)

        HostHandle(host, managerPortOf host) :> obj

    let managerPort (handle: obj) : obj = (handle :?> HostHandle).Manager

    let hasChild (handle: obj) (agentId: string) : bool =
        hasChildInRuntime (handle :?> HostHandle).Host agentId
