# Local Print Agent (in-process)

This folder is the local-print-agent code, embedded inside POS-Backend.
It activates only when `PrintAgent:Enabled = true`. **On Azure App Service
the flag is false → none of this code runs.**

## Why it exists

The Azure App Service sandbox cannot open TCP sockets to private LAN IPs
(WSAEACCES). Printers in your store sit on `192.168.x.x`. The agent is
the bridge: it runs *inside* the store LAN, polls the cloud for jobs
flagged with `UseLocalAgent = true`, and prints them — over TCP for
network printers, over the Windows print spooler (RAW datatype) for USB
printers.

There is no way to remove the in-store component. This is a network-layer
constraint, not a code issue.

## Operating modes — same publish artifact, different config

```
publish/  (one folder, one binary set)
   │
   ├──► Azure App Service                         (PrintAgent__Enabled=false)
   │       cloud API + DB + everything you have today
   │       agent code is dormant — never executes
   │
   └──► One Windows PC inside the store           (PrintAgent__Enabled=true)
           same exe, same dlls
           polls sandobox.net every 2s
           prints to LAN/USB
```

The cloud-side `PrintQueueProcessorBackgroundService` already filters out
printers with `UseLocalAgent = true`, so when an admin flips that flag on
a printer, the cloud worker stops touching it and only the agent will
claim its jobs.

## Configuration

Add to `appsettings.json` on the store PC (don't put real values in the
Azure-deployed copy):

```json
{
  "PrintAgent": {
    "Enabled": true,
    "BaseUrl": "https://sandobox.net",
    "TenantId": "<your tenant guid>",
    "Username": "print-agent",
    "Password": "<strong password>",
    "PollIntervalSeconds": 2,
    "BatchSize": 5
  }
}
```

Or use environment variables (preferred for secrets — no plain-text
password on disk):

```
PrintAgent__Enabled=true
PrintAgent__BaseUrl=https://sandobox.net
PrintAgent__TenantId=<guid>
PrintAgent__Username=print-agent
PrintAgent__Password=<strong password>
```

Create the `print-agent` user once via the admin panel (role: **Admin**;
the agent endpoints are AdminOnly).

## Deploying to a store PC

```powershell
# On a build machine with disk space:
cd POS-Backend
dotnet publish -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true -o publish

# Copy the publish folder to the store PC, e.g. C:\POSBackend\
# Edit appsettings.json (or set environment variables) on the store PC.

# Register as a Windows service:
sc.exe create POSPrintAgent binPath= "C:\POSBackend\RestaurantPos.Api.exe" start= auto
sc.exe failure POSPrintAgent reset= 86400 actions= restart/5000/restart/5000/restart/60000
sc.exe start POSPrintAgent
```

To stop / remove:

```powershell
sc.exe stop POSPrintAgent
sc.exe delete POSPrintAgent
```

## Verification

1. `Get-Service POSPrintAgent` → `Running`.
2. `Get-EventLog -LogName Application -Source POSPrintAgent -Newest 10`
   should show `[PrintAgent] Started — base=https://sandobox.net …`.
3. Place a test order via the cashier UI.
4. Within ~3 s the kitchen ticket prints (XP-80T) and the customer
   receipt prints (XP-80C).
5. `/ntxadminpos/printing → Print queue` shows both jobs `Completed`.

## Class layout

| File | Role |
|---|---|
| `PrintAgentSettings.cs` | Config DTO bound from `PrintAgent` section |
| `AgentApiClient.cs` | HTTP client with login + 401 retry |
| `LocalPrintAgentBackgroundService.cs` | Hosted service: poll → claim → print → report |
| `Writers/IAgentPrinterWriter.cs` | Writer abstraction |
| `Writers/AgentNetworkWriter.cs` | TCP/IP-9100 ESC/POS for LAN printers |
| `Writers/AgentWindowsSpoolerWriter.cs` | Win32 PInvoke for USB printers |

## Existing-flow safety guarantee

When `PrintAgent:Enabled = false` (the default; Azure deployment):

- Hosted service is NOT registered → never runs.
- HTTP client is NOT registered → no socket allocations.
- Writers are NOT registered → no native lib pinning.
- Cloud `PrintQueueProcessorBackgroundService` runs unchanged.
- Cloud `PrintDispatcher`, `KitchenRoutingService`, `KitchenTicketBuilder`
  run unchanged.
- Order create / payment / kitchen / receipt code paths are unchanged.

The behavior on Azure is bit-for-bit identical to the pre-agent build.
