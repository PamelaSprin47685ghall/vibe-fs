namespace Wanxiangshu.Mission.Work

type IncumbencyId
type AuthorityRevision
type RetirementId

module IncumbencyId =
    val create: string -> IncumbencyId
    val value: IncumbencyId -> string

module AuthorityRevision =
    val create: string -> AuthorityRevision
    val value: AuthorityRevision -> string

module RetirementId =
    val create: string -> RetirementId
    val value: RetirementId -> string

/// The single gate-kind vocabulary for the manager loop prompt, shared by
/// the Change sender and the projection cut so the two never drift apart.
module ManagerLoopGate =
    val gateKind: retirementId: RetirementId -> string

type ReviewFinding =
    { AcceptanceCriteria: string
      WorkPlan: string }

type ReviewFindings

module ReviewFindings =
    val tryCreate: ReviewFinding list -> Result<ReviewFindings, string>
    val values: ReviewFindings -> ReviewFinding list
    val isEmpty: ReviewFindings -> bool

type ReviewBinding =
    { PhysicalUserMessageId: string
      ProviderRunId: string
      ToolCallId: string
      NarrativeDigest: string
      PayloadDigest: string
      RootRequestDigest: string
      RequirementSetDigest: string
      EvidenceFrontierDigest: string }
