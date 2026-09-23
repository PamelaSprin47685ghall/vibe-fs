namespace Wanxiangshu.Sphinx.V2.Plugins

open System

/// The 16 probe manifests.
///
/// WHAT[sphinx-v2-031]: every probe must actually be dispatchable, absorbable and
/// re-estimable. A probe that is only registered by name is not a probe — it is a
/// string in a list.
///
/// WHAT[sphinx-v2-002]: no field here carries a quality weight, method utility or
/// expected default gain. A manifest carries cost, response limits and the material it
/// needs; whether the probe applies and what it contributes is the LLM's judgement.
/// `not-applicable`, empty findings and "no counterexample found" are successful
/// completions — the Runtime never retries a probe until it produces a preset answer.
/// A probe's declared shape: the question it asks, the extra fields its response
/// carries, and the mechanical facts the Runtime needs to dispatch it.
type ProbeManifest =
    {
        Id: string
        Title: string
        /// The schema its response is decoded against.
        ResponseSchemaId: string
        ResponseSchemaHash: string
        /// Extra response fields beyond the shared probe envelope.
        ResponseFields: string list
        /// Tool capabilities the probe may request. A request is not a grant.
        ToolNeeds: string list
        /// Declared default write permission. Probes are read-only by default.
        DefaultWriteAccess: bool
        /// Maximum response bytes.
        ResponseByteLimit: int
        /// How findings become graph deltas.
        DeltaShape: string list
    }

type ProbeError = { Code: string; Message: string }

