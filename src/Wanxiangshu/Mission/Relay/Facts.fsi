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

type RelayTransaction

module RelayTransaction =
    val create: RelayEvent list -> Result<RelayTransaction, string>
    val events: RelayTransaction -> RelayEvent list

type IncumbencyOpening =
    { RoadId: RoadId
      IncumbencyId: IncumbencyId
      AuthorityRevision: AuthorityRevision
      Transaction: RelayTransaction }

module IncumbencyOpening =
    val initial: unit -> Result<unit, string>

    val next:
        sha256: (string -> string) ->
        roadId: RoadId ->
        retirementId: RetirementId ->
        authorityRevision: AuthorityRevision ->
            IncumbencyOpening

[<RequireQualifiedAccess>]
type RelayFactCases =
    | TransactionCommitted of
        {| RoadId: RoadId
           Transaction: RelayTransaction |}
