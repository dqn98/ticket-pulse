namespace AdminDashboard.Services;

public class SeqLogService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SeqLogService> _logger;

    public SeqLogService(IConfiguration configuration, HttpClient httpClient, ILogger<SeqLogService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<SeqLogEntry>> GetRecentErrorsAsync(int count = 20)
    {
        try
        {
            var seqUrl = _configuration.GetConnectionString("seq") ?? "http://localhost:5341";
            var response = await _httpClient.GetAsync(
                $"{seqUrl}/api/events?count={count}&filter=@Level%20%3D%20'Error'%20or%20@Level%20%3D%20'Fatal'&render=true");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to query Seq: {StatusCode}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadFromJsonAsync<List<SeqLogEntry>>();
            return json ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching logs from Seq");
            return [];
        }
    }
}

public class SeqLogEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string RenderedMessage { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
