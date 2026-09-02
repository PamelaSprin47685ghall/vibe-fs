namespace Wanxiangshu.Verification

open System.Threading.Tasks

/// EventStore-only temporal proofs for the physical journal writer boundary.
/// Kept separate from TemporalSurface so verification never forms a dual-write bridge.
module EventStoreWriterSurface =

    /// PERSIST lifecycle proof: release closes admission but drains every append
    /// admitted while Open; later appends are known-not-attempted.
    val writerReleaseDrainScenario: unit -> Task<obj>

    /// PERSIST forensic proof: a physical first failure poisons the writer once;
    /// later calls never hit storage and preserve the original failure text.
    val writerPoisonPreservesFirstFailureScenario: unit -> Task<obj>
