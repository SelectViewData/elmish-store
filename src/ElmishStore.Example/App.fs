module ElmishStore.Example.App

open Feliz

ReactDOM
  .createRoot(Browser.Dom.document.getElementById "elmish-app")
  .render (React.strictMode [ View.AppView() ])
