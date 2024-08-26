namespace ElmishStore


open Elmish
open Fable.Core
open ElmishStore
open System.Collections.Generic

type ElmishStore<'Model, 'Msg> = {
    GetModel: unit -> 'Model
    Dispatch: 'Msg -> unit
    Subscribe: UseSyncExternalStoreSubscribe
}

type private StoreState<'Arg, 'Model, 'Msg> = {
    Store: ElmishStore<'Model, 'Msg>
    SetTermination: bool -> unit
}

type internal StoreKey<'T when 'T : equality> = 'T

type IElmishProgramBasedStoreInitializer<'StoreKey when StoreKey<'StoreKey>> =
    abstract CreateStore :
        program: Program<unit, 'Model, 'Msg, unit> *
        storeKey: 'StoreKey ->
            ElmishStore<'Model, 'Msg>
    abstract CreateStoreWithArg :
        program: Program<'Arg, 'Model, 'Msg, unit> *
        storeKey: 'StoreKey *
        arg: 'Arg ->
            ElmishStore<'Model, 'Msg>

type IElmishFunctionsBasedStoreInitializer<'StoreKey when StoreKey<'StoreKey>> =
    abstract CreateStoreWithFunctions :
        init: (unit -> 'Model * Cmd<'Msg>) *
        update: ('Msg -> 'Model -> 'Model * Cmd<'Msg>) *
        storeKey: 'StoreKey ->
            ElmishStore<'Model, 'Msg>
    abstract CreateStoreWithFunctionsAndArg :
        init: ('Arg -> 'Model * Cmd<'Msg>) *
        update: ('Msg -> 'Model -> 'Model * Cmd<'Msg>) *
        storeKey: 'StoreKey *
        arg: 'Arg ->
            ElmishStore<'Model, 'Msg>

type IElmishStoreInitializer<'StoreKey when StoreKey<'StoreKey>> =
    inherit IElmishFunctionsBasedStoreInitializer<'StoreKey>
    inherit IElmishProgramBasedStoreInitializer<'StoreKey>

type IElmishStoreAccessor<'StoreKey when StoreKey<'StoreKey>> =
    abstract GetStore<'Model, 'Msg when StoreKey<'StoreKey>> :
        storeKey: 'StoreKey -> ElmishStore<'Model, 'Msg>
    abstract TryGetStore<'Model, 'Msg when StoreKey<'StoreKey>> :
        storeKey: 'StoreKey -> ElmishStore<'Model, 'Msg> option

type IElmishStoresHost<'StoreKey when StoreKey<'StoreKey>> =
    inherit IElmishStoreInitializer<'StoreKey>
    inherit IElmishStoreAccessor<'StoreKey>


type ElmishStoresHost<'StoreKey when StoreKey<'StoreKey>>() =

    let mutable stores: Dictionary<'StoreKey, obj> = Dictionary<'StoreKey, obj>()

    let initiate
        storeKey
        (arg: 'Arg)
        (program: Program<'Arg, 'Model, 'Msg, unit>)
        (getState: unit -> 'Model option)
        =
        let mutable state = getState ()
        let mutable finalDispatch = None
        let mutable shouldTerminate = false

        let setTermination should = shouldTerminate <- should

        let dispatch msg =
            match finalDispatch with
            | Some finalDispatch -> finalDispatch msg
            | None -> failwith "You're using initial dispatch. That shouldn't happen."

        let subscribers = ResizeArray<unit -> unit>()

        let subscribe callback =
            subscribers.Add(callback)
            fun () -> subscribers.Remove(callback) |> ignore

        let mapSetState setState model dispatch =
            setState model dispatch
            let oldModel = state
            state <- Some model
            finalDispatch <- Some dispatch
            // Skip re-renders if model hasn't changed
            if not (obj.ReferenceEquals(model, oldModel)) then
                subscribers |> Seq.iter (fun callback -> callback ())

        let mapInit userInit arg =
            if state.IsSome then state.Value, Cmd.none else userInit arg

        let mapTermination (predicate, terminate) =
            let pred msg = predicate msg || shouldTerminate
            pred, terminate

        program
        |> Program.map mapInit id id mapSetState id mapTermination
        |> Program.runWith arg

        let getState () =
            match state with
            | Some state -> state
            | None -> failwith "State is not initialized. That shouldn't happen."

        let store = {
            GetModel = getState
            Dispatch = dispatch
            Subscribe = UseSyncExternalStoreSubscribe subscribe
        }

        let storeState = {
            Store = store
            SetTermination = setTermination
        }

        stores[storeKey] <- box storeState
        store

    let createStoreWith storeKey (arg: 'Arg) (program: Program<'Arg, 'Model, 'Msg, unit>) =

        let getState =
            if stores.ContainsKey(storeKey) then
                let storeState = stores[storeKey] |> unbox<StoreState<'Arg, 'Model, 'Msg>>
                storeState.SetTermination true
                (fun () -> Some(storeState.Store.GetModel()))
            else
                (fun () -> None)

        initiate storeKey arg program getState

    let createStore storeKey program : ElmishStore<'Model, 'Msg> =
        createStoreWith storeKey () program

    interface IElmishStoresHost<'StoreKey> with

        member this.CreateStore(program: Program<unit, 'Model, 'Msg, unit>, storeKey: StoreKey<'StoreKey>) =
            createStore storeKey program

        member this.CreateStoreWithArg(program: Program<'Arg, 'Model, 'Msg, unit>, storeKey, arg) =
            createStoreWith storeKey arg program

        member this.CreateStoreWithFunctions(
                init: unit -> 'Model * Cmd<'Msg>,
                update: 'Msg -> 'Model -> 'Model * Cmd<'Msg>,
                storeKey
            ) =
                let program = Program.mkProgram init update (fun _ _ -> ())
                (this :> IElmishStoresHost<'StoreKey>).CreateStore(program, storeKey)

        member this.CreateStoreWithFunctionsAndArg(
                init: 'Arg -> 'Model * Cmd<'Msg>,
                update: 'Msg -> 'Model -> 'Model * Cmd<'Msg>,
                storeKey,
                arg: 'Arg
            ) =
            let program = Program.mkProgram init update (fun _ _ -> ())
            (this :> IElmishStoresHost<'StoreKey>).CreateStoreWithArg(program, storeKey, arg)

        member this.GetStore<'Model, 'Msg>(storeKey) =
            try
                let storeState =
                    stores[storeKey] |> unbox<StoreState<_, 'Model, 'Msg>>
                storeState.Store
            with
            | :? KeyNotFoundException as e ->
                failwithf
                    $"Elmish Store with key %A{storeKey} hasn't been found. Store has to be created before it can be accessed."

        member this.TryGetStore<'Model, 'Msg> storeKey =
            match stores.TryGetValue storeKey with
            | true, s ->
                let storeState = unbox<StoreState<_, 'Model, 'Msg>> s
                Some storeState.Store
            | false, _ -> None

    // The following functions have to be inlined because JS doesn't support methods overloading.

    member inline this.CreateStore(program: Program<unit, 'Model, 'Msg, unit>, storeKey) =
        (this :> IElmishStoresHost<'StoreKey>).CreateStore(program, storeKey)

    member inline this.CreateStore(program: Program<'Arg, 'Model, 'Msg, unit>, storeKey, arg) =
        (this :> IElmishStoresHost<'StoreKey>).CreateStoreWithArg(program, storeKey, arg)

    member inline this.CreateStore(
            init: unit -> 'Model * Cmd<'Msg>,
            update: 'Msg -> 'Model -> 'Model * Cmd<'Msg>,
            storeKey
        ) =
        (this :> IElmishStoresHost<'StoreKey>).CreateStoreWithFunctions(init, update, storeKey)

    member inline this.CreateStore(
            init: 'Arg -> 'Model * Cmd<'Msg>,
            update: 'Msg -> 'Model -> 'Model * Cmd<'Msg>,
            storeKey,
            arg: 'Arg
        ) =
        let program = Program.mkProgram init update (fun _ _ -> ())
        (this :> IElmishStoresHost<'StoreKey>).CreateStoreWithArg(program, storeKey, arg)

    member inline this.GetStore storeKey =
        (this :> IElmishStoreAccessor<'StoreKey>).GetStore storeKey

    member inline this.TryGetStore storeKey =
        (this :> IElmishStoreAccessor<'StoreKey>).TryGetStore storeKey


[<Erase>]
type ElmishStore =

    static member inline create
        (host: IElmishStoresHost<'StoreKey>)
        storeKey
        (program: Program<unit, 'Model, 'Msg, unit>)
        =
        host.CreateStore(program, storeKey)

    static member inline createWithArg
        (host: IElmishStoresHost<'StoreKey>)
        storeKey
        arg
        (program: Program<'Arg, 'Model, 'Msg, unit>)
        =
        host.CreateStoreWithArg(program, storeKey, arg)

    static member inline createWithFunctions
        (host: IElmishStoresHost<'StoreKey>)
        storeKey
        (init: unit -> 'Model * Cmd<'Msg>)
        update
        =
        host.CreateStoreWithFunctions(init, update, storeKey)

    static member inline createWithFunctionsAndArg
        (host: IElmishStoresHost<'StoreKey>)
        storeKey
        arg
        (init: 'Arg -> 'Model * Cmd<'Msg>)
        update
        =
        host.CreateStoreWithFunctionsAndArg(init, update, storeKey, arg)


[<AutoOpen; Erase>]
module Extensions =

    type IElmishStoresHost<'StoreKey when StoreKey<'StoreKey>> with

        member inline this.create
            storeKey
            (program: Program<unit, 'Model, 'Msg, unit>)
            =
            this.CreateStore(program, storeKey)

        member inline this.createWithArg
            storeKey
            arg
            (program: Program<'Arg, 'Model, 'Msg, unit>)
            =
            this.CreateStoreWithArg(program, storeKey, arg)

        member inline this.createWithFunctions
            storeKey
            (init: unit -> 'Model * Cmd<'Msg>)
            update
            =
            this.CreateStoreWithFunctions(init, update, storeKey)

        member inline this.createWithFunctionsAndArg
            storeKey
            arg
            (init: 'Arg -> 'Model * Cmd<'Msg>)
            update
            =
            this.CreateStoreWithFunctionsAndArg(init, update, storeKey, arg)
