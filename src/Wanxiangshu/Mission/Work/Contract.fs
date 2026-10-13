namespace Wanxiangshu.Mission.Work

type IncumbencyId = private IncumbencyId of string
type AuthorityRevision = private AuthorityRevision of string
type RetirementId = private RetirementId of string

module IncumbencyId =
    let create value = IncumbencyId value
    let value (IncumbencyId value) = value

module AuthorityRevision =
    let create value = AuthorityRevision value
    let value (AuthorityRevision value) = value

module RetirementId =
    let create value = RetirementId value
    let value (RetirementId value) = value

/// The single gate-kind vocabulary for the manager loop prompt, shared by
/// the Change sender and the projection cut so the two never drift apart.
module ManagerLoopGate =
    let gateKind (retirementId: RetirementId) =
        "manager-loop:" + RetirementId.value retirementId

type ReviewFinding =
    { AcceptanceCriteria: string
      WorkPlan: string }

type ReviewFindings = private ReviewFindings of ReviewFinding list

module ReviewFindings =
    let tryCreate (findings: ReviewFinding list) =
        if
            findings
            |> List.forall (fun finding ->
                not (System.String.IsNullOrWhiteSpace finding.AcceptanceCriteria)
                && not (System.String.IsNullOrWhiteSpace finding.WorkPlan))
        then
            Ok(ReviewFindings findings)
        else
            Error "AssessmentFinding requires non-empty acceptance criteria and work plan"

    let values (ReviewFindings findings) = findings

    let isEmpty (ReviewFindings findings) = List.isEmpty findings

type ReviewBinding =
    { PhysicalUserMessageId: string
      ProviderRunId: string
      ToolCallId: string
      NarrativeDigest: string
      PayloadDigest: string
      RootRequestDigest: string
      RequirementSetDigest: string
      EvidenceFrontierDigest: string }
