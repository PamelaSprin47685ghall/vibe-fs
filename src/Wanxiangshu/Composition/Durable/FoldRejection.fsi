namespace Wanxiangshu.Composition.Durable

type FoldRejection =
    { Fact: string
      Reason: string }

module FoldRejection =
    val reject: factName: string -> reason: string -> Result<'a, FoldRejection>
