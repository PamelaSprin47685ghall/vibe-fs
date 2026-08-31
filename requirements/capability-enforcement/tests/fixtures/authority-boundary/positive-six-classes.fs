namespace Fixture

// DSL-AUTHORITY: Evidence
// DSL-ISSUE: CurrentEvidence
type CurrentEvidence = private CurrentEvidence of subject: string * version: int64 * digest: string

// DSL-AUTHORITY: Decision
// DSL-ISSUE: AdmissionDecision
type AdmissionDecision = private | Admit | Refuse

// DSL-AUTHORITY: Witness
// DSL-ISSUE: CurrentWitness
type CurrentWitness = private CurrentWitness of subject: string * version: int64 * digest: string

// DSL-AUTHORITY: Capability
// DSL-ISSUE: OneShotCapability
type OneShotCapability = private OneShotCapability of owner: obj * subject: string * version: int64

// DSL-AUTHORITY: Receipt
// DSL-ISSUE: AppliedReceipt
type AppliedReceipt = { Subject: string; Version: int64; Digest: string }

// DSL-AUTHORITY: PhysicalHandle
// DSL-ISSUE: ProcessPhysicalHandle
type ProcessPhysicalHandle = private ProcessPhysicalHandle of obj

// DSL-AUTHORITY: Vocabulary
type JsCapability = Read | Write

module Owner =
    let issueCurrentEvidence subject version digest = CurrentEvidence(subject, version, digest)
    let issueAdmissionDecision () = AdmissionDecision.Admit
    let issueCurrentWitness subject version digest = CurrentWitness(subject, version, digest)
    let issueOneShotCapability owner subject version = OneShotCapability(owner, subject, version)
    let issueAppliedReceipt subject version digest = { Subject = subject; Version = version; Digest = digest }
    let issueProcessPhysicalHandle value = ProcessPhysicalHandle value
