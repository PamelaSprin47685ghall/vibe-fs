namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks

module Surface =
    val canonicalize: serializedFact: string -> obj
    val fold: serializedFacts: string array -> obj
    val nonTerminal: serializedFacts: string array -> sessionId: string -> obj
    val admitIntent: serializedFacts: string array -> messageValue: obj -> attemptedEvidenceValue: obj -> obj
    val acceptanceScenario: attemptedEvidenceValue: obj -> appendOutcome: string -> Task<obj>
    val acceptanceDuplicateScenario: attemptedEvidenceValue: obj -> Task<obj>
    val acceptanceConflictScenario: establishedEvidenceValue: obj -> attemptedEvidenceValue: obj -> Task<obj>
    val providerLifecycleScenario: actions: obj array -> Task<obj>
