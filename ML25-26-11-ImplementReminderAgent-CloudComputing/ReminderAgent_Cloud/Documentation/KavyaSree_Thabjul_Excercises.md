# My Exercises - KavyaSree Thabjul

Repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork  


---

### Exercise I - Scripts & Azure CLI

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

**Outcome:** Resource group `RG-ex1delme` was created in `westeurope`, verified, and successfully deleted - confirmed by re-running `az group list`. Full console transcript with outputs is committed in the repo.

**Script / output files:**

- https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex1

---

### Exercise 2-a - Docker with Dockerhub

Two apps were containerized and pushed to Docker Hub: a console app and a web app.

**1. Dockerfile URLs:**

- Console app: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex2/Docker

**2. Published Docker Hub images:**

- Console app: https://hub.docker.com/repository/docker/kavyasree05/learning/general

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

Full command log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/kavyasree/CC-MyWork/Ex2/Links.md

---

### Exercise 2-b - Docker with Azure

A .NET console app (`ContinerizationAzure`) was built, pushed to Azure Container Registry, and run via Azure Container Instance.

> **Note:** The resource group hosting this ACR/ACI has since been **deleted to save Azure Student subscription credits**. The registry/image below is therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**Outcome:** Image pushed successfully to ACR, then deployed as an Azure Container Instance running from that ACR image; container logs confirmed `Hello, World!` output.

**Screenshots:**

- ACR repository: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/kavyasree/CC-MyWork/Ex2/ex2.b/Images/ACR.png
- ACI run output: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/kavyasree/CC-MyWork/Ex2/ex2.b/Images/ACI.png
- Full command log: https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex2/ex2.b

---

### Exercise 3a - Host a web application with Azure App Service

Application: **BestBikeApp** - an ASP.NET MVC app deployed via ZIP deployment.

> **Note:** The `RG-BIKE` resource group hosting this App Service has since been **deleted to save Azure Student subscription credits**. The public URL below is therefore no longer live — screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Public URL (no longer live):**
https://bestbikeapp-kavya.azurewebsites.net/

**2. Source code URL:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex3/BestBikeApp


**Outcome:** Deployment succeeded (`provisioningState: Succeeded`), app restarted and reachable publicly at the URL above.

**3. Images:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/kavyasree/CC-MyWork/Ex3/Images/ex3a-website.png

**2. Links and scrpit:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/kavyasree/CC-MyWork/Ex3/Links.md

---

### Exercise 3b - Deploy and run the containerized app in App Service

Application: **BestCarApp** - deployed as a Docker container running in Azure App Service (Linux, container-based).

> **Note:** The `RG-CAR` resource group hosting this ACR/App Service has since been **deleted to save Azure Student subscription credits**. The image and public URL below are therefore no longer live - screenshots were taken beforehand as evidence of the completed exercise (see below).

**1. Docker image URL (no longer live):**
Azure Container Registry `bestcaracraish`, repository `bestcarapp`, tag `v1`.
 https://bestbikeapp-kavya.azurewebsites.net/


**2. Excercise link**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex3/Ex3b


---

### Exercise 4 - MCP
**Source code:**
https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex4

**Screenshot:** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/kavyasree/CC-MyWork/Ex4/CurrencyTool.png

---

### Exercise 5 - Blob Storage

**My Container URLs**

1. Input Container - https://kavyasreeblobstorage.blob.core.windows.net/input

2. Output Container - https://kavyasreeblobstorage.blob.core.windows.net/output

3. Test Container - https://kavyasreeblobstorage.blob.core.windows.net/test

**SAS URLs**

1. SAS URL for test-file-1.txt:
   https://kavyasreeblobstorage.blob.core.windows.net/test/test-file-1.txt?sp=r&st=2026-09-07T10:46:50Z&se=2027-05-04T19:01:50Z&spr=https&sv=2026-02-06&sr=b&sig=jQMriKD931gOH9xfYtvDmTEZdiByEEeeUi8XygXYTx0%3D

2. SAS URL for test-file-2.csv:
   https://kavyasreeblobstorage.blob.core.windows.net/test/test-file-2.csv?sp=r&st=2026-09-07T10:50:27Z&se=2026-09-07T19:05:27Z&spr=https&sv=2026-02-06&sr=b&sig=m8%2FyHvEX%2B37%2FEeJoOqfAUsRJ75%2F%2BwSai6exVUZq7Xs4%3D
   
3. SAS URL for test-image.png:
   https://kavyasreeblobstorage.blob.core.windows.net/test/test-image.png?sp=r&st=2026-09-07T10:51:17Z&se=2027-09-22T19:06:17Z&spr=https&sv=2026-02-06&sr=b&sig=iyCesrI42IoSySt38ya%2FgZZIMDxx9sVmPNIP%2BI0s7uo%3D

**Evidence (screenshots):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex5

---
### Exercise 6 - Table Storage
Storage account: **kavyastorageaccountex6**
Table: **TableExercise**

- Table Storage URL: https://kavyastorageaccountex6.table.core.windows.net/TableExercise

**Evidence (screenshots taken before deletion):** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex6

---

### Exercise 7 - Queue Storage

Storage account: **kavyasreeblobstorage**
Queue: **queueexercise**

- Queue Storage URL: https://kavyasreeblobstorage.queue.core.windows.net/queueexercise

**Evidence:** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/kavyasree/CC-MyWork/Ex7

---