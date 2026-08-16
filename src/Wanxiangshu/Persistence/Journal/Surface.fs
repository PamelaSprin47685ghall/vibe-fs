namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.OpenCode
open Wanxiangshu.OpenCode.Host

/// Opaque capability for one journal projection and its local writer.
type JournalHandle private (journal: AgentJournal, release: unit -> unit) =
    let mutable disposed = false

    member internal _.Journal =
        if disposed then
            invalidOp "Journal handle is disposed"

        journal

    member internal _.Dispose() =
        if not disposed then
            disposed <- true
            release ()

    static member internal Create(journal: AgentJournal) =
        JournalHandle(journal, fun () -> (journal :> IDisposable).Dispose())

    static member internal CreateShared(journal: AgentJournal) =
        JournalHandle(journal, fun () -> SharedAgentJournal.release (Some journal))
[<RequireQualifiedAccess>]
module JournalSurface =

    let private str (value: obj) : string =
        if isNull value then "" else string value

    let private sessionIdOf (value: obj) : SessionId = SessionId.create (str value)

    let private providerRunOf (value: obj) : ProviderRunIdentity option =
        if isNull value then None else Some(ProviderRunIdentity.create (str value))

    let private blobDigest (value: obj) : BlobDigest = BlobDigest.create (str value)
    let private blobRef (value: obj) : BlobRef = BlobRef.create (str value)

    let private agentFactOfJs (value: obj) : AgentFact =
        let family = str (value?family)
        let case = str (value?case)
        let payload = unbox<obj> (value?payload)

        match family, case with
        | "Companion", "CompanionBloggerClosed" ->
            CompanionFact.CompanionBloggerClosed {| SessionId = sessionIdOf (payload?SessionId) |}
        | _ -> failwith $"JournalSurface: unknown AgentFact {family}.{case}"

    let private managerLifecycleOfJs (value: obj) : ManagerLifecycleFact =
        let case = str (value?case)
        let payload = unbox<obj> (value?payload)

        match case with
        | "LifeOpened" ->
            ManagerLifecycleFact.LifeOpened
                {| SessionId = sessionIdOf (payload?SessionId)
                   LifeId = ManagerLifeId.create (str (payload?LifeId))
                   OpeningUserMessageId = PhysicalUserMessageId.create (str (payload?OpeningUserMessageId))
                   OpeningTextRef = blobRef (payload?OpeningTextRef)
                   OpeningTextDigest = blobDigest (payload?OpeningTextDigest)
                   OpeningCursorSequence = unbox<int64> (payload?OpeningCursorSequence) |}
        | other -> failwith $"JournalSurface: unknown ManagerLifecycle case '{other}'"

    let private streamOfJs (value: obj) : StreamId =
        match str (value?kind) with
        | "Session" -> StreamId.Session(sessionIdOf (value?session))
        | other -> failwith $"JournalSurface: unknown stream kind '{other}'"

    let private projectionToJs (projection: ProjectionSet) : obj =
        let sessions =
            projection.AgentProjections.Sessions
            |> Map.toList
            |> List.map (fun (sessionId, _) -> SessionId.value sessionId)
            |> List.toArray

        box {| sessions = sessions |}

    let private journalOrError (writer: IJournalWriter) (init: Envelope) (projection: ProjectionSet) : obj =
        match AgentJournal.createFromProjection writer projection with
        | Ok journal ->
            box
                {| ok = true
                   journal = JournalHandle.Create(journal)
                   localSeq = LocalSeq.value init.LocalSeq |}
        | Error error -> box {| ok = false; error = $"{error.Fact}: {error.Reason}" |}

    let private bootResult (result: Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>) : obj =
        match result with
        | Ok(writer, init, projection) -> journalOrError writer init projection
        | Error error -> box {| ok = false; error = $"{error.Fact}: {error.Reason}" |}

    /// Acquire the plugin's process-local workspace journal through the same
    /// runtime-path owner as the composition root. The returned capability is
    /// ref-counted and must be released with `dispose`; no journal internals
    /// cross this boundary.
    let acquireSharedForWorkspace
        (workspace: string)
        (processId: int)
        (startedAt: string)
        : Task<obj> =
        task {
            let commonDirectory = RuntimePath.gitCommonDir workspace
            let runtimeDirectory = RuntimePath.forWorkspace workspace
            let boot = WorkspaceEventStore.bootPort commonDirectory

            let openJournal runtimeId processIdValue processStartedAt =
                task {
                    let! result = boot.ResumeOrCreate(runtimeId, processIdValue, processStartedAt)

                    match result with
                    | Ok(writer, _, projection) -> return AgentJournal.createFromProjection writer projection
                    | Error error -> return Error error
                }

            let! result =
                SharedAgentJournal.acquire
                    runtimeDirectory
                    processId
                    (DateTimeOffset.Parse startedAt)
                    openJournal

            match result with
            | Ok journal -> return box {| ok = true; journal = JournalHandle.CreateShared journal |}
            | Error error -> return box {| ok = false; error = $"{error.Fact}: {error.Reason}" |}
        }

    /// Open a workspace journal directly. The returned journal is an opaque
    /// capability and must be released with `dispose`.
    let bootWithWriterId
        (commonDir: string)
        (writerId: string)
        (runtimeId: string)
        (processId: int)
        (startedAt: string)
        : Task<obj> =
        task {
            let integrator = CanonicalIntegrator.create ()
            let store = EventStore.createLocal (str commonDir) (str writerId) integrator

            let! result =
                EventStoreJournalWriter.resumeOrCreate (
                    RuntimeId.create (str runtimeId),
                    processId,
                    DateTimeOffset.Parse(str startedAt),
                    store
                )

            return bootResult result
        }

    /// Open a journal with an anonymous process writer.
    let boot (commonDir: string) (runtimeId: string) (processId: int) (startedAt: string) : Task<obj> =
        bootWithWriterId commonDir (Guid.NewGuid().ToString("N")) runtimeId processId startedAt

    /// Release a journal capability and reject future operations on it.
    let dispose (handle: JournalHandle) : unit = handle.Dispose()

    /// Runtime identity is a plain diagnostic value; the journal remains opaque.
    let runtimeId (handle: JournalHandle) : string =
        AgentJournal.runtimeId handle.Journal |> RuntimeId.value

    /// Append an agent fact and return a normalized projection summary.
    let appendAgent (handle: JournalHandle) (stream: obj) (run: obj) (fact: obj) : Task<obj> =
        task {
            let! result = AgentJournal.appendAgent (streamOfJs stream) (providerRunOf run) (agentFactOfJs fact) handle.Journal

            return
                match result with
                | Ok projection -> box {| ok = true; projection = projectionToJs projection |}
                | Error error -> box {| ok = false; error = error.ToString() |}
        }

    /// Prove and durably record one terminal completion through the HandleController.
    /// The JournalHandle and JoinableCompletion remain opaque; callers provide only
    /// parent/agent/child identities, finality and body text.
    let recordTerminalCompletion
        (handle: JournalHandle)
        (parentId: string)
        (agentId: string)
        (childSessionId: string)
        (status: string)
        (body: string)
        : Task<obj> =
        task {
            let handleId = HandleController.agentHandle (str agentId)
            let evidence =
                if status.Equals("failed", StringComparison.OrdinalIgnoreCase) then
                    TerminalEvidence.failed (str agentId) handleId (SessionId.create (str childSessionId)) (str body)
                else
                    TerminalEvidence.completed (str agentId) handleId (SessionId.create (str childSessionId)) (str body)

            match JoinableCompletion.tryFromProvenTerminal evidence with
            | Error error -> return box {| ok = false; error = error.ToString() |}
            | Ok completion ->
                let! result =
                    HandleController.recordCompletion
                        (Some handle.Journal)
                        (SessionId.create (str parentId))
                        completion

                match result with
                | Ok() ->
                    return
                        box
                            {| ok = true
                               finality = JoinableCompletion.kind completion |> string
                               body = JoinableCompletion.body completion |}
                | Error error -> return box {| ok = false; error = error.ToString() |}
        }

    let appendManagerLifecycle (handle: JournalHandle) (stream: obj) (fact: obj) : Task<obj> =
        task {
            let! result = AgentJournal.appendManagerLifecycle (streamOfJs stream) (managerLifecycleOfJs fact) handle.Journal

            return
                match result with
                | Ok projection -> box {| ok = true; projection = projectionToJs projection |}
                | Error error -> box {| ok = false; error = error.ToString() |}
        }

    /// Write a UTF-8 payload through the journal's local payload owner.
    let writePayload (handle: JournalHandle) (content: string) : Task<obj> =
        task {
            let! result = handle.Journal.Writer.BlobWriter.Write(str content)

            return
                match result with
                | Ok receipt ->
                    box
                        {| ok = true
                           blobRef = BlobRef.value receipt.BlobRef
                           blobDigest = BlobDigest.value receipt.BlobDigest |}
                | Error error -> box {| ok = false; error = error |}
        }

    /// Read a payload by its opaque `blobs/<digest>` reference.
    let readPayload (handle: JournalHandle) (reference: string) : Task<obj> =
        task {
            let! result = handle.Journal.Writer.BlobWriter.Read(BlobRef.create (str reference))

            return
                match result with
                | Ok text -> box {| ok = true; content = text |}
                | Error error -> box {| ok = false; error = error |}
        }

    /// Current projection summary; no F# record crosses the boundary.
    let snapshot (handle: JournalHandle) : obj =
        AgentJournal.snapshot handle.Journal |> projectionToJs

    /// Keyed session lookup over the current projection.
    let hasSession (handle: JournalHandle) (sessionId: string) : bool =
        AgentProjection.tryFind (SessionId.create (str sessionId)) (AgentJournal.snapshot handle.Journal).AgentProjections
        |> Option.isSome
