# My Exercises — Bavana Jadala

Repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/Bavana (branch `Bavana`)
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

- https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-1/scritp.bat
- Screenshot: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-1/Completed_AzureCLI_module.png

---

#### Exercise 2-a — Docker with Dockerhub

Two apps were containerized and pushed to Docker Hub: a console app and a web app.

**1. Dockerfile URLs:**

- Console app: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-2/ConsoleApp1/ConsoleApp1/Dockerfile

**2. Published Docker Hub images:**

- Console app: https://hub.docker.com/repository/docker/bavana756/sampleexample/general

**Commands used:**

```bash
# Console app
docker build -t myjob:latest .
docker images
docker run myjob:latest
docker login
docker tag myjob:latest bavana756/sampleexample:latest
docker images
docker push bavana756/sampleexample:latest

# Web app
docker build -t webapplication1:latest .
docker run -p 8080:8080 webapplication1:latest
docker tag webapplication1 bavana/webapp:latest
docker push bavana/webapp:latest
```

Full command log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-2/ConsoleApp1/hublinkandcommand.md

---

#### Exercise 2-b — Docker with Azure

A .NET console app (`ConsoleApp2`) was built, pushed to Azure Container Registry, and run via Azure Container Instance.

> **Note:** The resource group hosting this ACR/ACI has since been **deleted to save Azure Student subscription credits**. The registry/image below is therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Private image in Azure Container Registry (no longer live):**

- Registry: `exer2b.azurecr.io`
- Image: `exer2b.azurecr.io/consoleapp2:v1`

**Commands used:**

```bash
docker build -t consoleapp2 .
docker run -it --rm consoleapp2:v1
docker login exer2b.azurecr.io
docker tag consoleapp2:latest exer2b.azurecr.io/consoleapp2:v1
docker push exer2b.azurecr.io/consoleapp2:v1
```

**Outcome:** Image pushed successfully to ACR, then deployed as an Azure Container Instance running from that ACR image; container logs confirmed `Hello, World!` output.

**Screenshots:**

- ACR repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-2/ConsoleApp2/ConsoleApp2/Container_registry1.png
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-2/ConsoleApp2/ConsoleApp2/Container_registry2.png
- ACI run output: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-2/ConsoleApp2/ConsoleApp2/Container_instances.png
- Full command log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/Ex-2/ConsoleApp2/ConsoleApp2/script.bat

---

#### Exercise 3a — Host a web application with Azure App Service

Application: **BestBikeApp** — an ASP.NET MVC app deployed via ZIP deployment.

> **Note:** The `RG-BIKE` resource group hosting this App Service has since been **deleted to save Azure Student subscription credits**. The public URL below is therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Public URL (no longer live):**
https://bestbikeapp-bavana.azurewebsites.net/

**2. Source code URL:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/Bavana/CC_Mywork/Ex-3/BestBikeApp


**3. Azure CLI scripts used:**

```bash
dotnet new mvc --framework net10.0 --name BestBikeApp
dotnet publish -c Release -o pub
Compress-Archive -Path .\pub\* -DestinationPath site.zip

az login
az group create --name RG-BIKE --location germanywestcentral
az appservice plan create --resource-group RG-BIKE --name BikePlan --sku S1
az webapp create --resource-group RG-BIKE --plan BikePlan --name bestbikeapp-bavana --runtime "dotnet:8"
az webapp deployment source config-zip --src site.zip --resource-group RG-BIKE --name bestbikeapp-bavana
az webapp restart --resource-group RG-BIKE --name bestbikeapp-bavana
```

**Outcome:** Deployment succeeded (`provisioningState: Succeeded`), app restarted and reachable publicly at the URL above.

Full transcript: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3a/BestBikeApp/script.bat

Image - https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3a/Image.png

---

#### Exercise 3b — Deploy and run the containerized app in App Service

Application: **BestBikeApp** — deployed as a Docker container running in Azure App Service (Linux, container-based).

> **Note:** The `bestbikeapp` resource group hosting this ACR/App Service has since been **deleted to save Azure Student subscription credits**. The image and public URL below are therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Docker image URL (no longer live):**
Azure Container Registry `bestbikeacrbavana`, repository `bestbikeapp`, tag `v4`.

