namespace Wanxiangshu.Mission.Relay

[<RequireQualifiedAccess>]
type RelayEvent =
    | RoadDevOpsBound of RoadId * devopsId: string * modelTarget: string option
    | IncumbencyOpened of IncumbencyId
    | AuthorityRevisionAdvanced of
        IncumbencyId *
        expected: AuthorityRevision *
        next: AuthorityRevision *
        PhysicalUserMessageId
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

    let initial () : Result<unit, string> =
        Error "Refused"

    let next
        (sha256: string -> string)
        (roadId: RoadId)
        (retirementId: RetirementId)
        (authorityRevision: AuthorityRevision)
        =
        let incumbencyId =
            sha256 ("manager-loop-v1\n" + RetirementId.value retirementId)
            |> fun digest -> IncumbencyId.create ("incumbency:" + digest)

        let transaction =
            buildTransaction [ RelayEvent.IncumbencyOpened incumbencyId ]

        { RoadId = roadId
          IncumbencyId = incumbencyId
          AuthorityRevision = authorityRevision
          Transaction = transaction }

[<RequireQualifiedAccess>]
type RelayFactCases =
    | TransactionCommitted of
        {| RoadId: RoadId
           Transaction: RelayTransaction |}
