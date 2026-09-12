namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module AttentionConcernJournalAdapter =
    val forAttention: journal: AgentJournal -> AttentionJournalPort
    val forConcern: journal: AgentJournal -> ConcernJournalPort
