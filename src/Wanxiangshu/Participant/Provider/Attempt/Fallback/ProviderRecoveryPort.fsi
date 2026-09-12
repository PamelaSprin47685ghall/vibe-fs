namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// Consumer-side journal reads and reactive change wait needed by ProviderRecoveryWorkflow.
type ProviderRecoveryJournalPort =
    { TryRequestKind: SessionId -> PhysicalUserMessageId -> ProviderRequestKind option
      TryMainSessionOf: SessionId -> SessionId option
      TryBlogState: SessionId -> (PrefixEpochId * BlogProjectionState) option
      AwaitChange: JournalRevision -> CancellationToken -> Task<unit option> }
