namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

/// Journal owner operations specific to the obligation ledger. The generic
/// JournalSurface owns boot/release; this module owns MagicTodo facts and the
/// compact projections that prove their durability.
[<RequireQualifiedAccess>]
module ObligationJournalSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private streamOfSession (sessionId: string) =
        StreamId.Session(SessionId.create sessionId)

    let private runOf (value: obj) =
        if isNull value then
            None
        else
            Some(ProviderRunIdentity.create (text value))

    let private appendResult result =
        match result with
        | Ok receipt ->
            box
                {| ok = true
                   eventId = EventId.value receipt.EventId |}
        | Error failure ->
            box
                {| ok = false
                   error = JournalAppendFailure.describe failure |}

    let appendMagicTodo (handle: JournalHandle) (sessionId: string) (providerRun: obj) (factJson: string) : Task<obj> =
        task {
            match MagicTodoFactCodec.tryDecode factJson with
            | Error error -> return box {| ok = false; error = error |}
            | Ok fact ->
                let! result =
                    AgentJournal.appendMagicTodo (streamOfSession sessionId) (runOf providerRun) fact handle.Journal

                return appendResult result
        }

    let writePayload (handle: JournalHandle) (content: string) : Task<obj> =
        task {
            let! result = handle.Journal.Writer.BlobWriter.Write content

            return
                match result with
                | Ok receipt ->
                    box
                        {| ok = true
                           blobRef = BlobRef.value receipt.BlobRef
                           blobDigest = BlobDigest.value receipt.BlobDigest |}
                | Error error -> box {| ok = false; error = error |}
        }

    let snapshotMagicTodo (handle: JournalHandle) (lifeId: string) : obj =
        let projection = AgentJournal.snapshot handle.Journal
        MagicTodoProjectionSurface.lifeView projection.AgentProjections.MagicTodo (ManagerLifeId.create lifeId)

    let private managerLifecycleFactOf (caseName: string) (payload: obj) : ManagerLifecycleFact =
        let sessionId = SessionId.create (text (payload?sessionId))
        let lifeId = ManagerLifeId.create (text (payload?lifeId))

        match caseName with
        | "LifeOpened" ->
            ManagerLifecycleFact.LifeOpened
                {| SessionId = sessionId
                   LifeId = lifeId
                   OpeningUserMessageId = PhysicalUserMessageId.create (text (payload?openingUserMessageId))
                   OpeningTextRef = BlobRef.create (text (payload?openingTextRef))
                   OpeningTextDigest = BlobDigest.create (text (payload?openingTextDigest))
                   OpeningCursorSequence = int64 (unbox<int> (payload?openingCursorSequence)) |}
        | "WorkActivated" ->
            ManagerLifecycleFact.WorkActivated
                {| SessionId = sessionId
                   LifeId = lifeId
                   ActivationPromptKey = PromptKey.create (text (payload?activationPromptKey))
                   ProtectedPrefixEndSequence = int64 (unbox<int> (payload?protectedPrefixEndSequence)) |}
        | other -> failwith $"ObligationJournalSurface: unknown lifecycle case '{other}'"

    let appendManagerLifecycle
        (handle: JournalHandle)
        (sessionId: string)
        (caseName: string)
        (payload: obj)
        : Task<obj> =
        task {
            let fact = managerLifecycleFactOf caseName payload
            let! result = AgentJournal.appendManagerLifecycle (streamOfSession sessionId) fact handle.Journal

            return
                match result with
                | Ok _ -> box {| ok = true |}
                | Error error ->
                    box
                        {| ok = false
                           error = JournalAppendFailure.describe error |}
        }