**2. Public URL of the running application (no longer live):**
https://bestbikeapp-bavana.azurewebsites.net/

**Outcome:** `az webapp log tail` confirms the container started successfully ("Site started", `Now listening on: http://0.0.0.0:8181` inside the container) and the App Service state shows **Running**.

**Screenshots:**

- ACR repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3b/BestBikeApp/AzureContainerRegistryRepository.png
- App Service status (Running): https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3b/BestBikeApp/Azurerunningapplication.png
- App Service logs: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3b/BestBikeApp/AzureAppServicelog.png
- Running application: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3b/BestBikeApp/runningazureapplication1.png
- Verification log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3b/BestBikeApp/AzureCLIVerification.png
- Source: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-3/EX-3b/BestBikeApp/Url.txt

---

#### Exercise 4 — MCP

**What was done:**

- Built an MCP server exposing "monkey" data tools (`GetMonkeys`, `GetMonkey`) over **STDIO transport** (`ConsoleApp1`).
- Built the equivalent MCP server over **HTTP transport** (`SampleMCPHttp`), using `ModelContextProtocol.AspNetCore` and `app.MapMcp()`.
- Built an AI agent client (`AgentSample`) that connects to the MCP server (HTTP mode, `httpHostedMcpTool`), loads the exposed tools, and runs an interactive conversation loop where the agent calls the MCP tools to answer questions.

**Source code:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/Bavana/CC_Mywork/EX-4

**Screenshot:** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX-4/ConsoleApp1/Calculations.png

---

#### Exercise 5 — Blob Storage

Storage account: **bavanablobstorage**

> **Note:** The resource group hosting this storage account has since been **deleted to save Azure Student subscription credits**. The URLs and SAS links below are therefore no longer live/accessible — screenshots were taken beforehand as evidence of the completed exercise (see below).

**Container URLs (no longer live):**

1. Input — https://bavanablobstorage.blob.core.windows.net/input
2. Output — https://bavanablobstorage.blob.core.windows.net/output
3. Test — https://bavanablobstorage.blob.core.windows.net/test

**SAS URLs (Test container, issued for ~1 year — no longer live since the account was deleted):**

1. `test-file-1.txt` — https://bavanablobstorage.blob.core.windows.net/test/test-file1.txt?sp=r&st=2026-06-06T10:19:10Z&se=2027-03-01T19:34:10Z&spr=https&sv=2026-02-06&sr=b&sig=l7vpStXGZ48ZOL5HLO7Gh%2FDj8Syxrzn8r3BSM3Y9JrE%3D
2. `test-file-2.csv` — https://bavanablobstorage.blob.core.windows.net/test/test-file2.csv?sp=r&st=2026-06-06T10:20:58Z&se=2027-02-01T19:35:58Z&spr=https&sv=2026-02-06&sr=b&sig=AhwtfQ4BPDt56Hj%2FFd43nIkxuQ1Rc%2FZpx7wBehT3Ew4%3D
3. `test-image.png` — https://bavanablobstorage.blob.core.windows.net/test/test-image.png?sp=r&st=2026-06-06T10:21:46Z&se=2027-02-01T19:36:46Z&spr=https&sv=2026-02-06&sr=b&sig=EAJ3ACxHjGnYMSM2kGlJXncgCRcGe3RULqjIxliAUOI%3D

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/Bavana/CC_Mywork/EX5/BlobStorageSample/BlobStorageSample

---

#### Exercise 6 — Table Storage

Storage account: **bavanablobstorage**
Table: **ex5**

> **Note:** The resource group hosting this storage account has since been **deleted to save Azure Student subscription credits**. The URL below is no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

- Table Storage URL (live): https://bavanablobstorage.table.core.windows.net/ex5

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX6/Tableslist.png

---

#### Exercise 7 — Queue Storage

Storage account: **bavanablobstorage**
Queue: **queueexcercise**

> **Note:** The resource group hosting this storage account has since been **deleted to save Azure Student subscription credits**. The URL below is no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

- Queue Storage URL (live): https://bavanablobstorage.queue.core.windows.net/queueexcercise

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/Bavana/CC_Mywork/EX7/queuemessage.png

---
