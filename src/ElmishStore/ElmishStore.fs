namespace ElmishStore


open Elmish
open Fable.Core
open ElmishStore
open System.Collections.Generic

type ElmishStore<'model, 'msg> = {
  GetModel: unit -> 'model
  Dispatch: 'msg -> unit
  Subscribe: UseSyncExternalStoreSubscribe
}

type private StoreState<'arg, 'model, 'msg> = {
  Store: ElmishStore<'model, 'msg>
  SetTermination: bool -> unit
}

module ElmishStore =

  let mutable private stores: Dictionary<string, obj> = Dictionary<string, obj>()

  let private initiate
    uniqueName
    (arg: 'arg)
    (program: Program<'arg, 'model, 'msg, unit>)
    (getState: unit -> 'model option)
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

    stores[uniqueName] <- box storeState
    store

  let createStoreWith uniqueName (arg: 'arg) (program: Program<'arg, 'model, 'msg, unit>) =

    let getState =
      if stores.ContainsKey(uniqueName) then
        let storeState = stores[uniqueName] |> unbox<StoreState<'arg, 'model, 'msg>>
        storeState.SetTermination true
        (fun () -> Some(storeState.Store.GetModel()))
      else
        (fun () -> None)

    initiate uniqueName arg program getState

  let inline createStore uniqueName program : ElmishStore<'model, 'msg> =
    createStoreWith uniqueName () program

[<Erase>]
type ElmishStore =

  static member Create
    (
      program: Program<'arg, 'model, 'msg, unit>,
      arg: 'arg,
      uniqueName
    ) : ElmishStore<'model, 'msg> =
    ElmishStore.createStoreWith uniqueName arg program

  static member inline Create(program: Program<unit, 'model, 'msg, unit>, uniqueName) =
    ElmishStore.Create(program, (), uniqueName)

  static member inline Create
    (
      init: 'arg -> 'model * Cmd<'msg>,
      update: 'msg -> 'model -> 'model * Cmd<'msg>,
      arg: 'arg,
      uniqueName
    ) =
    ElmishStore.Create((Program.mkProgram init update (fun _ _ -> ())), arg, uniqueName)

  static member inline Create
    (
      init: unit -> 'model * Cmd<'msg>,
      update: 'msg -> 'model -> 'model * Cmd<'msg>,
      uniqueName
    ) =
    ElmishStore.Create(Program.mkProgram init update (fun _ _ -> ()), uniqueName)

  static member inline Create
    (
      init: 'model * Cmd<'msg>,
      update: 'msg -> 'model -> 'model * Cmd<'msg>,
      uniqueName
    ) =
    ElmishStore.Create(Program.mkProgram (fun () -> init) update (fun _ _ -> ()), uniqueName)
