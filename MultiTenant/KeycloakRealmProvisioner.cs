using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultiTenant
{
    public class KeycloakRealmProvisioner
    {
        private readonly HttpClient _httpClient;
        private readonly string _keycloakUrl;
        private readonly string _adminUser;
        private readonly string _adminPassword;
        private const string ClientId = "test-client";
        private const string ClientScope = "multi-tenant-api";

        public KeycloakRealmProvisioner(string keycloakUrl, string adminUser, string adminPassword)
        {
            _httpClient = new HttpClient();
            _keycloakUrl = keycloakUrl.TrimEnd('/');
            _adminUser = adminUser;
            _adminPassword = adminPassword;
        }

        public async Task ProvisionRealmsAsync(IEnumerable<Tenant> tenants)
        {
            var token = await GetAdminTokenAsync();
            foreach (var tenant in tenants)
            {
                var realmName = tenant.ShortName;
                if (!await RealmExistsAsync(realmName, token))
                {
                    await CreateRealmAsync(realmName, token);
                }
                await CreateClientScopeAsync(realmName, token);
                await CreateClientAsync(realmName, token);
                await AssignClientScopeAsync(realmName, token);
                await FetchAndLogClientInfoAsync(realmName, token);
            }
        }

        private async Task<string> GetAdminTokenAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", "admin-cli"),
                new KeyValuePair<string, string>("username", _adminUser),
                new KeyValuePair<string, string>("password", _adminPassword)
            });
            var response = await _httpClient.PostAsync($"{_keycloakUrl}/realms/master/protocol/openid-connect/token", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString();
        }

        private async Task<bool> RealmExistsAsync(string realmName, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_keycloakUrl}/admin/realms/{realmName}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        private async Task CreateRealmAsync(string realmName, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_keycloakUrl}/admin/realms");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var realm = new
            {
                realm = realmName,
                enabled = true
            };
            request.Content = new StringContent(JsonSerializer.Serialize(realm), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private async Task<bool> ClientScopeExistsAsync(string realmName, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_keycloakUrl}/admin/realms/{realmName}/client-scopes");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var scopes = JsonDocument.Parse(json).RootElement;
            foreach (var scope in scopes.EnumerateArray())
            {
                if (scope.TryGetProperty("name", out var nameProp) && nameProp.GetString() == ClientScope)
                    return true;
            }
            return false;
        }

        private async Task CreateClientScopeAsync(string realmName, string token)
        {
            if (await ClientScopeExistsAsync(realmName, token)) return;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_keycloakUrl}/admin/realms/{realmName}/client-scopes");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var scope = new
            {
                name = ClientScope,
                protocol = "openid-connect",
                description = "Multi-tenant API scope"
            };
            request.Content = new StringContent(JsonSerializer.Serialize(scope), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private async Task<bool> ClientExistsAsync(string realmName, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_keycloakUrl}/admin/realms/{realmName}/clients?clientId={ClientId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var clients = JsonDocument.Parse(json).RootElement;
            return clients.GetArrayLength() > 0;
        }

        private async Task CreateClientAsync(string realmName, string token)
        {
            if (await ClientExistsAsync(realmName, token)) return;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_keycloakUrl}/admin/realms/{realmName}/clients");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var client = new
            {
                clientId = ClientId,
                enabled = true,
                protocol = "openid-connect",
                publicClient = false,
                secret = "test-client-secret",
                directAccessGrantsEnabled = true,
                serviceAccountsEnabled = true,
                standardFlowEnabled = false,
                clientAuthenticatorType = "client-secret",
                fullScopeAllowed = false,
                protocolMappers = new[]
                {
                    new {
                        name = "audience",
                        protocol = "openid-connect",
                        protocolMapper = "oidc-audience-mapper",
                        consentRequired = false,
                        config = new Dictionary<string, string>
                        {
                            { "included.client.audience", "multi-tenant-api" },
                            { "id.token.claim", "false" },
                            { "access.token.claim", "true" }
                        }
                    }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(client), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private async Task AssignClientScopeAsync(string realmName, string token)
        {
            // Get client id (UUID) for test-client
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_keycloakUrl}/admin/realms/{realmName}/clients?clientId={ClientId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var clients = JsonDocument.Parse(json).RootElement;
            if (clients.GetArrayLength() == 0) return;
            var clientUuid = clients[0].GetProperty("id").GetString();

            // Get client scope id for multi-tenant-api
            request = new HttpRequestMessage(HttpMethod.Get, $"{_keycloakUrl}/admin/realms/{realmName}/client-scopes");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await _httpClient.SendAsync(request);
            json = await response.Content.ReadAsStringAsync();
            var scopes = JsonDocument.Parse(json).RootElement;
            string scopeId = null;
            foreach (var scope in scopes.EnumerateArray())
            {
                if (scope.TryGetProperty("name", out var nameProp) && nameProp.GetString() == ClientScope)
                {
                    scopeId = scope.GetProperty("id").GetString();
                    break;
                }
            }
            if (scopeId == null) return;

            // Assign client scope as default to client
            request = new HttpRequestMessage(HttpMethod.Put, $"{_keycloakUrl}/admin/realms/{realmName}/clients/{clientUuid}/default-client-scopes/{scopeId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private async Task FetchAndLogClientInfoAsync(string realmName, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_keycloakUrl}/admin/realms/{realmName}/clients?clientId={ClientId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var clients = JsonDocument.Parse(json).RootElement;
            if (clients.GetArrayLength() == 0)
            {
                Console.WriteLine($"Client '{ClientId}' not found in realm '{realmName}'.");
                return;
            }
            var client = clients[0];
            Console.WriteLine($"--- Client Info for '{ClientId}' in realm '{realmName}' ---");
            Console.WriteLine($"Raw JsonElement: {client}");
            Console.WriteLine($"publicClient: {client.GetProperty("publicClient").GetBoolean()}");
            Console.WriteLine($"serviceAccountsEnabled: {client.GetProperty("serviceAccountsEnabled").GetBoolean()}");
            Console.WriteLine($"protocol: {client.GetProperty("protocol").GetString()}");
            Console.WriteLine($"clientAuthenticatorType: {client.GetProperty("clientAuthenticatorType").GetString()}");
            if (client.TryGetProperty("defaultClientScopes", out var scopes))
            {
                Console.WriteLine($"defaultClientScopes: {string.Join(",", scopes.EnumerateArray().Select(x => x.GetString()))}");
            }
            if (client.TryGetProperty("secret", out var secret))
            {
                Console.WriteLine($"secret: {secret.GetString()}");
            }
            Console.WriteLine($"----------------------------------------------------------");
        }
    }
}
