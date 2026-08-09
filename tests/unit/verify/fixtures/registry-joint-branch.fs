module RegistryJointBranch

let runs = Dictionary<string, StudentRun>()
let finalCompletions = Dictionary<string, FinalCompletion>()

let handleTurn sessionId outcome =
    match runs.TryGetValue sessionId, finalCompletions.TryGetValue sessionId with
    | (true, run), (true, completion) when outcome = TurnCompleted ->
        sendCompile run completion
    | _ -> ()
