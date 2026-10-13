namespace Wanxiangshu.Mission.Relay

module Surface =
    [<RequireQualifiedAccess>]
    type Admission =
        | Refused

    val empty: unit -> RelayState

    val openRoad: unit -> Admission

    val openIncumbency: unit -> Admission

    val assess: unit -> Admission

    val advanceAuthority:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        expected: string ->
        next: string ->
        authorityMessageId: string ->
            obj

    val blockCleanup: state: RelayState -> road: string -> incumbent: string -> blockerDigest: string -> obj

    val confirmRetirement: unit -> Admission

    val retire:
        state: RelayState ->
        road: string ->
        incumbent: string ->
        retirement: string ->
        providerRun: string ->
        toolCall: string ->
        outcome: string ->
            obj

    val view: state: RelayState -> road: string -> obj
    val authority: state: RelayState -> road: string -> obj
    val retirement: state: RelayState -> road: string -> obj
    val roadDevOps: state: RelayState -> road: string -> obj
    val bindRoadDevOps: state: RelayState -> road: string -> devopsId: string -> modelTarget: obj -> obj
