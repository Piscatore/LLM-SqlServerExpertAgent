using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace SqlServerExpertAgent.Communication.AgentProtocol;

/// <summary>
/// A2A protocol transport layer using HTTPS with JSON-RPC 2.0
/// Handles secure agent-to-agent communication
/// </summary>
public class A2ATransport : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<A2ATransport> _logger;
    private readonly A2AConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;

    public A2ATransport(A2AConfiguration configuration, ILogger<A2ATransport> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(configuration.DefaultTimeoutSeconds);
        
        // Configure authentication headers
        if (!string.IsNullOrEmpty(configuration.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configuration.AuthToken);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Send A2A message to another agent
    /// </summary>
    public async Task<A2AMessageResult> SendMessageAsync(A2AMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending A2A message {MessageId} from {From} to {To}", 
                message.Id, message.From.Name, message.To.Name);

            var targetEndpoint = message.To.Endpoint ?? await ResolveAgentEndpointAsync(message.To.Id);
            if (string.IsNullOrEmpty(targetEndpoint))
            {
                return A2AMessageResult.CreateError(message.Id, "Target agent endpoint not found");
            }

            var requestUri = $"{targetEndpoint}/a2a/messages";
            
            // Apply timeout from message or configuration
            var timeout = message.TimeToLiveSeconds.HasValue 
                ? TimeSpan.FromSeconds(message.TimeToLiveSeconds.Value)
                : TimeSpan.FromSeconds(_configuration.DefaultTimeoutSeconds);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var response = await _httpClient.PostAsJsonAsync(requestUri, message, _jsonOptions, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var responseMessage = await response.Content.ReadFromJsonAsync<A2AMessage>(_jsonOptions, cts.Token);
                _logger.LogDebug("Received A2A response for message {MessageId}", message.Id);
                
                return A2AMessageResult.CreateSuccess(message.Id, responseMessage);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("A2A message failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                
                return A2AMessageResult.CreateError(message.Id, 
                    $"HTTP {response.StatusCode}: {errorContent}");
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "A2A message {MessageId} timed out", message.Id);
            return A2AMessageResult.CreateError(message.Id, "Message timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending A2A message {MessageId}", message.Id);
            return A2AMessageResult.CreateError(message.Id, ex.Message);
        }
    }

    /// <summary>
    /// Send message and wait for response
    /// </summary>
    public async Task<A2AMessage?> SendRequestAsync(A2AMessage request, CancellationToken cancellationToken = default)
    {
        var result = await SendMessageAsync(request, cancellationToken);
        return result.Success ? result.ResponseMessage : null;
    }

    /// <summary>
    /// Send fire-and-forget message
    /// </summary>
    public async Task<bool> SendNotificationAsync(A2AMessage notification, CancellationToken cancellationToken = default)
    {
        var result = await SendMessageAsync(notification, cancellationToken);
        return result.Success;
    }

    /// <summary>
    /// Discover agents in the network
    /// </summary>
    public async Task<List<AgentIdentifier>> DiscoverAgentsAsync(
        A2ADiscoveryRequest discoveryRequest, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new A2AMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = A2AMessageType.Discovery,
                From = _configuration.LocalAgent,
                To = _configuration.DiscoveryService,
                Priority = A2APriority.Normal,
                Payload = new A2APayload
                {
                    ContentType = A2APayloadTypes.AgentDiscovery,
                    Content = discoveryRequest
                }
            };

            var response = await SendRequestAsync(message, cancellationToken);
            if (response?.Payload.Content is A2ADiscoveryResponse discoveryResponse)
            {
                _logger.LogInformation("Discovered {AgentCount} agents", discoveryResponse.Agents.Count);
                return discoveryResponse.Agents;
            }

            _logger.LogWarning("Agent discovery failed or returned invalid response");
            return new List<AgentIdentifier>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent discovery");
            return new List<AgentIdentifier>();
        }
    }

    /// <summary>
    /// Broadcast capability advertisement
    /// </summary>
    public async Task<bool> AdvertiseCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new A2AMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = A2AMessageType.Capability,
                From = _configuration.LocalAgent,
                To = _configuration.DiscoveryService,
                Priority = A2APriority.Normal,
                Payload = new A2APayload
                {
                    ContentType = A2APayloadTypes.CapabilityAdvertisement,
                    Content = _configuration.LocalAgent
                }
            };

            var result = await SendMessageAsync(message, cancellationToken);
            if (result.Success)
            {
                _logger.LogInformation("Successfully advertised capabilities for agent {AgentName}", 
                    _configuration.LocalAgent.Name);
            }
            else
            {
                _logger.LogWarning("Failed to advertise capabilities: {Error}", result.Error);
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advertising capabilities");
            return false;
        }
    }

    private async Task<string?> ResolveAgentEndpointAsync(string agentId)
    {
        // In a real implementation, this would query a service registry
        // For now, return configuration-based endpoint mapping
        if (_configuration.KnownAgents.TryGetValue(agentId, out var endpoint))
        {
            return endpoint;
        }

        _logger.LogWarning("No endpoint found for agent {AgentId}", agentId);
        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// A2A protocol configuration
/// </summary>
public class A2AConfiguration
{
    public required AgentIdentifier LocalAgent { get; set; }
    public required AgentIdentifier DiscoveryService { get; set; }
    public string? AuthToken { get; set; }
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public Dictionary<string, string> KnownAgents { get; set; } = new();
    public string SecurityMode { get; set; } = "Bearer"; // "Bearer", "mTLS", "None"
    public bool EnableMessageLogging { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Result of A2A message sending
/// </summary>
public class A2AMessageResult
{
    public required string MessageId { get; set; }
    public required bool Success { get; set; }
    public A2AMessage? ResponseMessage { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static A2AMessageResult CreateSuccess(string messageId, A2AMessage? response = null) =>
        new() { MessageId = messageId, Success = true, ResponseMessage = response };

    public static A2AMessageResult CreateError(string messageId, string error) =>
        new() { MessageId = messageId, Success = false, Error = error };
}