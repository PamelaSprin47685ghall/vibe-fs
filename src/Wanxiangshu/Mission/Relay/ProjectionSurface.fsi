namespace Wanxiangshu.Mission.Relay

module ProjectionSurface =
    val applyCut:
        messages: obj array ->
        providerRunId: string ->
        toolCallId: string ->
        retiredRunIds: string array ->
        authorityMessageIds: string array ->
            obj
