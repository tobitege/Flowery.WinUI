## Flowery.WinUI.UnitTests

Plain MSTest tests for logic that needs no WinUI, such as the scheduler models and the sidebar search. A test that touches a WinUI type, including the statics of a control class, belongs in `Flowery.WinUI.RuntimeTests`.

### Run locally

```powershell
./scripts/test_windows.ps1 -Configuration Debug
```

The script runs these tests before the runtime tests, except with `-TestFilter`, which selects runtime tests; CI runs it too.
