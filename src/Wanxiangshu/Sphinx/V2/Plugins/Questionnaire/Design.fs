namespace Wanxiangshu.Sphinx.V2.Plugins

/// Round design: what gets shown, in what order, under which seed, and what is withheld.
///
/// WHAT[sphinx-v2-023]: the manifest records the actual assignment, the actual order and
/// the actual visible bytes. "blind=true" is not a manifest. A worker sees opaque
/// labels; the mapping lives only in the host-private ticket, because a worker that
/// could read the mapping would no longer be blind.

type Assignment =
    {
        Seed: string
        /// Name and version of the shuffle actually used.
        ShuffleAlgorithm: string
        /// Opaque label -> real identity. Host-private; never sent to a worker.
        LabelMap: Map<string, string>
        /// The presented order, as shown.
        Order: string list
    }

type RoundDesign =
    {
        ScopeId: string
        SnapshotId: string
        Purpose: string
        QuestionId: string
        TemplateRef: string
        TemplateHash: string
        PresentedSet: string list
        OmittedSet: string list
        Assignment: Assignment
        /// The independence unit: responses in the same cluster are not independent votes.
        ClusterId: string
        ExpectedResponses: int
        MaxAttempts: int
        /// How a missing or excluded response is recorded.
        MissingnessPolicy: string
    }

type DesignError = { Code: string; Message: string }

module Design =

    let private error code message : Result<'value, DesignError> =
        Error { Code = code; Message = message }

    /// A seeded Fisher–Yates over the presented candidates. The seed is recorded so the
    /// order is reproducible; the algorithm is named so a future change is visible as a
    /// protocol change, not a silent one (WHAT[sphinx-v2-023]).
    let private shuffle (seed: string) (items: string list) : string list =
        let digest text =
            let mutable hash = 2166136261u

            for character in text do
                hash <- (hash ^^^ uint32 character) * 16777619u

            hash

        let rec move (remaining: string list) (salt: int) : string list =
            match remaining with
            | [] -> []
            | [ single ] -> [ single ]
            | _ ->
                let index = int (digest (seed + string salt) % uint32 (List.length remaining))

                let chosen = List.item index remaining

                let rest =
                    List.mapi (fun i item -> i, item) remaining
                    |> List.filter (fun (i, _) -> i <> index)
                    |> List.map snd

                chosen :: move rest (salt + 1)

        move items 0

    /// Opaque labels are assigned from the seed too, and are deliberately not the real
    /// ids: a worker must not be able to infer a candidate's identity from its label.
    let buildAssignment (seed: string) (presented: string list) : Assignment =
        let ordered = shuffle seed presented

        let labels = ordered |> List.mapi (fun index _ -> sprintf "item_%d" (index + 1))

        { Seed = seed
          ShuffleAlgorithm = "fisher-yates/1"
          LabelMap = List.zip labels ordered |> Map.ofList
          Order = labels }

    let tryValidate (design: RoundDesign) : Result<RoundDesign, DesignError> =
        let presented = set design.PresentedSet
        let omitted = set design.OmittedSet

        let labelsOutside =
            design.Assignment.Order
            |> List.filter (fun label -> not (Map.containsKey label design.Assignment.LabelMap))

        let realLabels =
            design.Assignment.LabelMap
            |> Map.toList
            |> List.filter (fun (_, real) -> not (Set.contains real presented))

        let overlap = Set.intersect presented omitted

        let decay =
            design.PresentedSet |> List.length <> (design.Assignment.Order |> List.length)

        if System.String.IsNullOrWhiteSpace design.Assignment.Seed then
            error "invalid-design" "round design requires a seed"
        elif overlap |> Set.isEmpty |> not then
            error "invalid-design" "a candidate cannot be both presented and omitted"
        elif decay then
            error "invalid-design" "presented set and assignment order must agree"
        elif labelsOutside |> List.isEmpty |> not then
            error "invalid-design" "assignment order references an unassigned label"
        elif realLabels |> List.isEmpty |> not then
            error "invalid-design" "opaque label maps to a candidate outside the presented set"
        elif design.MaxAttempts < 1 then
            error "invalid-design" "a round must allow at least one attempt"
        elif design.ExpectedResponses < 1 then
            error "invalid-design" "a round must expect at least one response"
        else
            Ok design

    /// The worker-visible envelope: opaque labels in the displayed order, the question
    /// and the response schema. The label map is deliberately absent.
    let workerView (design: RoundDesign) : (string * string) list =
        [ ("scopeId", design.ScopeId)
          ("snapshotId", design.SnapshotId)
          ("purpose", design.Purpose)
          ("questionId", design.QuestionId)
          ("templateHash", design.TemplateHash)
          ("presentedLabels", design.Assignment.Order |> String.concat ",")
          ("visibleBytesHash", design.TemplateHash)
          ("clusterId", design.ClusterId) ]
