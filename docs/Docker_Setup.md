# Run RaceDay MVC in Docker Desktop (Windows)

This local demonstration runs only MVC in a Linux container. The API runs on
Windows and retains access to LocalDB and Azure User Secrets. The container calls
`http://host.docker.internal:5000/`; `localhost` inside it refers to the container,
not your Windows API. No database or Azure connection strings go in the MVC image.

## 1. Check Docker

Install [Docker Desktop for Windows](https://docs.docker.com/desktop/setup/install/windows-install/)
if needed, using its WSL 2 backend and Linux containers. Open Docker Desktop and
wait until its engine is running. In PowerShell:

```powershell
docker version
docker info --format '{{.OSType}}'
```

The first command should show both Client and Server; the second should show
`linux`. Follow Docker Desktop's installation prompts if WSL needs updating or a
restart is required.

## 2. Start the API on Windows

Stop the Visual Studio debugging session. Open PowerShell in the repository:

```powershell
Set-Location (git rev-parse --show-toplevel)
dotnet run --project RaceDay/RaceDay.csproj --launch-profile docker-demo
```

Leave this terminal running. Check `http://localhost:5000/swagger/index.html`.
The profile explicitly uses Development, binds HTTP port 5000 on host interfaces,
and enables `LocalDockerDemo:AllowHttp`. This makes API sessions work over the
local HTTP demo connection. Outside this explicit mode HTTPS redirection and
secure API cookies remain enabled; enabling demo mode in Production fails startup.
Your existing API User Secrets still load in Development.

Use demo accounts on your own trusted machine/network. Binding `0.0.0.0` can make
the API reachable on other host interfaces. Do not enable public-network firewall
access; if Windows blocks Docker's connection, allow only the required private
Docker/host traffic. Stop the API when the demonstration is finished. This HTTP
setup is not an Internet deployment configuration.

## 3. Build MVC (a second PowerShell window)

Run from the repository root, where `RaceDay.sln` is located:

```powershell
Set-Location (git rev-parse --show-toplevel)
docker build -f RaceDay.Mvc/Dockerfile -t raceday-mvc:part3 .
```

Keep the final dot: it supplies the repository as build context, including the
shared Contracts project. The SDK stage restores/publishes MVC; the final stage
contains the ASP.NET runtime and published files only. `USER $APP_UID` runs the app
as the image's non-root user. `.dockerignore` excludes build outputs and common
secret files. Never put secrets in tracked appsettings files.

## 4. Run the container

```powershell
docker run -d --name raceday-mvc -p 127.0.0.1:8080:8080 -e ASPNETCORE_ENVIRONMENT=Development -e LocalDockerDemo__AllowHttp=true -e ApiSettings__BaseUrl=http://host.docker.internal:5000/ raceday-mvc:part3
docker ps
docker logs raceday-mvc
```

Open **http://localhost:8080** (HTTP, not HTTPS). Port 8080 on your PC maps to
port 8080 in the container. Publishing to 127.0.0.1 keeps this MVC demo local to
your PC. The image itself does not force Development: the explicit run arguments
enable the local demo. Normal deployments need HTTPS configuration.

## 5. Verify the complete flow

Keep the Windows API terminal running, but leave the Visual Studio MVC app stopped.
Use a fresh browser session for the Docker test and log in again.

- Home page and mobile navigation render at port 8080.
- Participant login succeeds; Browse events, My Events, My Results and My Profile load.
- Organiser login succeeds; their dashboard and event/category/enrolment/result pages load.
- Participants cannot access Organiser pages.
- Upload a profile photo/banner and verify it appears (API User Secrets supply Azure access).
- Log out and confirm protected pages require login again.
- Inspect `docker ps` and `docker logs raceday-mvc` for evidence.

A working home page alone does not prove the container can authenticate with the
API. Record at least one protected page after a successful login. Capture build
success, container/port listing, browser page at port 8080 and the latest CI jobs.

## Stop, restart and rebuild

```powershell
docker stop raceday-mvc
docker start raceday-mvc
```

Sessions are in memory, so log in again after restarting. To apply code changes,
stop/remove this named demo container, rebuild the image, and repeat the run command:

```powershell
docker stop raceday-mvc
docker rm raceday-mvc
docker build -f RaceDay.Mvc/Dockerfile -t raceday-mvc:part3 .
```

Removing this MVC container does not delete your Windows SQL database or Azure blobs.
Stop the API with Ctrl+C in its terminal. Return to Visual Studio's **https** profile
for your normal development workflow.

## Troubleshooting

| Symptom | Check |
|---|---|
| Docker command not found | Install Docker Desktop and reopen PowerShell. |
| Cannot connect to Docker engine | Start Docker Desktop; wait for engine readiness. |
| Container name already exists | Use `docker start raceday-mvc`, or stop/remove it before a rebuilt run. |
| Host port 8080 is busy | Use `-p 127.0.0.1:8081:8080` and open http://localhost:8081. |
| Home works but login fails | Keep API running with `docker-demo`; verify API address and private firewall access. |
| Redirect or secure-cookie errors | Include both demo environment arguments in `docker run` and use the API `docker-demo` profile. |
| Image upload fails | Check the Windows API's Azure User Secrets, container privacy and applied migrations. |
| Old UI appears | Rebuild image and replace the container; `docker start` reuses the old container. |

## What is automated

The .NET tests check HTTP login/session persistence in demo mode, normal HTTPS
redirection and rejection of demo mode in Production. CI builds the Docker image
and checks its home page. CI does not run your Windows LocalDB/Azure integration;
complete the protected-page and upload checks above locally. The coding workspace
has no Docker engine, so actual image build/run must be verified by CI and Docker
Desktop; do not describe it as verified until those checks pass.

References: [Docker Desktop host networking](https://docs.docker.com/desktop/features/networking/networking-how-tos/),
[.NET 8 container ports](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port),
[.NET non-root containers](https://devblogs.microsoft.com/dotnet/secure-your-container-build-and-publish-with-dotnet-8/).
