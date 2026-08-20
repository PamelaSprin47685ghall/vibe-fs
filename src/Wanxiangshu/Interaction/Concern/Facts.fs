namespace Wanxiangshu.Interaction.Concern

open Wanxiangshu.Foundation.Identity

type ConcernPlacementBatch =
    { AnnouncedGenerations: string list
      DeliveredMessages: string list }

type ConcernFactCases =
    | MailboxSubscribed of
        {| Id: string
           Concern: string
           Generation: string
           OwnerSessionId: SessionId |}
    | MessagePublished of
        {| OccurrenceId: string
           Generation: string
           Id: string
           SenderSessionId: SessionId
           Message: string |}
    | MailboxRetired of
        {| Generation: string
           Id: string
           OwnerSessionId: SessionId |}
