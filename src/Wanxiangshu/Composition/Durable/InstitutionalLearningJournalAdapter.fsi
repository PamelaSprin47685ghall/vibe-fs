namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module InstitutionalLearningJournalAdapter =
    val forInstitutionalLearning: journal: AgentJournal -> InstitutionalLearningJournalPort
