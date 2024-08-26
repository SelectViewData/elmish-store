module ElmishStore.Hooks

open Fable.Core
open Feliz
open ElmishStore


/// Provides a current snapshot of the store's state selected by the selector function.
/// NOTE: Selector returning value needs to be referentially stable.
[<Hook>]
let useElmishStore store (selector: 'model -> 'a) =
    ReactBindings.useSyncExternalStore (
        store.Subscribe,
        React.useCallback (
            (fun () -> store.GetModel() |> selector),
            [| box store; box selector |]
        )
    )

/// Provides a current snapshot of the store's state selected by the selector function.
/// The result of the selector function is memoized and compared with isEqual function.
[<Hook>]
let useElmishStoreMemoizedWithCustomEquality store (selector: 'model -> 'a) isEqual =
    ReactBindings.useSyncExternalStoreWithSelector (
        store.Subscribe,
        React.useCallback(
            (fun () -> store.GetModel()),
            [| box store; box selector |]
        ),
        selector,
        isEqual
    )

/// Provides a current snapshot of the store's state selected by the selector function.
/// The result of the selector function is memoized and compared with structural equality.
[<Hook>]
let useElmishStoreMemoized store (selector: 'model -> 'a) =
    ReactBindings.useSyncExternalStoreWithSelector (
        store.Subscribe,
        React.useCallback(
            (fun () -> store.GetModel()),
            [| box store; box selector |]
        ),
        selector,
        (=)
    )

