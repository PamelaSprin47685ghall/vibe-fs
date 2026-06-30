module Wanxiangshu.Methodology.Catalog

open Wanxiangshu.Methodology.SchemaCommon

let all: MethodologyEntry list =
    Catalog1.entries
    @ Catalog2.entries
    @ Catalog3.entries
    @ Catalog4.entries
