## Prerequisite to run the Application:
 You must have Docker Desktop installed.
## How to Run the Application
1. At the root of this repository open Git Bash or any CLI tool of your choice.
2. Run command `docker compose up --build -d`
3. This will start two containers, one for the multitenant-app running at http://localhost:8086 and another for MS Sql server
4. On startup, the application automatically creates databases and tables for all the Tenants configured in `tenants.json`. It'll also seed some random data into all Tenant's databases.
5. Call http://localhost:8086/persons with Header "X-Tenant-Id" with either "tenant-1" or "tenant-2".
6. You will see that the application will resolve the TenantId from "X-Tenant-Id" header and will fetch data from appropriate database for that Tenant.
