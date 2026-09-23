namespace Wanxiangshu.Sphinx.V2.Plugins

type RenderRequest =
    { GoalRef: string
      MaterialRefs: string list
      /// Conditions and disagreements the answer must reflect.
      Conditions: string list
      StopReason: string
      SourceRefs: string list }

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
    val validate: Set<string> -> RenderResult -> Result<RenderResult, RenderFault>
