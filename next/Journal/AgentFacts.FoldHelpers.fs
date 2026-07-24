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
            | Some m ->
                { Status = Some status
                  History = status :: m.History }
            | None ->
                { Status = Some status
                  History = [ status ] }

        let orch =
            { proj.Orchestrator with
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
            | Some m ->
                { Status = Some status
                  History = status :: m.History }
            | None ->
                { Status = Some status
                  History = [ status ] }

        let published = proj.Orchestrator.PublishedCommits @ [ commitHash ]

        let orch =
            { Managers = Map.add mgrId existingMgr proj.Orchestrator.Managers
              PublishedCommits = published }

        { proj with Orchestrator = orch }

    let foldOrchestratorRejected (proj: AgentProjectionSet) (managerId: string) (candidateId: string) (reason: string) =
        let mgrId = ManagerId.create managerId
        let candId = CandidateId.create candidateId
        let status = Rejected(candId, reason)

        let existingMgr =
            match Map.tryFind mgrId proj.Orchestrator.Managers with
            | Some m ->
                { Status = Some status
                  History = status :: m.History }
            | None ->
                { Status = Some status
                  History = [ status ] }

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
                    let effs =
                        match s.Effects with
                        | Some existing -> { Effects = Map.add effId (Requested(target, payload)) existing.Effects }
                        | None -> { Effects = Map.ofList [ (effId, Requested(target, payload)) ] }

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
                                match Map.tryFind effId existing.Effects with
                                | Some(Requested(target, payload)) -> Accepted(target, payload, result)
                                | Some(Accepted(target, payload, _)) -> Accepted(target, payload, result)
                                | None -> Accepted("", "", result)

                            { Effects = Map.add effId updated existing.Effects }
                        | None -> { Effects = Map.ofList [ (effId, Accepted("", "", result)) ] }

                    { s with Effects = Some effs })
                proj.Sessions

        { proj with Sessions = sessions }
