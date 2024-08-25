namespace ElmishStore

open Fable.Core
open Feliz
open ElmishStore

[<Erase>]
type React =

    /// Provides a current snapshot of the store's state selected by the selector function.
    /// NOTE: Selector returning value needs to be referentially stable.
    static member inline useElmishStore(store, selector: 'model -> 'a) =
        Hooks.useElmishStore store selector

    /// Provides a current snapshot of the store's state selected by the selector function.
    /// The result of the selector function is memoized and compared with isEqual function.
    static member inline useElmishStoreMemoized(store, selector: 'model -> 'a, isEqual) =
        Hooks.useElmishStoreMemoizedWithCustomEquality store selector isEqual

    /// Provides a current snapshot of the store's state selected by the selector function.
    /// The result of the selector function is memoized and compared with structural equality.
    static member inline useElmishStoreMemoized(store, selector: 'model -> 'a) =
        Hooks.useElmishStoreMemoized store selector