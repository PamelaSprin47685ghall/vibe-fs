namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

open Wanxiangshu.Foundation

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
            let physUser = Wanxiangshu.Mission.Relay.PhysicalUserMessageId.create "user-root"

            let events =
                [ Wanxiangshu.Mission.Relay.RelayEvent.RoadOpened(roadId, authRev, physUser)
                  Wanxiangshu.Mission.Relay.RelayEvent.IncumbencyOpened(incId, snapId) ]

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
            let physUser = Wanxiangshu.Mission.Relay.PhysicalUserMessageId.create "user-root"
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
                Wanxiangshu.Mission.Relay.ScoreVector.tryCreate
                    [ Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                      Wanxiangshu.Mission.Relay.ScoreGrade.Revise ]
                |> Result.defaultWith (fun _ -> failwith "scores")

            let events =
                [ Wanxiangshu.Mission.Relay.RelayEvent.RoadOpened(roadId, authRev, physUser)
                  Wanxiangshu.Mission.Relay.RelayEvent.IncumbencyOpened(incId, snapId)
                  Wanxiangshu.Mission.Relay.RelayEvent.AssessmentCommitted(
                      assessId,
                      incId,
                      binding,
                      snapId,
                      authRev,
                      scores
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

    let appendManagerLifecycle (handle: JournalHandle) (sessionId: string) (action: string) (payload: obj) : Task<obj> =
        match action with
        | "LifeOpened" -> openIncumbency handle sessionId sessionId
        | "LifeCompleted" ->
            task {
                let roadId = Wanxiangshu.Mission.Relay.RoadId.create sessionId
                let incId = Wanxiangshu.Mission.Relay.IncumbencyId.create sessionId
                let snapId = Wanxiangshu.Mission.Relay.WorkspaceSnapshotId.create "snapshot-root"
                let authRev = Wanxiangshu.Mission.Relay.AuthorityRevision.create "rev-1"

                let assessId =
                    Wanxiangshu.Mission.Relay.AssessmentId.create ("assess-terminal-" + sessionId)

                let retId = Wanxiangshu.Mission.Relay.RetirementId.create ("ret-" + sessionId)

                let binding: Wanxiangshu.Mission.Relay.AssessmentBinding =
                    { PhysicalUserMessageId = "user-root"
                      ProviderRunId = "run-terminal"
                      ToolCallId = "tool-terminal"
                      NarrativeDigest = "digest-narrative"
                      PayloadDigest = "digest-payload"
                      RootRequestDigest = "digest-root"
                      RequirementSetDigest = "digest-req"
                      EvidenceFrontierDigest = "digest-evidence" }

                let scores =
                    Wanxiangshu.Mission.Relay.ScoreVector.tryCreate (
                        List.replicate 8 Wanxiangshu.Mission.Relay.ScoreGrade.Perfect
                    )
                    |> Result.defaultWith (fun _ -> failwith "scores")

                let certificateId =
                    Wanxiangshu.Mission.Relay.QualityCertificateId.create (
                        "certificate:" + Wanxiangshu.Mission.Relay.AssessmentId.value assessId
                    )

                let cut: Wanxiangshu.Mission.Relay.ProjectionCut =
                    { ProviderRunId = "run-terminal"
                      ToolCallId = "tool-terminal" }

                let summary: Wanxiangshu.Mission.Relay.RetirementSummary =
                    { Id = retId
                      IncumbencyId = incId
                      SnapshotId = snapId
                      AuthorityRevision = authRev
                      ProjectionCut = cut
                      Outcome = Wanxiangshu.Mission.Relay.RetirementOutcome.Accepted certificateId }

                let events =
                    [ Wanxiangshu.Mission.Relay.RelayEvent.AssessmentCommitted(
                          assessId,
                          incId,
                          binding,
                          snapId,
                          authRev,
                          scores
                      )
                      Wanxiangshu.Mission.Relay.RelayEvent.RetirementCommitted summary ]

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
