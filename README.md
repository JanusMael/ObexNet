# ObexNet - DLL Blocking

**ObexNet** is a `.NET` console program, written in 100% `C#`, that can be used to prevent unwanted modules (e.g., `EDR` or monitoring libraries) from being loaded into a newly started process during process initialization or at runtime.

> NOTE: this started as a port of the code from the `GitHub` repository [dis0rder0x00/obex](https://github.com/dis0rder0x00/obex), originally written in C and which was limited to X64 architectures

## Features
- Spawns any process with arguments under debug control
- Blocks a configurable list of `DLL`s by name
- Works both for startup `DLL`s and dynamically loaded `DLL`s (via `LoadLibrary*`)
- Written in plain `C#` with no external dependencies

## Usage
```shell
ObexNet.exe "<command_to_run>" [x.dll,y.dll,z.dll]
```
- If no coma-delimited `DLL` list is provided, a default blocklist is used

  - > At the time of writing just `amsi.dll` (i.e. `Antimalware Scan Interface`)

- `DLL` names are case-insensitive.

#### PowerShell & child process example

```shell
ObexNet.exe "powershell.exe /C whoami" amsi.dll
```

## How Does It Work?
Besides parsing `CLI` arguments the application does the following (in a rough overview):
![](https://github.com/dis0rder0x00/obex/raw/main/images/flow.png)

#### Debug Loop Logic

The `C#` port faithfully recreates the original `C` debug loop state machine:

1) <u>Wait</u> for a debug event
2) <u>Check</u> for `LOAD_DLL_DEBUG_EVENT`
3) <u>Hook</u> `LdrLoadDll` by writing an `INT3` instruction (`0xCC`) to the remote address
4) <u>Catch</u> `EXCEPTION_DEBUG_EVENT` (specifically `EXCEPTION_BREAKPOINT`)
5) <u>Handle the hook</u>:
   - Read the `DLL` name from the remote `R8` register
   - Check if it matches the blocklist
   - If blocked, set the return value (`RAX`) to `STATUS_DLL_NOT_FOUND` and zero out the base address (`R9`)
6) <u>Single Step</u>: Temporarily restore the original instruction, set the `Trap Flag` (`TF`), and single-step the thread
7) <u>Re-arm</u>: Catch the `EXCEPTION_SINGLE_STEP`, re-write the `INT3` instruction, and continue

## Screenshot
The screenshot shows `ObexNet` spawning `powershell.exe` with the default blocklist: [ `amsi.dll` ]
Additionally you can see the spawned process’s module list to verify that `amsi.dll` was not loaded

![](https://github.com/dis0rder0x00/obex/raw/main/images/1.png)

## Extended Features 

In addition to the features from the original `obex` program, the following have been added to this implementation

- This single program now supports `ARM64` in addition to `X64`
- **"Silent Mode" / JSON Output**
  - **Feature**: Added flags `--silent` and `--json` flag
  - **Use Case**: For integration into automated security pipelines (CI/CD) or SIEM ingestion, structured JSON output (e.g., `{"event": "blocked", "dll": "amsi.dll", "timestamp": "..."}`) is supported
- **Wildcard / Regex Blocking**
  - **Feature**: Support wildcards (e.g., `api-ms-win-core-*.dll`) or Regex patterns in the block list
  - **Use Case**: Modern Windows applications load dozens of `api-ms-win*` or `ext-ms-win*` virtual `DLL`s. Regex allows blocking entire families of `DLL`s without listing them individually
- **Interactive Mode**
  - **Feature**: When a DLL load is intercepted, pauses execution and prompt the user: `[?] Allow loading 'unknown.dll'? (y/n)`
  - **Use Case**: Useful for manual malware analysis where you want to selectively allow/deny dependencies to see how the malware behaves (e.g., allowing network `DLL`s but blocking encryption `DLL`s)
- **"Fake Success" Injection**
  - **Feature**: Instead of returning `STATUS_DLL_NOT_FOUND` (which crashes `rundll32`), allow the load to "fail" gracefully or return a handle to a **dummy DLL**
  - **Use Case**: Some malware checks if `amsi.dll` failed to load and terminates if so. If you return a handle to a dummy `DLL` that exports the same functions (like `AmsiScanBuffer`) but does nothing, the malware proceeds, believing protection is active
- **Child Process Follow-up**
  - **Feature**: Automatically attach to child processes spawned by the target
  - **Use Case**: If `powershell.exe` launches `cmd.exe`, the original debugger only watches the parent. Following child processes is critical for analyzing complex infection chains