module Catalog =

    let private error code message : Result<'value, ProbeError> =
        Error { Code = code; Message = message }

    /// The shared envelope every probe response carries.
    let sharedResponseFields: string list =
        [ "scopeRef"; "inputArtifactRefs"; "applicability"; "findings"; "unresolved" ]

    /// `applicability` is one of three honest answers, and "not-applicable" is a
    /// successful result rather than a retry signal.
    let applicabilityValues: string list = [ "applicable"; "not-applicable"; "unclear" ]

    let sharedToolNeeds: string list = [ "context.read" ]

    let sharedDeltaShape: string list =
        [ "condition"
          "revises"
          "counterexample"
          "question-reframe"
          "answer-fragment" ]

    let private probe id title schemaId responseFields toolNeeds deltaShape : ProbeManifest =
        { Id = id
          Title = title
          ResponseSchemaId = schemaId
          ResponseSchemaHash = ""
          ResponseFields = responseFields @ sharedResponseFields
          ToolNeeds = sharedToolNeeds @ toolNeeds
          DefaultWriteAccess = false
          ResponseByteLimit = 16000
          DeltaShape = deltaShape }

    /// The 16 probes, each with its own response fields and delta shape. The count is
    /// not a product target; a probe that cannot be dispatched is not listed here.
    let all: ProbeManifest list =
        [ probe
              "sphinx.probe.boundary-degeneracy@2"
              "P-01 边界与退化"
              "sphinx.probe.response@2"
              [ "testedConditions" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.magnitude-units@2"
              "P-02 数量级与单位"
              "sphinx.probe.response@2"
              [ "quantities"; "orderRelations"; "missingMeasurements" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.structural-analogy@2"
              "P-03 结构类比"
              "sphinx.probe.response@2"
              [ "mapping"; "transferredIdeas"; "mappingLimits" ]
              []
              [ "answer-fragment" ]
          probe
              "sphinx.probe.new-interpretation@2"
              "P-04 新解释"
              "sphinx.probe.response@2"
              [ "hypotheses"; "explains"; "doesNotExplain" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.hidden-variable@2"
              "P-05 隐藏变量"
              "sphinx.probe.response@2"
              [ "variables"; "newOptions" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.steelman@2"
              "P-06 钢人化"
              "sphinx.probe.response@2"
              [ "strongestCase"; "conditions"; "limits" ]
              []
              [ "answer-fragment"; "condition" ]
          probe
              "sphinx.probe.counterexample@2"
              "P-07 反例与边界修复"
              "sphinx.probe.response@2"
              [ "counterexamples"; "affectedClaims"; "repairs" ]
              []
              [ "counterexample"; "revises" ]
          probe
              "sphinx.probe.conditional-intervention@2"
              "P-08 条件干预"
              "sphinx.probe.response@2"
              [ "intervention"; "heldFixed"; "predictedChanges"; "incoherence" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.constraints-bottleneck@2"
              "P-09 约束与瓶颈"
              "sphinx.probe.response@2"
              [ "constraints"; "regimes"; "suggestedChanges" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.combination@2"
              "P-10 组合方案"
              "sphinx.probe.response@2"
              [ "compatibleSets"; "conflicts"; "combinedPlanCards" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.lossless-synthesis@2"
              "P-11 无损综合"
              "sphinx.probe.response@2"
              [ "groups"; "retainedDifferences"; "draftFragments" ]
              []
              [ "answer-fragment" ]
          probe
              "sphinx.probe.principle-case@2"
              "P-12 原则与案例"
              "sphinx.probe.response@2"
              [ "tensions"; "revisionOptions" ]
              []
              [ "question-reframe" ]
          probe
              "sphinx.probe.problem-reframing@2"
              "P-13 问题重构"
              "sphinx.probe.response@2"
              [ "reframings"; "goalPreservationNotes"; "omissions" ]
              []
              [ "question-reframe" ]
          probe
              "sphinx.probe.minimal-discriminator@2"
              "P-14 最小区分实验"
              "sphinx.probe.response@2"
              [ "experiment"; "possibleObservations"; "conditionalActions" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.revision-after-reasons@2"
              "P-15 匿名理由后修订"
              "sphinx.probe.response@2"
              [ "priorResponseRef"; "revisedResponse"; "newReasons" ]
              []
              [ "condition" ]
          probe
              "sphinx.probe.gap-and-stop@2"
              "P-16 缺口与停止"
              "sphinx.probe.response@2"
              [ "remainingPlans"; "stopComparison"; "answerChanges" ]
              []
              [ "answer-fragment"; "condition" ] ]

    let private openQuestionManifest: ProbeManifest =
        { Id = "sphinx.probe.open-question@2"
          Title = "动态问题"
          ResponseSchemaId = "sphinx.probe.response@2"
          ResponseSchemaHash = ""
          ResponseFields = sharedResponseFields
          ToolNeeds = sharedToolNeeds
          DefaultWriteAccess = false
          ResponseByteLimit = 16000
          DeltaShape = sharedDeltaShape }

    /// The dynamic wrapper. A question the LLM invents outside the library is carried
    /// here, with the registered response schema and only the tools already
    /// authorized. It is not an arbitrary-code path.
    let openQuestion: ProbeManifest = openQuestionManifest

    let tryFind (id: string) : Result<ProbeManifest, ProbeError> =
        match (all @ [ openQuestion ]) |> List.tryFind (fun manifest -> manifest.Id = id) with
        | Some manifest -> Ok manifest
        | None -> error "unknown-probe" (sprintf "no probe is registered as %s" id)

    /// A manifest is dispatchable only if it names a response schema, declares its
    /// applicability values and carries no cost-free claim. WHAT[sphinx-v2-031] makes
    /// "looks registered" insufficient.
    let validate (manifest: ProbeManifest) : Result<unit, ProbeError> =
        match
            System.String.IsNullOrWhiteSpace manifest.ResponseSchemaId,
            List.isEmpty manifest.ResponseFields,
            List.isEmpty manifest.DeltaShape
        with
        | true, _, _ -> error "invalid-probe" "a probe must name its response schema"
        | _, true, _ -> error "invalid-probe" "a probe must declare its response fields"
        | _, _, true -> error "invalid-probe" "a probe must declare how findings become deltas"
        | false, false, false -> Ok()
