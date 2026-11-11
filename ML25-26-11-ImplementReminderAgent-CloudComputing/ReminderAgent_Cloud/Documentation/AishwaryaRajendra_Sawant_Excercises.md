# My Exercises — Aishwarya Sawant

Repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026 (branch `AishwaryaS`)
All work referenced below lives under `CC_MyWork/` on that branch.

---

#### Exercise I — Scripts & Azure CLI

**What was done / why / how:**
Installed the Azure CLI and practiced the core subscription, login and resource-group commands to get comfortable with the CLI before using it for later exercises. Ran the commands directly against the "Azure for Students" subscription, then deleted the test resource group afterwards to avoid leaving resources behind.

| Command                                                    | Purpose                           |
| ---------------------------------------------------------- | --------------------------------- |
| `az`                                                       | List all available command groups |
| `az version`                                               | Check installed CLI version       |
| `az login`                                                 | Sign in to Azure                  |
| `az account show`                                          | Show the active subscription      |
| `az account list --output table`                           | List all subscriptions            |
| `az group create --name RG-ex1delme --location westeurope` | Create a resource group           |
| `az group list --output table`                             | List resource groups              |
| `az group show --name RG-ex1delme`                         | Show resource group details       |
| `az group delete --name RG-ex1delme`                       | Delete the resource group         |
| `az account list-locations --output table`                 | List all Azure regions            |

**Outcome:** Resource group `RG-ex1delme` was created in `westeurope`, verified, and successfully deleted — confirmed by re-running `az group list`. Full console transcript with outputs is committed in the repo.

**Script / output files:**

- https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex1/script.bat
- https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex1/az
- Screenshot: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex1/AzureResourcesUsingAzureCLI-modulepassed.png

---

#### Exercise 2-a — Docker with Dockerhub

Two apps were containerized and pushed to Docker Hub: a console app and a web app.

**1. Dockerfile URLs:**

- Console app: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex2/ConsoleApp1/ConsoleApp1/Dockerfile

**2. Published Docker Hub images:**

- Console app: https://hub.docker.com/repository/docker/aishwaryasawant/dockertest/general

**Commands used:**

```bash
# Console app
docker build -t dockerapp .
docker run dockerapp
docker login
docker tag dockerapp aishwaryasawant/dockertest:latest
docker push aishwaryasawant/dockertest:latest

# Web app
docker build -t webapplication1:latest .
docker run -p 8080:8080 webapplication1:latest
docker tag webapplication1 aishwaryasawant/webapp:latest
docker push aishwaryasawant/webapp:latest
```

Full command log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex2/DockerHubLink_and_CommandsUsed.md

---

#### Exercise 2-b — Docker with Azure

A .NET console app (`ConsoleApp1`) was built, pushed to Azure Container Registry, and run via Azure Container Instance.

> **Note:** The resource group hosting this ACR/ACI has since been **deleted to save Azure Student subscription credits**. The registry/image below is therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Private image in Azure Container Registry (no longer live):**

- Registry: `sample2b.azurecr.io`
- Image: `sample2b.azurecr.io/consoleapp1:v1`

**Commands used:**

```bash
docker build -t consoleapp1 .
docker run -it --rm consoleapp1:v1
docker login sample2b.azurecr.io
docker tag consoleapp1:latest sample2b.azurecr.io/consoleapp1:v1
docker push sample2b.azurecr.io/consoleapp1:v1
```

**Outcome:** Image pushed successfully to ACR, then deployed as an Azure Container Instance running from that ACR image; container logs confirmed `Hello, World!` output.

**Screenshots:**

- ACR repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex2/ACR-consoleapp1.png
- ACI run output: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex2/ACI-run-consoleapp1.png
- Full command log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex2/azurecontainer_script.bat

---

#### Exercise 3a — Host a web application with Azure App Service

Application: **BestBikeApp** — an ASP.NET MVC app deployed via ZIP deployment.

> **Note:** The `RG-BIKE` resource group hosting this App Service has since been **deleted to save Azure Student subscription credits**. The public URL below is therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Public URL (no longer live):**
https://aish-bikeapp.azurewebsites.net/

**2. Source code URL:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/AishwaryaS/CC_MyWork/Ex3/BestBikeApp

**3. Azure CLI scripts used:**

```bash
dotnet new mvc --framework net10.0 --name BestBikeApp
dotnet publish -c Release -o pub
Compress-Archive -Path .\pub\* -DestinationPath site.zip

az login
az group create --name RG-BIKE --location germanywestcentral
az appservice plan create --resource-group RG-BIKE --name BikePlan --sku S1
az webapp create --resource-group RG-BIKE --plan BikePlan --name Aish-BikeApp --runtime "dotnet:10"
az webapp deployment source config-zip --src site.zip --resource-group RG-BIKE --name Aish-BikeApp
az webapp restart --resource-group RG-BIKE --name Aish-BikeApp
```

