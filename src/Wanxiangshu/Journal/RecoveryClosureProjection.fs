namespace Wanxiangshu.Journal

open System
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Kernel.Identity

/// Pure RecoveryClosure discovery from durable projections (RECOVERY-FAMILY-001).
/// Child-first order: dependents before ancestors; siblings by SessionId.
module RecoveryClosureProjection =

    let private sortKey =
        function
        | RecoveryNode.Blogger(_, id)
        | RecoveryNode.Companion(_, id)
        | RecoveryNode.AgentChild(_, id, _)
        | RecoveryNode.Reviewer(_, id)
        | RecoveryNode.ManagerJob(_, id)
        | RecoveryNode.WorkSession id -> SessionId.value id

    let private digestOf (root: SessionId) (nodes: RecoveryNode list) =
        nodes
        |> List.map (fun node ->
            match node with
            | RecoveryNode.WorkSession id -> "W:" + SessionId.value id
            | RecoveryNode.AgentChild(parent, child, handle) ->
                "A:"
                + SessionId.value parent
                + ">"
                + SessionId.value child
                + ":"
                + AgentHandleId.value handle
            | RecoveryNode.Companion(main, companion) -> "C:" + SessionId.value main + ">" + SessionId.value companion
            | RecoveryNode.Blogger(main, blogger) -> "B:" + SessionId.value main + ">" + SessionId.value blogger
            | RecoveryNode.ManagerJob(jobId, manager) ->
                "M:" + ManagerJobId.value jobId + ":" + SessionId.value manager
            | RecoveryNode.Reviewer(jobId, reviewer) ->
                "R:" + ManagerJobId.value jobId + ":" + SessionId.value reviewer)
        |> fun parts -> String.Join("|", SessionId.value root :: parts)

    let private rootHandles (root: SessionId) (projection: AgentProjectionSet) =
        AgentProjection.tryFind root projection
        |> Option.bind (fun session -> session.Handles)
        |> Option.defaultValue HandleProjection.empty

    let private isLinkedChild (root: SessionId) (child: SessionId) (projection: AgentProjectionSet) =
        HandleProjection.linkedChildren (rootHandles root projection)
        |> List.exists (fun record -> record.ChildSessionId = child)

    /// Discover durable recovery dependency closure for a parent session.
    let discover (root: SessionId) (projection: AgentProjectionSet) (journalSequence: int64) : RecoveryClosure =
        let nodes = ResizeArray<RecoveryNode>()
        let seen = System.Collections.Generic.HashSet<string>()

        let add (node: RecoveryNode) =
            let key = sortKey node

            if seen.Add key then
                nodes.Add node

        add (RecoveryNode.WorkSession root)

        for record in HandleProjection.linkedChildren (rootHandles root projection) do
            match record.Lifecycle, HandleId.tryAgent record.Handle with
            | HandleLifecycle.Retired, _
            | HandleLifecycle.Abandoned _, _
            | _, None -> ()
            | HandleLifecycle.Active, Some agentHandle
            | HandleLifecycle.CompletedAwaitingJoin _, Some agentHandle ->
                add (RecoveryNode.AgentChild(root, record.ChildSessionId, agentHandle))

                match SessionAssociationProjection.tryBloggerOf record.ChildSessionId projection.Associations with
                | Some blogger ->
                    add (RecoveryNode.Companion(record.ChildSessionId, blogger))
                    add (RecoveryNode.Blogger(record.ChildSessionId, blogger))
                | None -> ()

        match SessionAssociationProjection.tryBloggerOf root projection.Associations with
        | Some blogger ->
            add (RecoveryNode.Companion(root, blogger))
            add (RecoveryNode.Blogger(root, blogger))
        | None -> ()

        for job in OrchestratorProjection.activeJobs projection.Orchestrator do
            let related =
                job.ManagerSessionId = root
                || isLinkedChild root job.ManagerSessionId projection

            if related then
                add (RecoveryNode.ManagerJob(job.ManagerJobId, job.ManagerSessionId))

                if job.ManagerSessionId <> root then
                    add (
                        RecoveryNode.AgentChild(
                            root,
                            job.ManagerSessionId,
                            AgentHandleId.create (ManagerJobId.value job.ManagerJobId)
                        )
                    )

                match SessionAssociationProjection.tryBloggerOf job.ManagerSessionId projection.Associations with
                | Some blogger ->
                    add (RecoveryNode.Companion(job.ManagerSessionId, blogger))
                    add (RecoveryNode.Blogger(job.ManagerSessionId, blogger))
                | None -> ()

        for sessionId, session in Map.toList projection.Sessions do
            let pending =
                session.PromptAuthority
                |> Option.map (fun authority -> not (Map.isEmpty authority.PendingClaims))
                |> Option.defaultValue false

            let openBlogger =
                session.BloggerCycles
                |> Option.map (fun cycles -> not (Map.isEmpty cycles.OpenByRequestId))
                |> Option.defaultValue false

            if pending || openBlogger then
                let related =
                    sessionId = root
                    || SessionAssociationProjection.tryMainSessionOf sessionId projection.Associations = Some root
                    || isLinkedChild root sessionId projection

                if related then
                    add (RecoveryNode.WorkSession sessionId)

        let rank =
            function
            | RecoveryNode.Blogger _ -> 0
            | RecoveryNode.Companion _ -> 1
            | RecoveryNode.AgentChild _
            | RecoveryNode.Reviewer _ -> 2
            | RecoveryNode.ManagerJob _ -> 3
            | RecoveryNode.WorkSession _ -> 4

        let ordered =
            nodes |> Seq.toList |> List.sortBy (fun node -> rank node, sortKey node)

        { Root = root
          Nodes = ordered
          Digest = digestOf root ordered
          JournalSequence = journalSequence }
