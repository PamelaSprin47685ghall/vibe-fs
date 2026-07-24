namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Fact

module internal AgentFactsFoldHelpers =


    let updateSession
        (sessionId: Wanxiangshu.Next.Kernel.Identity.SessionId)
        (updateFn: SessionAgentProjection -> SessionAgentProjection)
        (map: Map<Wanxiangshu.Next.Kernel.Identity.SessionId, SessionAgentProjection>)
        =
        let existing =
            match Map.tryFind sessionId map with
            | Some s -> s
            | None ->
                { Companion = None
                  Linkage = None
                  ReviewGuard = None
                  Fallback = None
                  Effects = None }

        Map.add sessionId (updateFn existing) map

    let foldOrchestratorManagerJobCreated
        (proj: AgentProjectionSet)
        (managerId: string)
        (worktreePath: string)
        (branch: string)
        =
        let mgrId = ManagerId.create managerId

        let job =
            { WorktreePath = worktreePath
              Branch = branch
              CandidateId = None
              CandidateCommit = None
              PublishedCommit = None }

        { proj with
            Orchestrator =
                { proj.Orchestrator with
                    ManagerJobs = Map.add mgrId job proj.Orchestrator.ManagerJobs } }

    let foldOrchestratorCandidateRegistered
        (proj: AgentProjectionSet)
        (managerId: string)
        (candidateId: string)
        (branch: string)
        (commitHash: string)
        =
        let mgrId = ManagerId.create managerId
        let candId = CandidateId.create candidateId
        let status = Registered(candId, branch, commitHash)

        let existingMgr =
            match Map.tryFind mgrId proj.Orchestrator.Managers with
            | Some _ -> { Status = Some status }
            | None -> { Status = Some status }

        let orch =
            { proj.Orchestrator with
                ManagerJobs =
                    match Map.tryFind mgrId proj.Orchestrator.ManagerJobs with
                    | Some job ->
                        Map.add
                            mgrId
                            { job with
                                CandidateId = Some candId
                                CandidateCommit = Some commitHash }
                            proj.Orchestrator.ManagerJobs
                    | None -> proj.Orchestrator.ManagerJobs
                Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers }

        { proj with Orchestrator = orch }

    let foldOrchestratorPublished
        (proj: AgentProjectionSet)
        (managerId: string)
        (candidateId: string)
        (commitHash: string)
        =
        let mgrId = ManagerId.create managerId
        let candId = CandidateId.create candidateId
        let status = Published(candId, commitHash)

        let existingMgr =
            match Map.tryFind mgrId proj.Orchestrator.Managers with
            | Some _ -> { Status = Some status }
            | None -> { Status = Some status }

        let orch =
            { ManagerJobs =
                match Map.tryFind mgrId proj.Orchestrator.ManagerJobs with
                | Some job ->
                    Map.add
                        mgrId
                        { job with
                            CandidateId = Some candId
                            CandidateCommit = Some commitHash
                            PublishedCommit = Some commitHash }
                        proj.Orchestrator.ManagerJobs
                | None -> proj.Orchestrator.ManagerJobs
              Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers
              PublishedCommit = Some commitHash }

        { proj with Orchestrator = orch }

    let foldOrchestratorRejected (proj: AgentProjectionSet) (managerId: string) (candidateId: string) (reason: string) =
        let mgrId = ManagerId.create managerId
        let candId = CandidateId.create candidateId
        let status = Rejected(candId, reason)

        let existingMgr =
            match Map.tryFind mgrId proj.Orchestrator.Managers with
            | Some _ -> { Status = Some status }
            | None -> { Status = Some status }

        let orch =
            { proj.Orchestrator with
                Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers }

        { proj with Orchestrator = orch }

    let foldDurableEffectRequested
        (proj: AgentProjectionSet)
        (sessionId: Wanxiangshu.Next.Kernel.Identity.SessionId)
        (effectId: string)
        (target: string)
        (payload: string)
        =
        let effId = EffectId.create effectId

        let sessions =
            updateSession
                sessionId
                (fun s ->
                    let effs = { Current = Some(effId, Requested(target, payload)) }

                    { s with Effects = Some effs })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldDurableEffectAccepted
        (proj: AgentProjectionSet)
        (sessionId: Wanxiangshu.Next.Kernel.Identity.SessionId)
        (effectId: string)
        (result: string)
        =
        let effId = EffectId.create effectId

        let sessions =
            updateSession
                sessionId
                (fun s ->
                    let effs =
                        match s.Effects with
                        | Some existing ->
                            let updated =
                                match existing.Current with
                                | Some(currentId, Requested(target, payload)) when currentId = effId ->
                                    Accepted(target, payload, result)
                                | Some(currentId, Accepted(target, payload, _)) when currentId = effId ->
                                    Accepted(target, payload, result)
                                | _ -> Accepted("", "", result)

                            { Current = Some(effId, updated) }
                        | None -> { Current = Some(effId, Accepted("", "", result)) }

                    { s with Effects = Some effs })
                proj.Sessions

        { proj with Sessions = sessions }
