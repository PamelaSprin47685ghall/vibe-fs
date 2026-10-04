namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Persistence.Journal
open System.Threading.Tasks

[<RequireQualifiedAccess>]
module AttentionConcernJournalAdapter =
    val forAttention: journal: AgentJournal -> AttentionJournalPort
    val forConcern: journal: AgentJournal -> ConcernJournalPort

    /// concern-routing-006: retire every active mailbox owned by the given
    /// participant session through a durable MailboxRetired append per mailbox.
    val retireMailboxesOf:
        journal: AgentJournal ->
        owner: SessionId ->
        providerRun: ProviderRunIdentity option ->
            Task<Result<unit, string>>
