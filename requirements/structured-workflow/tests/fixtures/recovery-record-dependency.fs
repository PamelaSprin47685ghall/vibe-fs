module RecoveryRecordDependency

type RecoveryDependencies =
    { Load: string -> string
      Save: string -> unit }

let recoverJobs deps jobs =
    jobs
    |> List.map (fun job ->
        let current =
            deps.Load job

        deps.Save current
        current)
