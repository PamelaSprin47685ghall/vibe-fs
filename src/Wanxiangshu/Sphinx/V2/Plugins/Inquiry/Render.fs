namespace Wanxiangshu.Sphinx.V2.Plugins

/// The final answer work and what it must carry.
///
/// WHAT[sphinx-v2-020]: `Completed` requires an AnswerCommitted, and AnswerCommitted
/// requires a reference to an accepted renderer result. A ranking posterior array is
/// not an answer to the user's question, and rendering is not a fallback that appears
/// only when nothing else ran.

type RenderRequest =
    {
        GoalRef: string
        MaterialRefs: string list
        /// Conditions and disagreements the answer must reflect.
        Conditions: string list
        StopReason: string
        SourceRefs: string list
    }

type RenderResult =
    { AnswerText: string
      UsedArtifactRefs: string list
      UnresolvedRefs: string list
      ScopeRef: string }

[<RequireQualifiedAccess>]
type RenderFault =
    | BlankAnswerText
    | UnknownArtifactRef of artifactRef: string
    | UnresolvedBeyondScope of artifactRef: string

module Render =

    /// The renderer must produce text, and every artifact it cites must exist.
    let validate (known: Set<string>) (result: RenderResult) : Result<RenderResult, RenderFault> =
        let unknown =
            result.UsedArtifactRefs
            |> List.tryFind (fun reference -> not (Set.contains reference known))

        let unresolved =
            result.UnresolvedRefs
            |> List.tryFind (fun reference -> not (Set.contains reference known))

        match System.String.IsNullOrWhiteSpace result.AnswerText, unknown, unresolved with
        | true, _, _ -> Error RenderFault.BlankAnswerText
        | false, Some reference, _ -> Error(RenderFault.UnknownArtifactRef reference)
        | false, None, Some reference -> Error(RenderFault.UnresolvedBeyondScope reference)
        | false, None, None -> Ok result
