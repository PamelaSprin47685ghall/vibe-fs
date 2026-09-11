namespace Wanxiangshu.Change


module OrchestratorFactFold =
    val fold:
        orchestrator: OrchestratorProjection ->
        fact: OrchestratorFactCases ->
            Result<OrchestratorProjection, OrchestratorFoldRejection>
