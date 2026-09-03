namespace Wanxiangshu.Sphinx.Core

open System

module Reducer =

    let private error code message = Error { Code = code; Message = message }

    let private isFiniteNumber (value: float) : bool = not (Double.IsNaN value || Double.IsInfinity value)

    let stateTag (state: WorkState) =
        match state with
        | Planned -> "Planned"
        | Ready -> "Ready"
        | Leased _ -> "Leased"
        | Running _ -> "Running"
        | WorkState.InputRequired _ -> "InputRequired"
        | Succeeded _ -> "Succeeded"
        | WorkState.Failed _ -> "Failed"
        | WorkState.Cancelled _ -> "Cancelled"
        | Superseded _ -> "Superseded"

    let private finiteNonnegative value = isFiniteNumber value && value >= 0.0

    let private validBudget budget = budget |> Map.forall (fun _ value -> finiteNonnegative value)

    let private lockMap entries =
        entries
        |> List.fold
            (fun result entry ->
                result
                |> Result.bind (fun locked ->
                    if String.IsNullOrWhiteSpace entry.Plugin.Id
                       || String.IsNullOrWhiteSpace entry.Plugin.Release
                       || String.IsNullOrWhiteSpace entry.Plugin.AbiHash then
                        error "invalid-plugin-lock" "plugin identity fields must not be blank"
                    elif Map.containsKey entry.Plugin.Id locked then
                        error "duplicate-plugin" (sprintf "plugin %s is bound more than once" entry.Plugin.Id)
                    else
                        Ok(Map.add entry.Plugin.Id entry locked)))
            (Ok Map.empty)

    let private sameLock entries locked =
        match lockMap entries with
        | Ok candidate -> candidate = locked
        | Error _ -> false

    let private dependenciesDone (state: InquiryState) (work: WorkSpec) =
        work.Dependencies
        |> Set.forall (fun dependency ->
            state.Work
            |> Map.tryFind dependency
            |> Option.exists (fun item ->
                match item.State with
                | Succeeded _ -> true
                | _ -> false))

    let private validTransition (state: InquiryState) (work: WorkSpec) (current: WorkState) (next: WorkState) =
        let sameAttempt proof = proof.Attempt = work.Attempt

        match current, next with
        | Planned, Ready when dependenciesDone state work -> Ok()
        | Planned, Ready -> error "dependency-unsatisfied" "work dependencies are not complete"
        | Ready, Leased proof when sameAttempt proof && not (String.IsNullOrWhiteSpace proof.Fence) -> Ok()
        | Ready, Leased _ -> error "missing-fence" "leased work requires its exact fence"
        | Leased previous, Running proof
            when sameAttempt proof
                 && proof.Fence = previous.Fence
                 && proof.Session |> Option.exists (String.IsNullOrWhiteSpace >> not) ->
            Ok()
        | Leased previous, Ready when previous.Attempt = work.Attempt -> Ok()
        | Running previous, WorkState.InputRequired proof
            when sameAttempt proof && proof.Fence = previous.Fence && proof.Session = previous.Session ->
            Ok()
        | WorkState.InputRequired previous, Running proof
            when sameAttempt proof && proof.Fence = previous.Fence && proof.Session = previous.Session ->
            Ok()
        | (Leased previous | Running previous | WorkState.InputRequired previous), WorkState.Cancelled proof
            when proof.Attempt = previous.Attempt ->
            Ok()
        | Running previous, Succeeded proof when proof.Attempt = previous.Attempt -> Ok()
        | Running previous, WorkState.Failed proof when proof.Attempt = previous.Attempt -> Ok()
        | WorkState.Failed previous, Ready when work.Attempt = previous.Attempt + 1 -> Ok()
        | WorkState.Cancelled previous, Ready when work.Attempt = previous.Attempt + 1 -> Ok()
        | Planned, Superseded successor
        | Ready, Superseded successor ->
            if Map.containsKey successor state.Work then
                Ok()
            else
                error "unknown-work" (sprintf "successor work %s is not planned" (WorkId.value successor))
        | Succeeded _, Succeeded _ -> error "duplicate-observation" "an attempt already accepted an observation"
        | _ ->
            error
                "illegal-transition"
                (sprintf "work cannot transition from %s to %s" (stateTag current) (stateTag next))

    let private advance event state =
        { state with
            Revision = event.Revision
            EventHead = event.Id }

    let private verifyNext state event =
        if event.InquiryId <> state.Id then
            error "inquiry-mismatch" "event belongs to another inquiry"
        elif event.Revision <> state.Revision + 1L then
            error "revision-conflict" "event revision is not the next revision"
        elif event.Parent <> Some state.EventHead then
            error "parent-conflict" "event parent is not the current head"
        else
            Ok()

    let private patchGraph patch state =
        let nodes =
            patch.RemoveNodes
            |> List.fold (fun graph nodeId -> Map.remove nodeId graph) state.Graph
            |> fun graph -> patch.UpsertNodes |> List.fold (fun current node -> Map.add node.Id node current) graph

        let removed = patch.RemoveNodes |> Set.ofList

        let edges =
            state.Edges
            |> Map.filter (fun edgeId edge ->
                not (List.contains edgeId patch.RemoveEdges)
                && Set.intersect removed edge.Tails |> Set.isEmpty
                && Set.intersect removed edge.Heads |> Set.isEmpty)
            |> fun graph -> patch.UpsertEdges |> List.fold (fun current edge -> Map.add edge.Id edge current) graph

        if
            edges
            |> Map.exists (fun _ edge ->
                Set.union edge.Tails edge.Heads
                |> Set.exists (fun nodeId -> not (Map.containsKey nodeId nodes)))
        then
            error "dangling-edge" "hyperedge endpoints must exist"
        else
            Ok { state with Graph = nodes; Edges = edges }

    let private planWork specs state =
        specs
        |> List.fold
            (fun result spec ->
                result
                |> Result.bind (fun work ->
                    if spec.Attempt < 1 then
                        error "invalid-attempt" "work attempt must be positive"
                    elif Map.containsKey spec.Id work then
                        error "duplicate-work" (sprintf "work %s already exists" (WorkId.value spec.Id))
                    else
                        Ok(Map.add spec.Id { Spec = spec; State = Planned } work)))
            (Ok state.Work)
        |> Result.map (fun work -> { state with Work = work })

    let private sameSpecFields (current: WorkSpec) (incoming: WorkSpec) =
        current.Id = incoming.Id
        && current.BranchId = incoming.BranchId
        && current.Producer = incoming.Producer
        && current.Capability = incoming.Capability
        && current.Input = incoming.Input
        && current.OutputSchema = incoming.OutputSchema
        && current.Dependencies = incoming.Dependencies
        && current.ConflictKeys = incoming.ConflictKeys
        && current.BlindToken = incoming.BlindToken
        && current.RandomSeed = incoming.RandomSeed
        && current.Budget = incoming.Budget

    let private checkSpec (current: WorkItem) (spec: WorkSpec) (next: WorkState) =
        let retry =
            match current.State, next with
            | WorkState.Failed _, Ready
            | WorkState.Cancelled _, Ready -> true
            | _ -> false

        if not (sameSpecFields current.Spec spec) then
            error "spec-mismatch" "work spec is immutable within its lifecycle"
        elif retry && spec.Attempt <> current.Spec.Attempt + 1 then
            error "invalid-attempt" "retry must advance the attempt by exactly one"
        elif not retry && spec.Attempt <> current.Spec.Attempt then
            error "attempt-conflict" "work attempt does not match the planned attempt"
        else
            Ok()

    let private transitionWork (spec: WorkSpec) (fromState: string) (nextState: WorkState) (state: InquiryState) =
        match Map.tryFind spec.Id state.Work with
        | None -> error "unknown-work" (sprintf "work %s is not planned" (WorkId.value spec.Id))
        | Some current when stateTag current.State <> fromState ->
            if stateTag current.State = "Succeeded" && stateTag nextState = "Succeeded" then
                error "duplicate-observation" "an attempt already accepted an observation"
            else
                error
                    "stale-work-state"
                    (sprintf "expected %s but work is %s" fromState (stateTag current.State))
        | Some current ->
            checkSpec current spec nextState
            |> Result.bind (fun () -> validTransition state spec current.State nextState)
            |> Result.map (fun () ->
                { state with
                    Work =
                        state.Work
                        |> Map.add spec.Id { Spec = spec; State = nextState } })

    let private checkGuarantee slot certificate =
        match slot with
        | "exact" when certificate.Exact.IsSome ->
            match Map.tryFind "exact" certificate.Guarantees with
            | Some(DeterministicInclusion _) -> Ok()
            | Some _ -> error "missing-guarantee" "exact slot requires a deterministic inclusion guarantee"
            | None -> Ok()
        | "bound" when certificate.LowerEnvelope.IsSome || certificate.UpperEnvelope.IsSome ->
            match Map.tryFind "bound" certificate.Guarantees with
            | Some(DeterministicInclusion _) -> Ok()
            | Some _ -> error "missing-guarantee" "bound slot requires a deterministic inclusion guarantee"
            | None -> Ok()
        | "sample" when certificate.SampleSummary.IsSome ->
            match Map.tryFind "sample" certificate.Guarantees with
            | Some(ProbabilisticCoverage _) -> Ok()
            | _ -> error "missing-coverage" "sample slot requires a probabilistic coverage guarantee"
        | "latent" when certificate.LatentPosterior.IsSome ->
            match Map.tryFind "latent" certificate.Guarantees with
            | Some(ProbabilisticCoverage _) -> Ok()
            | _ -> error "missing-coverage" "latent slot requires a probabilistic coverage guarantee"
        | "ordinal" when not (List.isEmpty certificate.OrdinalConstraints) ->
            match Map.tryFind "ordinal" certificate.Guarantees with
            | Some(OrdinalModel _) -> Ok()
            | _ -> error "missing-guarantee" "ordinal slot requires an ordinal guarantee"
        | "residual" when certificate.Residual.IsSome ->
            match Map.tryFind "residual" certificate.Guarantees with
            | Some ResidualOnly -> Ok()
            | Some _ -> error "missing-guarantee" "residual slot carries an unrelated guarantee"
            | None -> Ok()
        | _ -> Ok()

    let private checkCertificate certificate =
        [ "exact"; "bound"; "sample"; "latent"; "ordinal"; "residual" ]
        |> List.fold
            (fun result slot -> result |> Result.bind (fun () -> checkGuarantee slot certificate))
            (Ok())

    let private debitBudget debit state =
        if not (validBudget debit) then
            error "invalid-budget" "resource debit must be finite and nonnegative"
        else
            debit
            |> Map.fold
                (fun result resource amount ->
                    result
                    |> Result.bind (fun budget ->
                        let remaining = budget |> Map.tryFind resource |> Option.defaultValue 0.0

                        if amount > remaining then
                            error "budget-exhausted" (sprintf "resource %s is exhausted" resource)
                        else
                            Ok(Map.add resource (remaining - amount) budget)))
                (Ok state.Budget)
            |> Result.map (fun budget -> { state with Budget = budget })

    let private applyExisting state event =
        verifyNext state event
        |> Result.bind (fun () ->
            match event.Body with
            | InquiryCreated _ -> error "duplicate-inquiry" "inquiry is already created"
            | PluginSetBound entries ->
                if sameLock entries state.PluginLock then
                    Ok state
                else
                    error "plugin-swapped" "plugin lock is immutable"
            | GraphPatched patch -> patchGraph patch state
            | WorkPlanned specs -> planWork specs state
            | WorkTransitioned(spec, fromState, nextState) -> transitionWork spec fromState nextState state
            | ObservationAccepted binding ->
                match Map.tryFind binding.WorkId state.Work with
                | None -> error "unknown-work" "observation work is not planned"
                | Some item when item.Spec.Attempt <> binding.Attempt ->
                    error "attempt-conflict" "observation attempt does not match the work"
                | Some _ when not (sameLock binding.PluginLock state.PluginLock) ->
                    error "plugin-swapped" "observation plugin lock differs from inquiry lock"
                | Some item ->
                    match item.Spec.OutputSchema with
                    | Some expected when expected <> binding.Schema ->
                        error "schema-mismatch" "observation schema differs from work output schema"
                    | _ ->
                        let key = binding.WorkId, binding.Attempt

                        match Map.tryFind key state.Observations with
                        | Some seen when seen = binding.Payload.CanonicalPayload -> Ok state
                        | Some _ ->
                            error "observation-conflict" "an observation already exists for this work attempt"
                        | None ->
                            Ok
                                { state with
                                    Observations = Map.add key binding.Payload.CanonicalPayload state.Observations }
            | CertificatePatched patch ->
                let certificate = patch.Certificate

                if not (Map.containsKey certificate.NodeId state.Graph) then
                    error "unknown-node" "certificate target node does not exist"
                else
                    checkCertificate certificate
                    |> Result.map (fun () ->
                        { state with
                            Certificates = Map.add certificate.NodeId certificate state.Certificates })
            | BudgetDebited debit -> debitBudget debit state
            | InquiryStatusChanged status ->
                match state.Status, status with
                | Completed, Completed
                | InquiryStatus.Cancelled, InquiryStatus.Cancelled -> Ok state
                | InquiryStatus.Failed previous, InquiryStatus.Failed current when previous = current -> Ok state
                | Completed, _
                | InquiryStatus.Failed _, _
                | InquiryStatus.Cancelled, _ -> error "terminal-status" "inquiry status cannot leave its terminal state"
                | _ -> Ok { state with Status = status }
            | AnswerCommitted answer ->
                match state.Answer with
                | Some existing when existing = answer -> Ok state
                | Some _ -> error "answer-conflict" "inquiry answer is immutable"
                | None -> Ok { state with Answer = Some answer; Status = Completed })
        |> Result.map (advance event)

    let apply state event =
        match state, event.Body with
        | None, InquiryCreated(root, entries, budget) ->
            if event.Revision <> 0L || event.Parent.IsSome then
                error "invalid-origin" "inquiry creation must be revision zero without a parent"
            elif not (validBudget budget) then
                error "invalid-budget" "initial budget must be finite and nonnegative"
            else
                lockMap entries
                |> Result.map (fun locked ->
                    { Id = event.InquiryId
                      Revision = event.Revision
                      EventHead = event.Id
                      Graph = Map.empty
                      Edges = Map.empty
                      Certificates = Map.empty
                      Work = Map.empty
                      PluginLock = locked
                      Budget = budget
                      Observations = Map.empty
                      Status = Active
                      Answer = None })
        | None, _ -> error "missing-inquiry" "first event must create the inquiry"
        | Some current, _ -> applyExisting current event

    let fold events =
        events
        |> List.fold
            (fun result event -> result |> Result.bind (fun state -> apply state event |> Result.map Some))
            (Ok None)
        |> Result.bind (function
            | Some state -> Ok state
            | None -> error "empty-history" "inquiry has no events")

    let private envelopeView (envelope: JsonEnvelope) : obj =
        box
            {| schema = {| id = envelope.Schema.Id; hash = envelope.Schema.Hash |}
               payload = envelope.CanonicalPayload |}

    let private envelopeOptionView (envelope: JsonEnvelope option) : obj =
        envelope |> Option.map envelopeView |> Option.toObj

    let private guaranteeView slot guarantee : obj =
        let assumedNames assumptions =
            assumptions |> Set.toArray |> Array.sort

        match guarantee with
        | DeterministicInclusion assumptions ->
            box
                {| slot = slot
                   kind = "inclusion"
                   level = (null: obj)
                   error = (null: obj)
                   assumptions = assumedNames assumptions
                   scope = (null: obj) |}
        | ProbabilisticCoverage(level, margin, assumptions, scope) ->
            box
                {| slot = slot
                   kind = "coverage"
                   level = level
                   error = margin
                   assumptions = assumedNames assumptions
                   scope = scope |}
        | OrdinalModel assumptions ->
            box
                {| slot = slot
                   kind = "ordinal"
                   level = (null: obj)
                   error = (null: obj)
                   assumptions = assumedNames assumptions
                   scope = (null: obj) |}
        | ResidualOnly ->
            box
                {| slot = slot
                   kind = "residual"
                   level = (null: obj)
                   error = (null: obj)
                   assumptions = ([||]: string[])
                   scope = (null: obj) |}

    let private certificateView (certificate: ValueCertificate) : obj =
        box
            {| node = NodeId.value certificate.NodeId
               semantics =
                certificate.Semantics
                |> Option.map (fun plugin -> box {| id = plugin.Id; release = plugin.Release; abiHash = plugin.AbiHash |})
                |> Option.toObj
               exact = envelopeOptionView certificate.Exact
               lower = envelopeOptionView certificate.LowerEnvelope
               upper = envelopeOptionView certificate.UpperEnvelope
               sample = envelopeOptionView certificate.SampleSummary
               ordinal = certificate.OrdinalConstraints |> List.map envelopeView |> List.toArray
               latent = envelopeOptionView certificate.LatentPosterior
               residual = envelopeOptionView certificate.Residual
               guarantees =
                certificate.Guarantees
                |> Map.toList
                |> List.sortBy fst
                |> List.map (fun (slot, guarantee) -> guaranteeView slot guarantee)
                |> List.toArray
               witnesses = certificate.WitnessEvents |> List.map EventId.value |> List.toArray
               derivations = certificate.DerivationEvents |> List.map EventId.value |> List.toArray
               revision = certificate.Revision |}

    let private nodeView (node: GraphNode) : obj =
        box
            {| id = NodeId.value node.Id
               kind = node.Kind
               payload = node.Payload.CanonicalPayload
               schema = {| id = node.Payload.Schema.Id; hash = node.Payload.Schema.Hash |}
               revision = node.Revision |}

    let private edgeView (edge: HyperEdge) : obj =
        box
            {| id = EdgeId.value edge.Id
               tails = edge.Tails |> Set.toArray |> Array.map NodeId.value |> Array.sort
               heads = edge.Heads |> Set.toArray |> Array.map NodeId.value |> Array.sort
               relation = edge.Relation
               payload = edge.Payload |> Option.map (fun envelope -> envelope.CanonicalPayload) |> Option.toObj
               payloadSchema =
                edge.Payload
                |> Option.map (fun envelope -> box {| id = envelope.Schema.Id; hash = envelope.Schema.Hash |})
                |> Option.toObj |}

    let private workView item =
        let fence: obj =
            match item.State with
            | Leased proof
            | Running proof
            | InputRequired proof -> box proof.Fence
            | _ -> null

        let session: obj =
            match item.State with
            | Leased proof
            | Running proof
            | InputRequired proof -> proof.Session |> Option.map box |> Option.toObj
            | _ -> null

        let completionAttempt: obj =
            match item.State with
            | Succeeded proof
            | Failed proof
            | Cancelled proof -> box proof.Attempt
            | _ -> null

        let completionEvent: obj =
            match item.State with
            | Succeeded proof
            | Failed proof
            | Cancelled proof -> proof.EventId |> Option.map (EventId.value >> box) |> Option.toObj
            | _ -> null

        let completionDetail: obj =
            match item.State with
            | Succeeded proof
            | Failed proof
            | Cancelled proof ->
                proof.Detail |> Option.map (fun envelope -> box envelope.CanonicalPayload) |> Option.toObj
            | _ -> null

        let successor: obj =
            match item.State with
            | Superseded next -> box (WorkId.value next)
            | _ -> null

        {| id = WorkId.value item.Spec.Id
           branch = BranchId.value item.Spec.BranchId
           attempt = item.Spec.Attempt
           status = stateTag item.State
           capability = item.Spec.Capability
           input = envelopeOptionView item.Spec.Input
           outputSchema =
            item.Spec.OutputSchema
            |> Option.map (fun schema -> box {| id = schema.Id; hash = schema.Hash |})
            |> Option.toObj
           dependencies = item.Spec.Dependencies |> Set.toArray |> Array.map WorkId.value |> Array.sort
           conflictKeys = item.Spec.ConflictKeys |> Set.toArray |> Array.sort
           blindToken = item.Spec.BlindToken |> Option.map (BlindToken.value >> box) |> Option.toObj
           randomSeed = item.Spec.RandomSeed
           budget = item.Spec.Budget |> Map.toList |> List.sortBy fst |> List.toArray
           fence = fence
           session = session
           completionAttempt = completionAttempt
           completionEvent = completionEvent
           completionDetail = completionDetail
           successor = successor |}

    let private lockView (id: string) (entry: PluginLockEntry) : obj =
        box
            {| id = id
               release = entry.Plugin.Release
               abiHash = entry.Plugin.AbiHash
               capabilities = entry.Capabilities |> Set.toArray |> Array.sort
               dependencies = entry.Dependencies |> Set.toArray |> Array.sort
               schemas =
                entry.Schemas
                |> Map.toList
                |> List.sortBy fst
                |> List.map (fun (name, schema) -> box {| name = name; id = schema.Id; hash = schema.Hash |})
                |> List.toArray |}

    let semanticView state =
        {| inquiry = InquiryId.value state.Id
           revision = state.Revision
           eventHead = EventId.value state.EventHead
           graph =
            state.Graph
            |> Map.toList
            |> List.sortBy (fun (nodeId, _) -> NodeId.value nodeId)
            |> List.map (fun (_, node) -> nodeView node)
            |> List.toArray
           edges =
            state.Edges
            |> Map.toList
            |> List.sortBy (fun (edgeId, _) -> EdgeId.value edgeId)
            |> List.map (fun (_, edge) -> edgeView edge)
            |> List.toArray
           certificates =
            state.Certificates
            |> Map.toList
            |> List.sortBy (fun (nodeId, _) -> NodeId.value nodeId)
            |> List.map (fun (_, certificate) -> certificateView certificate)
            |> List.toArray
           work =
            state.Work
            |> Map.toList
            |> List.sortBy (fun (workId, _) -> WorkId.value workId)
            |> List.map (fun (_, item) -> workView item)
            |> List.toArray
           pluginLock =
            state.PluginLock
            |> Map.toList
            |> List.sortBy fst
            |> List.map (fun (id, entry) -> lockView id entry)
            |> List.toArray
           budget = state.Budget |> Seq.map (fun pair -> pair.Key, pair.Value) |> Seq.sortBy fst |> Seq.toArray
           observations =
            state.Observations
            |> Map.toList
            |> List.sortBy (fun ((workId, attempt), _) -> WorkId.value workId, attempt)
            |> List.map (fun ((workId, attempt), payloadHash) ->
                box {| work = WorkId.value workId; attempt = attempt; payloadHash = payloadHash |})
            |> List.toArray
           status = sprintf "%A" state.Status
           answer = state.Answer |> Option.map (fun envelope -> envelope.CanonicalPayload) |}
        :> obj

    let semanticHash state = state |> semanticView |> CoreHash.canonicalSha256
