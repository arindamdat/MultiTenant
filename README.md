## Prerequisite to run the Application:
 You must have Docker Desktop installed.
## How to Run the Application
1. At the root of this repository open Git Bash or any CLI tool of your choice.
2. Run command `docker compose up --build -d`
3. This will start two containers, one for the multitenant-app running at http://localhost:8086 and another for MS Sql server.
4. On startup, the application automatically creates databases and tables for all the Tenants configured in `tenants.json`. It'll also seed some random data into all Tenant's databases.
5. The application now resolves the Tenant from the subdomain. To call the APIs, use a DNS service like nip.io. For example, access the API at `http://tenant1.127.0.0.1.nip.io:8086/persons` or `http://tenant2.127.0.0.1.nip.io:8086/persons`. The subdomain (e.g., `tenant1`) determines the tenant context.
6. You will see that the application resolves the TenantId from the subdomain and fetches data from the appropriate database for that Tenant.
7. The application is integrated with Keycloak for authentication and authorization. On startup, it automatically creates a Keycloak realm for each tenant and registers a client with each realm for authorization purposes.
8. To access protected APIs, obtain an access token from the Keycloak server for the relevant tenant's realm and client, and include it in the Authorization header of your API requests.
9. Refer to MultiTenant.http for sample API requests and how to include the access token for authentication and authorization.

## Keycloak integration for Authentication and Authorization
- On startup, a Keycloak realm is created for each tenant.
- A Keycloak client is registered for each tenant's realm.
- Use the appropriate realm and client to authenticate and authorize API requests for each tenant.
