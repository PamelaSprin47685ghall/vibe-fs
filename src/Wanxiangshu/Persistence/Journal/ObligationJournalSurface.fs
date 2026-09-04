namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
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

    let snapshotMagicTodo (handle: JournalHandle) (incumbencyId: string) : obj =
        let projection = AgentJournal.snapshot handle.Journal

        MagicTodoProjectionSurface.incumbencyView
            projection.AgentProjections.MagicTodo
            (Wanxiangshu.Mission.Relay.IncumbencyId.create incumbencyId)

    let openIncumbency (handle: JournalHandle) (sessionId: string) (incumbencyId: string) : Task<obj> =
        task {
            let roadId = Wanxiangshu.Mission.Relay.RoadId.create sessionId
            let incId = Wanxiangshu.Mission.Relay.IncumbencyId.create incumbencyId
            let snapId = Wanxiangshu.Mission.Relay.WorkspaceSnapshotId.create "snapshot-root"
            let authRev = Wanxiangshu.Mission.Relay.AuthorityRevision.create "rev-1"
            let physUser = PhysicalUserMessageId.create "user-root"

            let events =
                [ Wanxiangshu.Mission.Relay.RelayEvent.RoadOpened(roadId, authRev, physUser)
                  Wanxiangshu.Mission.Relay.RelayEvent.IncumbencyOpened(
                      incId,
                      snapId,
                      Wanxiangshu.Mission.Relay.BatonSource.ExistingWorld
                  ) ]

            match Wanxiangshu.Mission.Relay.RelayTransaction.create events with
            | Error err -> return box {| ok = false; error = err |}
            | Ok tx ->
                let fact =
                    AgentFact.Relay(
                        Wanxiangshu.Mission.Relay.RelayFactCases.TransactionCommitted
                            {| RoadId = roadId; Transaction = tx |}
                    )

                let! result = AgentJournal.appendAgent (streamOfSession sessionId) None fact handle.Journal

                return
                    match result with
                    | Ok _ -> box {| ok = true |}
                    | Error failure ->
                        box
                            {| ok = false
                               error = JournalAppendFailure.describe failure |}
        }

    let grantWorkOwned (handle: JournalHandle) (sessionId: string) (incumbencyId: string) : Task<obj> =
        task {
            let roadId = Wanxiangshu.Mission.Relay.RoadId.create sessionId
            let incId = Wanxiangshu.Mission.Relay.IncumbencyId.create incumbencyId
            let snapId = Wanxiangshu.Mission.Relay.WorkspaceSnapshotId.create "snapshot-root"
            let authRev = Wanxiangshu.Mission.Relay.AuthorityRevision.create "rev-1"
            let physUser = PhysicalUserMessageId.create "user-root"
            let assessId = Wanxiangshu.Mission.Relay.AssessmentId.create ("assess-" + sessionId)

            let binding: Wanxiangshu.Mission.Relay.AssessmentBinding =
                { PhysicalUserMessageId = "user-root"
                  ProviderRunId = "run-test"
                  ToolCallId = "tool-test"
                  NarrativeDigest = "digest-narrative"
                  PayloadDigest = "digest-payload"
                  RootRequestDigest = "digest-root"
                  RequirementSetDigest = "digest-req"
                  EvidenceFrontierDigest = "digest-evidence" }

            let scores =
                Wanxiangshu.Mission.Relay.ScoreVector.tryCreate [ 10; 10; 10; 10; 10; 10; 10; 9 ]
                |> Result.defaultWith (fun _ -> failwith "scores")

            let events =
                [ Wanxiangshu.Mission.Relay.RelayEvent.RoadOpened(roadId, authRev, physUser)
                  Wanxiangshu.Mission.Relay.RelayEvent.IncumbencyOpened(
                      incId,
                      snapId,
                      Wanxiangshu.Mission.Relay.BatonSource.ExistingWorld
                  )
                  Wanxiangshu.Mission.Relay.RelayEvent.AssessmentCommitted(assessId, binding, snapId, authRev, scores) ]

            match Wanxiangshu.Mission.Relay.RelayTransaction.create events with
            | Error err -> return box {| ok = false; error = err |}
            | Ok tx ->
                let fact =
                    AgentFact.Relay(
                        Wanxiangshu.Mission.Relay.RelayFactCases.TransactionCommitted
                            {| RoadId = roadId; Transaction = tx |}
                    )

                let! result = AgentJournal.appendAgent (streamOfSession sessionId) None fact handle.Journal

                return
                    match result with
                    | Ok _ -> box {| ok = true |}
                    | Error failure ->
                        box
                            {| ok = false
                               error = JournalAppendFailure.describe failure |}
        }

    let appendManagerLifecycle (handle: JournalHandle) (sessionId: string) (action: string) (payload: obj) : Task<obj> =
        match action with
        | "LifeOpened" -> openIncumbency handle sessionId sessionId
        | "LifeCompleted" ->
            task {
                let roadId = Wanxiangshu.Mission.Relay.RoadId.create sessionId
                let incId = Wanxiangshu.Mission.Relay.IncumbencyId.create sessionId
                let retId = Wanxiangshu.Mission.Relay.RetirementId.create ("ret-" + sessionId)
                let snapId = Wanxiangshu.Mission.Relay.WorkspaceSnapshotId.create "snapshot-root"
                let batonId = Wanxiangshu.Mission.Relay.BatonId.create ("baton-" + sessionId)
                let cutId = Wanxiangshu.Mission.Relay.ProjectionCutId.create ("cut-" + sessionId)

                let envelope: Wanxiangshu.Mission.Relay.BatonEnvelope =
                    { SchemaVersion = 1
                      RoadId = Wanxiangshu.Mission.Relay.RoadId.value roadId
                      FromIncumbencyId = Wanxiangshu.Mission.Relay.IncumbencyId.value incId
                      AuthorityRevision = "rev-1"
                      SnapshotId = Wanxiangshu.Mission.Relay.WorkspaceSnapshotId.value snapId
                      OpenObligations = []
                      EvidenceRefs = [] }

                let cut: Wanxiangshu.Mission.Relay.ProjectionCut =
                    { RetiredIncumbencyId = Wanxiangshu.Mission.Relay.IncumbencyId.value incId
                      ThroughProviderRunId = "run-terminal"
                      ThroughToolCallId = "tool-terminal"
                      StaleProviderRunIds = [] }

                let summary: Wanxiangshu.Mission.Relay.RetirementSummary =
                    { Id = retId
                      IncumbencyId = incId
                      SnapshotId = snapId
                      BatonId = batonId
                      Baton = envelope
                      ProjectionCutId = cutId
                      ProjectionCut = cut
                      SuccessorRequested = false
                      QualityCandidateAccepted = true }

                let events = [ Wanxiangshu.Mission.Relay.RelayEvent.RetirementCommitted summary ]

                match Wanxiangshu.Mission.Relay.RelayTransaction.create events with
                | Error err -> return box {| ok = false; error = err |}
                | Ok tx ->
                    let fact =
                        AgentFact.Relay(
                            Wanxiangshu.Mission.Relay.RelayFactCases.TransactionCommitted
                                {| RoadId = roadId; Transaction = tx |}
                        )

                    let! result = AgentJournal.appendAgent (streamOfSession sessionId) None fact handle.Journal

                    return
                        match result with
                        | Ok _ -> box {| ok = true |}
                        | Error failure ->
                            box
                                {| ok = false
                                   error = JournalAppendFailure.describe failure |}
            }
        | _ -> Task.FromResult(box {| ok = true |})
