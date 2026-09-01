namespace Wanxiangshu.Sphinx

module SphinxSurface =
    val createStore: unit -> obj
    val start: store: obj -> question: string -> obj
    val resume: store: obj -> handle: string -> observation: obj -> obj
    val state: store: obj -> handle: string -> obj
    val close: store: obj -> handle: string -> obj
    val status: store: obj -> handle: string -> obj
    val cancel: store: obj -> handle: string -> obj
    val decode: raw: obj -> obj
    val decodeSemanticAssessmentObservation: raw: obj -> obj
    val decodeCandidatesObservation: raw: obj -> obj
    val decodeInvestigationObservation: raw: obj -> obj
    val decodeSynthesisObservation: raw: obj -> obj
    val mcpServer: store: obj -> obj
    val serverName: string
    val permissionKey: string
    val relativeServerEntry: string
    val isTool: name: string -> bool
    val localCommand: entryPath: string -> string array
    val fixtureCommand: fixturePath: string -> string array
    val libraryNames: unit -> string array
    val phase0MethodNames: unit -> string array
    val solveGraph: problem: obj -> obj
    val mctsRun: iterations: int -> model: obj -> obj
    val mctsUct: parentVisits: int -> exploration: float -> node: obj -> float
    val paretoFrontier: actions: obj array -> obj array
