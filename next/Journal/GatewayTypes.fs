namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

type IGateway =
    abstract RuntimeId: RuntimeId
    abstract ProjectionSet: ProjectionSet
    abstract Append: StreamId -> TurnId option -> Fact -> CommitResult<Envelope>
