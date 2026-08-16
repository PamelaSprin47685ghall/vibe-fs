namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.EventStore

/// JS-native semantic surface for workspace journal lifecycle and append.
[<RequireQualifiedAccess>]
module JournalSurface =

    let private str (value: obj) : string =
        if isNull value then "" else string value

    let gitCommonDir (workspace: string) : string =
        RuntimePath.gitCommonDir (str workspace)

    let runtimeDirectory (workspace: string) : string =
        RuntimePath.forWorkspace (str workspace)

    let private sessionIdOf (value: obj) : SessionId = SessionId.create (str value)

    let private providerRunOf (value: obj) : ProviderRunIdentity option =
        if isNull value then
            None
        else
            Some(ProviderRunIdentity.create (str value))

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

    let private journalResultToJs (result: Result<AgentJournal, FoldRejection>) : obj =
        match result with
        | Ok journal -> box {| ok = true; journal = journal |}
        | Error e ->
            box
                {| ok = false
                   error = $"{e.Fact}: {e.Reason}" |}

    let private journalOrError (writer: IJournalWriter) (init: Envelope) (projection: ProjectionSet) : obj =
        match AgentJournal.createFromProjection writer projection with
        | Ok journal ->
            box
                {| ok = true
                   journal = journal
                   localSeq = LocalSeq.value init.LocalSeq
                   filePath = writer.FilePath
                   release = fun () -> (journal :> IDisposable).Dispose() |}
        | Error e ->
            box
                {| ok = false
                   error = $"{e.Fact}: {e.Reason}" |}

    let private bootResult (result: Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>) : obj =
        match result with
        | Ok(writer, init, projection) -> journalOrError writer init projection
        | Error e ->
            box
                {| ok = false
                   error = $"{e.Fact}: {e.Reason}" |}

    /// Open a workspace journal directly. Returns `{ ok, journal, filePath }`.
    /// `writerId` is used for the underlying NDJSON file name.
    let bootWithWriterId
        (commonDir: string)
        (writerId: string)
        (runtimeId: string)
        (processId: int)
        (startedAt: string)
        : Task<obj> =
        task {
            let cd = str commonDir
            let integrator = CanonicalIntegrator.create ()
            let store = EventStore.createLocal cd (str writerId) integrator

            let! result =
                EventStoreJournalWriter.resumeOrCreate (
                    RuntimeId.create (str runtimeId),
                    processId,
                    DateTimeOffset.Parse(str startedAt),
                    store
                )

            return bootResult result
        }

    /// Open a workspace journal with an anonymous writer. Returns `{ ok, journal }`.
    let boot (commonDir: string) (runtimeId: string) (processId: int) (startedAt: string) : Task<obj> =
        let writerId = System.Guid.NewGuid().ToString("N")
        bootWithWriterId commonDir writerId runtimeId processId startedAt

    /// Append an agent fact and return the updated projection.
    let appendAgent (journal: AgentJournal) (stream: obj) (run: obj) (fact: obj) : Task<obj> =
        task {
            let! result = AgentJournal.appendAgent (streamOfJs stream) (providerRunOf run) (agentFactOfJs fact) journal

            return
                match result with
                | Ok projection -> box {| ok = true; projection = projection |}
                | Error e -> box {| ok = false; error = e.ToString() |}
        }

    /// Append a manager lifecycle fact and return the updated projection.
    let appendManagerLifecycle (journal: AgentJournal) (stream: obj) (fact: obj) : Task<obj> =
        task {
            let! result = AgentJournal.appendManagerLifecycle (streamOfJs stream) (managerLifecycleOfJs fact) journal

            return
                match result with
                | Ok projection -> box {| ok = true; projection = projection |}
                | Error e -> box {| ok = false; error = e.ToString() |}
        }

    /// Write a payload and return the receipt. `content` is a UTF-8 string.
    let writePayload (journal: AgentJournal) (content: string) : Task<obj> =
        task {
            let! result = journal.Writer.BlobWriter.Write(str content)

            return
                match result with
                | Ok receipt ->
                    box
                        {| ok = true
                           blobRef = BlobRef.value receipt.BlobRef
                           blobDigest = BlobDigest.value receipt.BlobDigest |}
                | Error e -> box {| ok = false; error = e |}
        }

    /// Read a blob by its `blobs/<digest>` ref.
    let readPayload (journal: AgentJournal) (ref: string) : Task<obj> =
        task {
            let! result = journal.Writer.BlobWriter.Read(BlobRef.create (str ref))

            return
                match result with
                | Ok text -> box {| ok = true; content = text |}
                | Error e -> box {| ok = false; error = e |}
        }

    /// Current projection snapshot.
    let snapshot (journal: AgentJournal) : obj = AgentJournal.snapshot journal

    /// True if the projection contains a session.
    let hasSession (journal: AgentJournal) (sessionId: string) : bool =
        let projection = AgentJournal.snapshot journal

        AgentProjection.tryFind (SessionId.create (str sessionId)) projection.AgentProjections
        |> Option.isSome