**Outcome:** Deployment succeeded (`provisioningState: Succeeded`), app restarted and reachable publicly at the URL above.

Full transcript: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/script.bat

Image - https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/aish-bikeapp.azurewebsites.png

---

#### Exercise 3b — Deploy and run the containerized app in App Service

Application: **BestCarApp** — deployed as a Docker container running in Azure App Service (Linux, container-based).

> **Note:** The `RG-CAR` resource group hosting this ACR/App Service has since been **deleted to save Azure Student subscription credits**. The image and public URL below are therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Docker image URL (no longer live):**
Azure Container Registry `bestcaracraish`, repository `bestcarapp`, tag `v1`.

**2. Public URL of the running application (no longer live):**
https://bestcarapp-aish.azurewebsites.net/

**Outcome:** `az webapp log tail` confirms the container started successfully ("Site started", `Now listening on: http://0.0.0.0:8181` inside the container) and the App Service state shows **Running**.

**Screenshots:**

- ACR repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/Ex3b/AzureContainerRegistryRepository.png
- App Service status (Running): https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/Ex3b/AzureAppServiceStatus-Running.png
- App Service logs: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/Ex3b/AzureAppServiceLogs.png
- Running application: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/Ex3b/RunningAzureApplication.png
- Verification log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex3/AzureCLILogVerification.bat
- Source: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/AishwaryaS/CC_MyWork/Ex3/Ex3b/BestCarApp

---

#### Exercise 4 — MCP

**What was done:**

- Built an MCP server exposing "monkey" data tools (`GetMonkeys`, `GetMonkey`) over **STDIO transport** (`ConsoleApp1`).
- Built the equivalent MCP server over **HTTP transport** (`SampleMCPHttp`), using `ModelContextProtocol.AspNetCore` and `app.MapMcp()`.
- Built an AI agent client (`SampleAgent`) that connects to the MCP server (HTTP mode, `httpHostedMcpTool`), loads the exposed tools, and runs an interactive conversation loop where the agent calls the MCP tools to answer questions.

**Source code:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/AishwaryaS/CC_MyWork/Ex4

**Screenshot:** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/AishwaryaS/CC_MyWork/Ex4/CurrencyTool.png

---

#### Exercise 5 — Blob Storage

Storage account: **aishwaryablobstorage**

> **Note:** The resource group hosting this storage account has since been **deleted to save Azure Student subscription credits**. The URLs and SAS links below are therefore no longer live/accessible — screenshots were taken beforehand as evidence of the completed exercise (see below).

**Container URLs (no longer live):**

1. Input — https://aishwaryablobstorage.blob.core.windows.net/input
2. Output — https://aishwaryablobstorage.blob.core.windows.net/output
3. Test — https://aishwaryablobstorage.blob.core.windows.net/test

**SAS URLs (Test container, issued for ~1 year — no longer live since the account was deleted):**

1. `test-file-1.txt` — https://aishwaryablobstorage.blob.core.windows.net/test/test-file-1.txt?sp=r&st=2026-06-01T16:07:07Z&se=2027-06-01T00:22:07Z&spr=https&sv=2026-02-06&sr=b&sig=Y%2FH%2FrhjwNxkV5M%2F5WqcRDHS5JghjFE4FrmU8SpbU4to%3D
2. `test-file-2.csv` — https://aishwaryablobstorage.blob.core.windows.net/test/test-file-2.csv?sp=r&st=2026-06-03T05:25:16Z&se=2027-06-03T13:40:16Z&spr=https&sv=2026-02-06&sr=b&sig=mMI%2Bviv1ZF69nybN6jQUKXOIgpd%2BuMV%2F6Hajz5QZWIE%3D
3. `test-image.png` — https://aishwaryablobstorage.blob.core.windows.net/test/test-image.png?sp=r&st=2026-06-03T05:25:55Z&se=2027-06-03T13:40:55Z&spr=https&sv=2026-02-06&sr=b&sig=LvCOFU9LjnRyA6Ua%2FiplfDK5pd5Xs7Alh3M7R%2Ba3skw%3D

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/AishwaryaS/CC_MyWork/Ex5/screenshots

---

#### Exercise 6 — Table Storage

Storage account: **aishwaryatablestorage**
Table: **TableExercise**

> **Note:** The resource group hosting this storage account has since been **deleted to save Azure Student subscription credits**. The URL below is no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

- Table Storage URL (no longer live): https://aishwaryatablestorage.table.core.windows.net/TableExercise

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/AishwaryaS/CC_MyWork/Ex6/images

---

#### Exercise 7 — Queue Storage

Storage account: **aishwaryaqueuestorage**
Queue: **queueexercise**

> **Note:** The resource group hosting this storage account has since been **deleted to save Azure Student subscription credits**. The URL below is no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

- Queue Storage URL (no longer live): https://aishwaryaqueuestorage.queue.core.windows.net/queueexercise

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/AishwaryaS/CC_MyWork/Ex7/images

---
