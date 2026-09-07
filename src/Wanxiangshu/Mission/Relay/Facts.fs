namespace Wanxiangshu.Mission.Relay

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type RelayEvent =
    | RoadOpened of RoadId * AuthorityRevision * PhysicalUserMessageId
    | IncumbencyOpened of IncumbencyId * WorkspaceSnapshotId
    | AssessmentCommitted of
        AssessmentId *
        IncumbencyId *
        AssessmentBinding *
        WorkspaceSnapshotId *
        AuthorityRevision *
        ScoreVector
    | AuthorityRevisionAdvanced of
        IncumbencyId *
        expected: AuthorityRevision *
        next: AuthorityRevision *
        PhysicalUserMessageId *
        WorkspaceSnapshotId
    | QualityCertificateInvalidated of QualityCertificateId * reason: string
    | RetirementCleanupBlocked of IncumbencyId * blockerDigest: string
    | RetirementCommitted of RetirementSummary

type RelayTransaction = private RelayTransaction of RelayEvent list

module RelayTransaction =
    let create events =
        match events with
        | [] -> Error "Relay transaction must contain at least one event."
        | _ -> Ok(RelayTransaction events)

    let events (RelayTransaction events) = events

type IncumbencyOpening =
    { RoadId: RoadId
      IncumbencyId: IncumbencyId
      AuthorityRevision: AuthorityRevision
      Transaction: RelayTransaction }

module IncumbencyOpening =
    let private buildTransaction events =
        match RelayTransaction.create events with
        | Ok transaction -> transaction
        | Error error -> failwith error

    let initial
        (sha256: string -> string)
        (sessionId: SessionId)
        (physicalUserMessageId: PhysicalUserMessageId)
        (snapshotId: WorkspaceSnapshotId)
        =
        let roadId = RoadId.create (SessionId.value sessionId)

        let authorityRevision =
            AuthorityRevision.create (PhysicalUserMessageId.value physicalUserMessageId)

        let incumbencyId =
            sha256 (
                "incumbency-v1\n"
                + SessionId.value sessionId
                + "\n"
                + PhysicalUserMessageId.value physicalUserMessageId
            )
            |> fun digest -> IncumbencyId.create ("incumbency:" + digest)

        let transaction =
            buildTransaction
                [ RelayEvent.RoadOpened(roadId, authorityRevision, physicalUserMessageId)
                  RelayEvent.IncumbencyOpened(incumbencyId, snapshotId) ]

        { RoadId = roadId
          IncumbencyId = incumbencyId
          AuthorityRevision = authorityRevision
          Transaction = transaction }

    let next
        (sha256: string -> string)
        (roadId: RoadId)
        (retirementId: RetirementId)
        (authorityRevision: AuthorityRevision)
        (snapshotId: WorkspaceSnapshotId)
        =
        let incumbencyId =
            sha256 ("manager-loop-v1\n" + RetirementId.value retirementId)
            |> fun digest -> IncumbencyId.create ("incumbency:" + digest)

        let transaction =
            buildTransaction [ RelayEvent.IncumbencyOpened(incumbencyId, snapshotId) ]

        { RoadId = roadId
          IncumbencyId = incumbencyId
          AuthorityRevision = authorityRevision
          Transaction = transaction }

[<RequireQualifiedAccess>]
type RelayFactCases =
    | TransactionCommitted of
        {| RoadId: RoadId
           Transaction: RelayTransaction |}
